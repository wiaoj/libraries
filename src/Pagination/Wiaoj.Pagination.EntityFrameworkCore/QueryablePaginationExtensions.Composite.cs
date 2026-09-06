using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using Wiaoj.Pagination;
using Wiaoj.Preconditions;
using Wiaoj.Primitives.Buffers;
using Wiaoj.Primitives.Collections;

#pragma warning disable IDE0130
namespace Microsoft.EntityFrameworkCore;
#pragma warning restore IDE0130

/// <summary>
/// Provides asynchronous Entity Framework Core extensions for paginating <see cref="IQueryable{T}"/> sources using composite keys.
/// </summary>
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
    /// <b>Binary Encoding:</b> Combines both keys into an opaque 16-byte big-endian payload (8 bytes timestamp and 8 bytes identifier).
    /// Supports backward-compatible decoding of single-key 8-byte timestamp cursor tokens.
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
    /// <exception cref="FormatException">Thrown when an incoming cursor token does not contain a valid composite payload.</exception>
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
                if(!token.TryDecode(buffer, out int written) || (written != sizeof(long) && written != (sizeof(long) + sizeof(long)))) {
                    throw new FormatException("Invalid composite DateTimeOffset + long cursor payload.");
                }
                long unixMs = BinaryPrimitives.ReadInt64BigEndian(buffer[..sizeof(long)]);
                long id = written == (sizeof(long) + sizeof(long))
                    ? BinaryPrimitives.ReadInt64BigEndian(buffer[sizeof(long)..])
                    : 0L;
                return (DateTimeOffset.FromUnixTimeMilliseconds(unixMs), id);
            },
            cancellationToken);
    }

    /// <summary>
    /// Asynchronously executes deterministic keyset (cursor-based) pagination on an <see cref="IQueryable{TSource}"/> 
    /// using a primary sorting key (<see cref="DateTime"/>) and a secondary unique tie-breaker key (<see cref="long"/>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Binary Encoding:</b> Combines both keys into an opaque 16-byte big-endian payload (8 bytes ticks and 8 bytes identifier).
    /// </para>
    /// </remarks>
    /// <typeparam name="TSource">The entity or projected data model type.</typeparam>
    /// <param name="source">The source queryable. Must be explicitly ordered before calling pagination.</param>
    /// <param name="request">The cursor request specifying the seek position, item limit, and direction.</param>
    /// <param name="primaryKeySelector">A lambda expression identifying the primary sorting date-time property (e.g. <c>x => x.CreatedAt</c>).</param>
    /// <param name="tieBreakerSelector">A lambda expression identifying the secondary unique tie-breaker property (e.g. <c>x => x.Id</c>).</param>
    /// <param name="cancellationToken">A token to observe while waiting for the database query to complete.</param>
    /// <returns>
    /// A task representing the asynchronous query execution, containing a <see cref="CursorResult{TSource}"/> 
    /// with the items window and boundary metadata.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/>, <paramref name="primaryKeySelector"/>, or <paramref name="tieBreakerSelector"/> is <see langword="null"/>.</exception>
    /// <exception cref="FormatException">Thrown when an incoming cursor token does not contain a valid composite payload.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the <paramref name="cancellationToken"/> is canceled.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task<CursorResult<TSource>> ToCursorResultAsync<TSource>(
        this IQueryable<TSource> source,
        CursorRequest request,
        Expression<Func<TSource, DateTime>> primaryKeySelector,
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
            cursorEncoder: static (dt, id) => {
                Span<byte> buffer = stackalloc byte[sizeof(long) + sizeof(long)];
                BinaryPrimitives.WriteInt64BigEndian(buffer[..sizeof(long)], dt.Ticks);
                BinaryPrimitives.WriteInt64BigEndian(buffer[sizeof(long)..], id);
                return CursorToken.FromBytes(buffer);
            },
            cursorDecoder: static token => {
                Span<byte> buffer = stackalloc byte[sizeof(long) + sizeof(long)];
                if(!token.TryDecode(buffer, out int written) || (written != sizeof(long) && written != (sizeof(long) + sizeof(long)))) {
                    throw new FormatException("Invalid composite DateTime + long cursor payload.");
                }
                long ticks = BinaryPrimitives.ReadInt64BigEndian(buffer[..sizeof(long)]);
                long id = written == (sizeof(long) + sizeof(long))
                    ? BinaryPrimitives.ReadInt64BigEndian(buffer[sizeof(long)..])
                    : 0L;
                return (new DateTime(ticks, DateTimeKind.Utc), id);
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
    /// <b>Binary Encoding:</b> Combines both keys into an opaque 24-byte big-endian payload (16 bytes decimal and 8 bytes identifier).
    /// Supports backward-compatible decoding of single-key 16-byte decimal cursor tokens.
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
    /// <exception cref="FormatException">Thrown when an incoming cursor token does not contain a valid composite payload.</exception>
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
                if(!token.TryDecode(buffer, out int written) || (written != 16 && written != (16 + sizeof(long)))) {
                    throw new FormatException("Invalid composite decimal + long cursor payload.");
                }
                Span<int> bits = stackalloc int[4];
                for(int i = 0; i < 4; i++) {
                    bits[i] = BinaryPrimitives.ReadInt32BigEndian(buffer.Slice(i * 4, 4));
                }
                long id = written == (16 + sizeof(long))
                    ? BinaryPrimitives.ReadInt64BigEndian(buffer[16..])
                    : 0L;
                return (new decimal(bits), id);
            },
            cancellationToken);
    }

    /// <summary>
    /// Asynchronously executes deterministic keyset (cursor-based) pagination on an <see cref="IQueryable{TSource}"/> 
    /// using a primary sorting key (<see cref="string"/>) and a secondary unique tie-breaker key (<see cref="long"/>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Encoding:</b> Combines the UTF-8 encoded string sequence and an 8-byte big-endian identifier payload.
    /// </para>
    /// </remarks>
    /// <typeparam name="TSource">The entity or projected data model type.</typeparam>
    /// <param name="source">The source queryable. Must be explicitly ordered before calling pagination.</param>
    /// <param name="request">The cursor request specifying the seek position, item limit, and direction.</param>
    /// <param name="primaryKeySelector">A lambda expression identifying the primary sorting string property (e.g. <c>x => x.Name</c>).</param>
    /// <param name="tieBreakerSelector">A lambda expression identifying the secondary unique tie-breaker property (e.g. <c>x => x.Id</c>).</param>
    /// <param name="cancellationToken">A token to observe while waiting for the database query to complete.</param>
    /// <returns>
    /// A task representing the asynchronous query execution, containing a <see cref="CursorResult{TSource}"/> 
    /// with the items window and boundary metadata.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/>, <paramref name="primaryKeySelector"/>, or <paramref name="tieBreakerSelector"/> is <see langword="null"/>.</exception>
    /// <exception cref="FormatException">Thrown when an incoming cursor token does not contain a valid composite payload.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the <paramref name="cancellationToken"/> is canceled.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task<CursorResult<TSource>> ToCursorResultAsync<TSource>(
        this IQueryable<TSource> source,
        CursorRequest request,
        Expression<Func<TSource, string>> primaryKeySelector,
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
            cursorEncoder: static (primary, tie) => {
                string s = primary ?? string.Empty;
                int strByteCount = Encoding.UTF8.GetByteCount(s);
                using ValueBuffer<byte> buffer = new(strByteCount + sizeof(long), stackalloc byte[256]);
                Encoding.UTF8.GetBytes(s, buffer.Span[..strByteCount]);
                BinaryPrimitives.WriteInt64BigEndian(buffer.Span[strByteCount..], tie);
                return CursorToken.FromBytes(buffer.Span);
            },
            cursorDecoder: static token => {
                if(token.Length < sizeof(long)) {
                    throw new FormatException("Invalid composite string + long cursor payload.");
                }
                using ValueBuffer<byte> buffer = new(token.Length, stackalloc byte[256]);
                if(!token.TryDecode(buffer.Span, out int written) || written < sizeof(long)) {
                    throw new FormatException("Invalid composite string + long cursor payload.");
                }
                int strLen = written - sizeof(long);
                string primary = Encoding.UTF8.GetString(buffer.Span[..strLen]);
                long tie = BinaryPrimitives.ReadInt64BigEndian(buffer.Span[strLen..]);
                return (primary, tie);
            },
            cancellationToken);
    }

    /// <summary>
    /// Generic terminal execution engine for composite keyset (cursor-based) pagination on an <see cref="IQueryable{TSource}"/>.
    /// Evaluates independent ascending and descending sort directions for primary and tie-breaker columns.
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

        // 1. Analyze independent sorting directions for primary and tie-breaker expressions
        bool[] sortDirections = ExtractSortDirections(source.Expression, expectedLevelCount: 2);
        bool primaryIsDescending = sortDirections[0];
        bool tieBreakerIsDescending = sortDirections[1];

        // 2. Build and inject composite seek predicate
        if(!request.Cursor.IsEmpty) {
            (TPrimary pivotPrimary, TTieBreaker pivotTieBreaker) = cursorDecoder(request.Cursor);
            hasPrevious = true;

            bool primarySeekGreater = (!primaryIsDescending && request.Direction == CursorDirection.Forward) ||
                                      (primaryIsDescending && request.Direction == CursorDirection.Backward);

            bool tieSeekGreater = (!tieBreakerIsDescending && request.Direction == CursorDirection.Forward) ||
                                  (tieBreakerIsDescending && request.Direction == CursorDirection.Backward);

            ParameterExpression parameter = primaryKeySelector.Parameters[0];
            Expression remappedTieBreakerBody = ParameterReplacer.Replace(tieBreakerSelector.Body, tieBreakerSelector.Parameters[0], parameter);

            ConstantExpression primaryConst = Expression.Constant(pivotPrimary, typeof(TPrimary));
            ConstantExpression tieConst = Expression.Constant(pivotTieBreaker, typeof(TTieBreaker));

            BinaryExpression primaryComp = BuildComparisonExpression(primaryKeySelector.Body, primaryConst, primarySeekGreater);
            BinaryExpression primaryEqual = Expression.Equal(primaryKeySelector.Body, primaryConst);
            BinaryExpression tieComp = BuildComparisonExpression(remappedTieBreakerBody, tieConst, tieSeekGreater);

            // Logic: (Primary seek condition) OR (Primary == pivotPrimary AND TieBreaker seek condition)
            BinaryExpression compositePredicate = Expression.OrElse(primaryComp, Expression.AndAlso(primaryEqual, tieComp));
            Expression<Func<TSource, bool>> lambda = Expression.Lambda<Func<TSource, bool>>(compositePredicate, parameter);

            query = query.Where(lambda);

            if(request.Direction == CursorDirection.Backward) {
                // Invert each column's direction individually
                query = primaryIsDescending
                    ? (tieBreakerIsDescending
                        ? query.OrderBy(primaryKeySelector).ThenBy(tieBreakerSelector)
                        : query.OrderBy(primaryKeySelector).ThenByDescending(tieBreakerSelector))
                    : (tieBreakerIsDescending
                        ? query.OrderByDescending(primaryKeySelector).ThenBy(tieBreakerSelector)
                        : query.OrderByDescending(primaryKeySelector).ThenByDescending(tieBreakerSelector));
            }
        }

        // 3. Fetch Limit + 1
        int fetchLimit = request.Limit + 1;
        List<TSource> rawItems = await query
            .Take(fetchLimit)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if(rawItems.Count == 0) {
            return CursorResult<TSource>.Empty;
        }

        // 4. Evaluate boundaries
        bool hasMore = rawItems.Count > request.Limit;
        if(hasMore) {
            rawItems.RemoveAt(rawItems.Count - 1);
        }

        if(request.Direction == CursorDirection.Backward && !request.Cursor.IsEmpty) {
            rawItems.Reverse();
        }

        bool hasNext = request.Direction == CursorDirection.Forward ? hasMore : !request.Cursor.IsEmpty;
        hasPrevious = request.Direction == CursorDirection.Forward ? !request.Cursor.IsEmpty : hasMore;

        // 5. Compile and execute delegate selectors
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

    private static BinaryExpression BuildComparisonExpression(Expression left, Expression right, bool isGreaterThan) {
        if(left.Type == typeof(string)) {
            MethodInfo compareMethod = typeof(string).GetMethod(nameof(string.Compare), [typeof(string), typeof(string)])!;
            MethodCallExpression compareCall = Expression.Call(compareMethod, left, right);
            ConstantExpression zero = Expression.Constant(0);
            return isGreaterThan
                ? Expression.GreaterThan(compareCall, zero)
                : Expression.LessThan(compareCall, zero);
        }

        return isGreaterThan
            ? Expression.GreaterThan(left, right)
            : Expression.LessThan(left, right);
    }

    /// <summary>
    /// Walks the <c>OrderBy</c>/<c>ThenBy</c> chain applied to <paramref name="expression"/> and returns
    /// each ordering level's direction, in true application order (index 0 = <c>OrderBy</c>,
    /// index 1 = first <c>ThenBy</c>, index 2 = second <c>ThenBy</c>, ...).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Expression trees for a chained <c>.OrderBy().ThenBy().ThenByDescending()</c> call nest with the
    /// <b>last-applied</b> call outermost (e.g. <c>ThenByDescending(Call(ThenBy(Call(OrderBy(...)))))</c>).
    /// Walking via <c>Arguments[0]</c> therefore visits levels in <b>reverse</b> application order, which
    /// this method corrects for before returning.
    /// </para>
    /// <para>
    /// Only the ordering levels corresponding to <paramref name="expectedLevelCount"/> key selectors are
    /// ever part of the seek (<c>WHERE</c>) predicate built by the caller. A chain with more levels than
    /// <paramref name="expectedLevelCount"/> would silently drop the extra key(s) from the seek boundary -
    /// producing non-deterministic pagination on ties - so that case throws instead of failing silently.
    /// </para>
    /// </remarks>
    /// <param name="expression">The queryable's expression tree (<c>source.Expression</c>).</param>
    /// <param name="expectedLevelCount">
    /// The exact number of ordering levels this pagination call expects, matching the number of key
    /// selectors (primary, secondary, ..., tie-breaker) passed by the caller.
    /// </param>
    /// <returns>An array of length <paramref name="expectedLevelCount"/> with each level's direction.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the queryable has no explicit ordering, or has more ordering levels than
    /// <paramref name="expectedLevelCount"/>. A chain with fewer levels than expected is tolerated: any
    /// missing trailing level defaults to the direction of the last present level.
    /// </exception>
    private static bool[] ExtractSortDirections(Expression expression, int expectedLevelCount) {
        List<bool> reverseOrderDirections = new(expectedLevelCount);

        Expression? current = expression;
        while(current is MethodCallExpression methodCall) {
            if(methodCall.Method.DeclaringType == typeof(Queryable)) {
                string name = methodCall.Method.Name;
                if(name is nameof(Queryable.OrderBy) or nameof(Queryable.OrderByDescending)
                         or nameof(Queryable.ThenBy) or nameof(Queryable.ThenByDescending)) {
                    reverseOrderDirections.Add(name is nameof(Queryable.OrderByDescending) or nameof(Queryable.ThenByDescending));
                }
            }
            current = methodCall.Arguments.Count > 0 ? methodCall.Arguments[0] : null;
        }

        if(reverseOrderDirections.Count == 0) {
            throw new InvalidOperationException(
                "The source queryable must be explicitly ordered (OrderBy/OrderByDescending) before calling keyset pagination.");
        }

        if(reverseOrderDirections.Count > expectedLevelCount) {
            throw new InvalidOperationException(
                $"The queryable's OrderBy/ThenBy chain has {reverseOrderDirections.Count} ordering level(s), but this " +
                $"pagination call was given {expectedLevelCount} key selector(s). Extra ordering levels are never part " +
                "of the seek (WHERE) predicate - only the explicitly passed key selectors are - so they would be " +
                "silently excluded from the pagination boundary and can cause skipped or duplicated rows across pages " +
                "when values tie. Align the OrderBy/ThenBy chain with the key selectors passed to this method.");
        }

        reverseOrderDirections.Reverse(); // now in true application order: [Primary, Secondary, ..., TieBreaker]

        bool[] directions = new bool[expectedLevelCount];
        for(int i = 0; i < expectedLevelCount; i++) {
            directions[i] = i < reverseOrderDirections.Count
                ? reverseOrderDirections[i]
                : directions[i - 1]; // missing trailing level defaults to the previous level's direction
        }
        return directions;
    }

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