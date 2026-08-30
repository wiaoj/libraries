using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using Wiaoj.Pagination;
using Wiaoj.Preconditions;
using Wiaoj.Primitives.Collections;

#pragma warning disable IDE0130
namespace Microsoft.EntityFrameworkCore;
#pragma warning restore IDE0130

public static partial class QueryablePaginationExtensions {
    private static readonly ConcurrentDictionary<(Expression, Expression), Delegate> CompiledCompositeSelectorCache = new();

    #region Keyset Pagination - Composite (Primary Key + Tie-Breaker)

    /// <summary>
    /// Asynchronously executes deterministic keyset (cursor-based) pagination on an <see cref="IQueryable{TSource}"/> 
    /// using a primary sorting key (<see cref="DateTimeOffset"/>) and a secondary unique tie-breaker key (<see cref="long"/>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Deterministic Ordering:</b> Eliminates data loss and infinite loops when multiple records share identical primary key timestamps
    /// by evaluating composite boundary expressions: <c>(CreatedAt > @p_time) OR (CreatedAt == @p_time AND Id > @p_id)</c>.
    /// </para>
    /// <para>
    /// <b>Binary Encoding:</b> Combines both keys into an opaque 16-byte big-endian payload (8 bytes timestamp + 8 bytes ID).
    /// </para>
    /// </remarks>
    /// <typeparam name="TSource">The entity or projected data model type.</typeparam>
    /// <param name="source">The source queryable. Must be explicitly ordered before calling pagination.</param>
    /// <param name="request">The cursor request specifying the seek position, item limit, and direction.</param>
    /// <param name="primaryKeySelector">A lambda expression identifying the primary sorting timestamp property (e.g. <c>x => x.CreatedAt</c>).</param>
    /// <param name="tieBreakerSelector">A lambda expression identifying the secondary unique tie-breaker property (e.g. <c>x => x.Id</c>).</param>
    /// <param name="cancellationToken">A token to observe while waiting for the database query to complete.</param>
    /// <returns>
    /// A task representing the asynchronous query execution, containing a <see cref="CursorResult{TSource}"/> 
    /// with the items window and boundary metadata.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/>, <paramref name="primaryKeySelector"/>, or <paramref name="tieBreakerSelector"/> is <see langword="null"/>.</exception>
    /// <exception cref="FormatException">Thrown when an incoming cursor token does not contain a valid 16-byte composite payload.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the <paramref name="cancellationToken"/> is canceled.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task<CursorResult<TSource>> ToCursorResultAsync<TSource>(
        this IQueryable<TSource> source,
        CursorRequest request,
        Expression<Func<TSource, DateTimeOffset>> primaryKeySelector,
        Expression<Func<TSource, long>> tieBreakerSelector,
        CancellationToken cancellationToken = default) {

        Preca.ThrowIfNull(source);
        Preca.ThrowIfNull(primaryKeySelector);
        Preca.ThrowIfNull(tieBreakerSelector);

        return ToCursorResultAsync(
            source,
            request,
            primaryKeySelector,
            tieBreakerSelector,
            cursorEncoder: static (time, id) => {
                Span<byte> buffer = stackalloc byte[sizeof(long) + sizeof(long)];
                BinaryPrimitives.WriteInt64BigEndian(buffer[..sizeof(long)], time.ToUnixTimeMilliseconds());
                BinaryPrimitives.WriteInt64BigEndian(buffer[sizeof(long)..], id);
                return CursorToken.FromBytes(buffer);
            },
            cursorDecoder: static token => {
                Span<byte> buffer = stackalloc byte[sizeof(long) + sizeof(long)];
                if(!token.TryDecode(buffer, out int written) || written != (sizeof(long) + sizeof(long))) {
                    throw new FormatException("Invalid composite DateTimeOffset + long cursor payload.");
                }
                long unixMs = BinaryPrimitives.ReadInt64BigEndian(buffer[..sizeof(long)]);
                long id = BinaryPrimitives.ReadInt64BigEndian(buffer[sizeof(long)..]);
                return (DateTimeOffset.FromUnixTimeMilliseconds(unixMs), id);
            },
            cancellationToken);
    }

    /// <summary>
    /// Asynchronously executes deterministic keyset (cursor-based) pagination on an <see cref="IQueryable{TSource}"/> 
    /// using a primary sorting key (<see cref="decimal"/>) and a secondary unique tie-breaker key (<see cref="long"/>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Deterministic Ordering:</b> Eliminates data loss when multiple records share identical prices
    /// by evaluating composite boundary expressions: <c>(Price > @p_price) OR (Price == @p_price AND Id > @p_id)</c>.
    /// </para>
    /// <para>
    /// <b>Binary Encoding:</b> Combines both keys into an opaque 24-byte big-endian payload (16 bytes decimal + 8 bytes ID).
    /// </para>
    /// </remarks>
    /// <typeparam name="TSource">The entity or projected data model type.</typeparam>
    /// <param name="source">The source queryable. Must be explicitly ordered before calling pagination.</param>
    /// <param name="request">The cursor request specifying the seek position, item limit, and direction.</param>
    /// <param name="primaryKeySelector">A lambda expression identifying the primary sorting price property (e.g. <c>x => x.Price</c>).</param>
    /// <param name="tieBreakerSelector">A lambda expression identifying the secondary unique tie-breaker property (e.g. <c>x => x.Id</c>).</param>
    /// <param name="cancellationToken">A token to observe while waiting for the database query to complete.</param>
    /// <returns>
    /// A task representing the asynchronous query execution, containing a <see cref="CursorResult{TSource}"/> 
    /// with the items window and boundary metadata.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/>, <paramref name="primaryKeySelector"/>, or <paramref name="tieBreakerSelector"/> is <see langword="null"/>.</exception>
    /// <exception cref="FormatException">Thrown when an incoming cursor token does not contain a valid 24-byte composite payload.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the <paramref name="cancellationToken"/> is canceled.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task<CursorResult<TSource>> ToCursorResultAsync<TSource>(
        this IQueryable<TSource> source,
        CursorRequest request,
        Expression<Func<TSource, decimal>> primaryKeySelector,
        Expression<Func<TSource, long>> tieBreakerSelector,
        CancellationToken cancellationToken = default) {

        Preca.ThrowIfNull(source);
        Preca.ThrowIfNull(primaryKeySelector);
        Preca.ThrowIfNull(tieBreakerSelector);

        return ToCursorResultAsync(
            source,
            request,
            primaryKeySelector,
            tieBreakerSelector,
            cursorEncoder: static (price, id) => {
                Span<byte> buffer = stackalloc byte[16 + sizeof(long)];
                Span<int> bits = stackalloc int[4];
                decimal.TryGetBits(price, bits, out _);
                for(int i = 0; i < 4; i++) {
                    BinaryPrimitives.WriteInt32BigEndian(buffer.Slice(i * 4, 4), bits[i]);
                }
                BinaryPrimitives.WriteInt64BigEndian(buffer[16..], id);
                return CursorToken.FromBytes(buffer);
            },
            cursorDecoder: static token => {
                Span<byte> buffer = stackalloc byte[16 + sizeof(long)];
                if(!token.TryDecode(buffer, out int written) || written != (16 + sizeof(long))) {
                    throw new FormatException("Invalid composite decimal + long cursor payload.");
                }
                Span<int> bits = stackalloc int[4];
                for(int i = 0; i < 4; i++) {
                    bits[i] = BinaryPrimitives.ReadInt32BigEndian(buffer.Slice(i * 4, 4));
                }
                long id = BinaryPrimitives.ReadInt64BigEndian(buffer[16..]);
                return (new decimal(bits), id);
            },
            cancellationToken);
    }

    /// <summary>
    /// Generic terminal execution engine for composite keyset (cursor-based) pagination on an <see cref="IQueryable{TSource}"/>.
    /// </summary>
    /// <typeparam name="TSource">The entity or projected data model type.</typeparam>
    /// <typeparam name="TPrimary">The comparable primary key type used for sorting.</typeparam>
    /// <typeparam name="TTieBreaker">The comparable secondary tie-breaker key type used for uniqueness.</typeparam>
    /// <param name="source">The source queryable. Must be ordered consistently with the seek predicate.</param>
    /// <param name="request">The cursor request specifying the seek position, item limit, and direction.</param>
    /// <param name="primaryKeySelector">A lambda expression identifying the primary key property to seek on.</param>
    /// <param name="tieBreakerSelector">A lambda expression identifying the secondary tie-breaker key property to seek on.</param>
    /// <param name="cursorEncoder">A delegate converting primary and tie-breaker key instances into an opaque <see cref="CursorToken"/>.</param>
    /// <param name="cursorDecoder">A delegate converting an opaque <see cref="CursorToken"/> back into primary and tie-breaker key instances.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the database query to complete.</param>
    /// <returns>
    /// A task representing the asynchronous query execution, containing a <see cref="CursorResult{TSource}"/> 
    /// with the items and boundary metadata.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="source"/>, <paramref name="primaryKeySelector"/>, <paramref name="tieBreakerSelector"/>, 
    /// <paramref name="cursorEncoder"/>, or <paramref name="cursorDecoder"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="OperationCanceledException">Thrown when the <paramref name="cancellationToken"/> is canceled.</exception>
    public static async Task<CursorResult<TSource>> ToCursorResultAsync<TSource, TPrimary, TTieBreaker>(
        this IQueryable<TSource> source,
        CursorRequest request,
        Expression<Func<TSource, TPrimary>> primaryKeySelector,
        Expression<Func<TSource, TTieBreaker>> tieBreakerSelector,
        Func<TPrimary, TTieBreaker, CursorToken> cursorEncoder,
        Func<CursorToken, (TPrimary Primary, TTieBreaker TieBreaker)> cursorDecoder,
        CancellationToken cancellationToken = default)
        where TPrimary : IComparable<TPrimary>
        where TTieBreaker : IComparable<TTieBreaker> {

        Preca.ThrowIfNull(source);
        Preca.ThrowIfNull(primaryKeySelector);
        Preca.ThrowIfNull(tieBreakerSelector);
        Preca.ThrowIfNull(cursorEncoder);
        Preca.ThrowIfNull(cursorDecoder);

        IQueryable<TSource> query = source;
        bool hasPrevious = false;
        bool isDescending = IsQueryOrderedDescending(source.Expression);

        // 1. Build and inject composite seek predicate
        if(!request.Cursor.IsEmpty) {
            (TPrimary pivotPrimary, TTieBreaker pivotTieBreaker) = cursorDecoder(request.Cursor);
            hasPrevious = true;

            bool seekGreaterThan = (!isDescending && request.Direction == CursorDirection.Forward) ||
                                   (isDescending && request.Direction == CursorDirection.Backward);

            ParameterExpression parameter = primaryKeySelector.Parameters[0];
            Expression remappedTieBreakerBody = ParameterReplacer.Replace(tieBreakerSelector.Body, tieBreakerSelector.Parameters[0], parameter);

            ConstantExpression primaryConst = Expression.Constant(pivotPrimary, typeof(TPrimary));
            ConstantExpression tieConst = Expression.Constant(pivotTieBreaker, typeof(TTieBreaker));

            BinaryExpression primaryComp = seekGreaterThan
                ? Expression.GreaterThan(primaryKeySelector.Body, primaryConst)
                : Expression.LessThan(primaryKeySelector.Body, primaryConst);

            BinaryExpression primaryEqual = Expression.Equal(primaryKeySelector.Body, primaryConst);

            BinaryExpression tieComp = seekGreaterThan
                ? Expression.GreaterThan(remappedTieBreakerBody, tieConst)
                : Expression.LessThan(remappedTieBreakerBody, tieConst);

            // Logic: (Primary > pivotPrimary) OR (Primary == pivotPrimary AND TieBreaker > pivotTieBreaker)
            BinaryExpression compositePredicate = Expression.OrElse(primaryComp, Expression.AndAlso(primaryEqual, tieComp));
            Expression<Func<TSource, bool>> lambda = Expression.Lambda<Func<TSource, bool>>(compositePredicate, parameter);

            query = query.Where(lambda);

            if(request.Direction == CursorDirection.Backward) {
                query = isDescending
                    ? query.OrderBy(primaryKeySelector).ThenBy(tieBreakerSelector)
                    : query.OrderByDescending(primaryKeySelector).ThenByDescending(tieBreakerSelector);
            }
        }

        // 2. Fetch Limit + 1
        int fetchLimit = request.Limit + 1;
        List<TSource> rawItems = await query
            .Take(fetchLimit)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if(rawItems.Count == 0) {
            return CursorResult<TSource>.Empty;
        }

        // 3. Evaluate boundaries
        bool hasMore = rawItems.Count > request.Limit;
        if(hasMore) {
            rawItems.RemoveAt(rawItems.Count - 1);
        }

        if(request.Direction == CursorDirection.Backward && !request.Cursor.IsEmpty) {
            rawItems.Reverse();
        }

        bool hasNext = request.Direction == CursorDirection.Forward ? hasMore : !request.Cursor.IsEmpty;
        hasPrevious = request.Direction == CursorDirection.Forward ? !request.Cursor.IsEmpty : hasMore;

        // 4. Compile and execute delegate selectors
        Func<TSource, (TPrimary Primary, TTieBreaker TieBreaker)> compiledSelectors =
            (Func<TSource, (TPrimary, TTieBreaker)>)CompiledCompositeSelectorCache.GetOrAdd(
                (primaryKeySelector, tieBreakerSelector),
                static key => {
                    Expression<Func<TSource, TPrimary>> p = (Expression<Func<TSource, TPrimary>>)key.Item1;
                    Expression<Func<TSource, TTieBreaker>> t = (Expression<Func<TSource, TTieBreaker>>)key.Item2;
                    Func<TSource, TPrimary> pCompiled = p.Compile();
                    Func<TSource, TTieBreaker> tCompiled = t.Compile();
                    return (TSource item) => (pCompiled(item), tCompiled(item));
                });

        (TPrimary startP, TTieBreaker startT) = compiledSelectors(rawItems[0]);
        (TPrimary endP, TTieBreaker endT) = compiledSelectors(rawItems[^1]);

        CursorToken startCursor = cursorEncoder(startP, startT);
        CursorToken endCursor = cursorEncoder(endP, endT);

        CursorMetadata metadata = new(startCursor, endCursor, hasPrevious, hasNext);
        return new CursorResult<TSource>(new EquatableArray<TSource>(rawItems), metadata);
    }

    #endregion

    private sealed class ParameterReplacer : ExpressionVisitor {
        private readonly ParameterExpression _source;
        private readonly ParameterExpression _target;

        private ParameterReplacer(ParameterExpression source, ParameterExpression target) {
            this._source = source;
            this._target = target;
        }

        public static Expression Replace(Expression body, ParameterExpression source, ParameterExpression target) {
            return new ParameterReplacer(source, target).Visit(body);
        }

        protected override Expression VisitParameter(ParameterExpression node) {
            return node == this._source ? this._target : base.VisitParameter(node);
        }
    }
}