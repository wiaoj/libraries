using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Linq.Expressions;
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
/// Provides asynchronous Entity Framework Core extensions for paginating <see cref="IQueryable{T}"/> sources
/// using three-level composite keys (primary + secondary + tie-breaker).
/// </summary>
public static partial class QueryablePaginationExtensions {
    private static readonly ConcurrentDictionary<(Expression, Expression, Expression), Delegate> CompiledComposite3SelectorCache = new();

    #region Keyset Pagination - Composite (Primary + Secondary + Tie-Breaker)

    /// <summary>
    /// Asynchronously executes deterministic keyset (cursor-based) pagination on an <see cref="IQueryable{TSource}"/>
    /// using a primary sorting key (<see cref="DateTimeOffset"/>), a secondary sorting key (<see cref="decimal"/>),
    /// and a tertiary unique tie-breaker key (<see cref="long"/>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Deterministic Ordering:</b> Evaluates a three-level composite boundary expression:
    /// <c>(A &gt; @a) OR (A == @a AND B &gt; @b) OR (A == @a AND B == @b AND Id &gt; @id)</c>.
    /// </para>
    /// <para>
    /// <b>Binary Encoding:</b> Combines all three keys into an opaque fixed-size 32-byte big-endian payload
    /// (8 bytes timestamp, 16 bytes decimal, 8 bytes identifier).
    /// </para>
    /// <para>
    /// <b>Ordering contract:</b> The queryable must be ordered with exactly three levels matching this call's
    /// key selectors, e.g. <c>.OrderBy(primary).ThenBy(secondary).ThenBy(tieBreaker)</c> (any direction mix).
    /// A chain with more or fewer levels than three is rejected (or padded) by <c>ExtractSortDirections</c> -
    /// see its remarks for why extra levels can't simply be inferred.
    /// </para>
    /// </remarks>
    /// <typeparam name="TSource">The entity or projected data model type.</typeparam>
    /// <param name="source">The source queryable. Must be explicitly ordered with exactly 3 levels before calling pagination.</param>
    /// <param name="request">The cursor request specifying the seek position, item limit, and direction.</param>
    /// <param name="primaryKeySelector">A lambda expression identifying the primary sorting timestamp property.</param>
    /// <param name="secondaryKeySelector">A lambda expression identifying the secondary sorting property.</param>
    /// <param name="tieBreakerSelector">A lambda expression identifying the tertiary unique tie-breaker property (e.g. <c>x => x.Id</c>).</param>
    /// <param name="cancellationToken">A token to observe while waiting for the database query to complete.</param>
    /// <returns>
    /// A task representing the asynchronous query execution, containing a <see cref="CursorResult{TSource}"/>
    /// with the items window and boundary metadata.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> or any key selector is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the queryable's ordering doesn't have exactly 3 levels.</exception>
    /// <exception cref="FormatException">Thrown when an incoming cursor token does not contain a valid composite payload.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the <paramref name="cancellationToken"/> is canceled.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task<CursorResult<TSource>> ToCursorResultAsync<TSource>(
        this IQueryable<TSource> source,
        CursorRequest request,
        Expression<Func<TSource, DateTimeOffset>> primaryKeySelector,
        Expression<Func<TSource, decimal>> secondaryKeySelector,
        Expression<Func<TSource, long>> tieBreakerSelector,
        CancellationToken cancellationToken = default) {

        Preca.ThrowIfNull(source);
        Preca.ThrowIfNull(primaryKeySelector);
        Preca.ThrowIfNull(secondaryKeySelector);
        Preca.ThrowIfNull(tieBreakerSelector);

        return ToCursorResultAsync(
            source,
            request,
            primaryKeySelector,
            secondaryKeySelector,
            tieBreakerSelector,
            cursorEncoder: static (time, secondary, id) => {
                Span<byte> buffer = stackalloc byte[sizeof(long) + 16 + sizeof(long)];
                BinaryPrimitives.WriteInt64BigEndian(buffer[..sizeof(long)], time.ToUnixTimeMilliseconds());

                Span<int> bits = stackalloc int[4];
                decimal.TryGetBits(secondary, bits, out _);
                for(int i = 0; i < 4; i++) {
                    BinaryPrimitives.WriteInt32BigEndian(buffer.Slice(sizeof(long) + (i * 4), 4), bits[i]);
                }

                BinaryPrimitives.WriteInt64BigEndian(buffer[(sizeof(long) + 16)..], id);
                return CursorToken.FromBytes(buffer);
            },
            cursorDecoder: static token => {
                const int expectedLength = sizeof(long) + 16 + sizeof(long);
                Span<byte> buffer = stackalloc byte[expectedLength];
                if(!token.TryDecode(buffer, out int written) || written != expectedLength) {
                    throw new FormatException("Invalid composite DateTimeOffset + decimal + long cursor payload.");
                }

                long unixMs = BinaryPrimitives.ReadInt64BigEndian(buffer[..sizeof(long)]);

                Span<int> bits = stackalloc int[4];
                for(int i = 0; i < 4; i++) {
                    bits[i] = BinaryPrimitives.ReadInt32BigEndian(buffer.Slice(sizeof(long) + (i * 4), 4));
                }

                long id = BinaryPrimitives.ReadInt64BigEndian(buffer[(sizeof(long) + 16)..]);
                return (DateTimeOffset.FromUnixTimeMilliseconds(unixMs), new decimal(bits), id);
            },
            cancellationToken);
    }

    /// <summary>
    /// Asynchronously executes deterministic keyset (cursor-based) pagination on an <see cref="IQueryable{TSource}"/>
    /// using two ordered text sorting keys (e.g. <c>LastName</c> + <c>FirstName</c>) and a tertiary unique
    /// tie-breaker key (<see cref="long"/>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Encoding:</b> Each string key is length-prefixed (4-byte big-endian UTF-8 byte count) so both
    /// variable-length fields can be unambiguously split back apart on decode, followed by an 8-byte
    /// big-endian tie-breaker identifier.
    /// </para>
    /// <para>
    /// <b>Ordering contract:</b> Same as the other 3-key overload - the queryable must be ordered with
    /// exactly three levels matching primary, secondary, and tie-breaker.
    /// </para>
    /// </remarks>
    /// <typeparam name="TSource">The entity or projected data model type.</typeparam>
    /// <param name="source">The source queryable. Must be explicitly ordered with exactly 3 levels before calling pagination.</param>
    /// <param name="request">The cursor request specifying the seek position, item limit, and direction.</param>
    /// <param name="primaryKeySelector">A lambda expression identifying the primary sorting text property (e.g. <c>x => x.LastName</c>).</param>
    /// <param name="secondaryKeySelector">A lambda expression identifying the secondary sorting text property (e.g. <c>x => x.FirstName</c>).</param>
    /// <param name="tieBreakerSelector">A lambda expression identifying the tertiary unique tie-breaker property (e.g. <c>x => x.Id</c>).</param>
    /// <param name="cancellationToken">A token to observe while waiting for the database query to complete.</param>
    /// <returns>
    /// A task representing the asynchronous query execution, containing a <see cref="CursorResult{TSource}"/>
    /// with the items window and boundary metadata.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> or any key selector is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the queryable's ordering doesn't have exactly 3 levels.</exception>
    /// <exception cref="FormatException">Thrown when an incoming cursor token does not contain a valid composite payload.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the <paramref name="cancellationToken"/> is canceled.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task<CursorResult<TSource>> ToCursorResultAsync<TSource>(
        this IQueryable<TSource> source,
        CursorRequest request,
        Expression<Func<TSource, string>> primaryKeySelector,
        Expression<Func<TSource, string>> secondaryKeySelector,
        Expression<Func<TSource, long>> tieBreakerSelector,
        CancellationToken cancellationToken = default) {

        Preca.ThrowIfNull(source);
        Preca.ThrowIfNull(primaryKeySelector);
        Preca.ThrowIfNull(secondaryKeySelector);
        Preca.ThrowIfNull(tieBreakerSelector);

        return ToCursorResultAsync(
            source,
            request,
            primaryKeySelector,
            secondaryKeySelector,
            tieBreakerSelector,
            cursorEncoder: static (primary, secondary, tie) => {
                string p = primary ?? string.Empty;
                string s = secondary ?? string.Empty;
                int pByteCount = Encoding.UTF8.GetByteCount(p);
                int sByteCount = Encoding.UTF8.GetByteCount(s);
                int totalLength = sizeof(int) + pByteCount + sizeof(int) + sByteCount + sizeof(long);

                using ValueBuffer<byte> buffer = new(totalLength, stackalloc byte[256]);
                Span<byte> span = buffer.Span;
                int offset = 0;

                BinaryPrimitives.WriteInt32BigEndian(span.Slice(offset, sizeof(int)), pByteCount);
                offset += sizeof(int);
                Encoding.UTF8.GetBytes(p, span.Slice(offset, pByteCount));
                offset += pByteCount;

                BinaryPrimitives.WriteInt32BigEndian(span.Slice(offset, sizeof(int)), sByteCount);
                offset += sizeof(int);
                Encoding.UTF8.GetBytes(s, span.Slice(offset, sByteCount));
                offset += sByteCount;

                BinaryPrimitives.WriteInt64BigEndian(span.Slice(offset, sizeof(long)), tie);

                return CursorToken.FromBytes(span);
            },
            cursorDecoder: static token => {
                const int minimumLength = sizeof(int) + sizeof(int) + sizeof(long);
                if(token.Length < minimumLength) {
                    throw new FormatException("Invalid composite string + string + long cursor payload.");
                }

                using ValueBuffer<byte> buffer = new(token.Length, stackalloc byte[256]);
                if(!token.TryDecode(buffer.Span, out int written) || written < minimumLength) {
                    throw new FormatException("Invalid composite string + string + long cursor payload.");
                }

                Span<byte> span = buffer.Span[..written];
                int offset = 0;

                int pByteCount = BinaryPrimitives.ReadInt32BigEndian(span.Slice(offset, sizeof(int)));
                offset += sizeof(int);
                if(pByteCount < 0 || offset + pByteCount + sizeof(int) > written) {
                    throw new FormatException("Invalid composite string + string + long cursor payload.");
                }
                string primary = Encoding.UTF8.GetString(span.Slice(offset, pByteCount));
                offset += pByteCount;

                int sByteCount = BinaryPrimitives.ReadInt32BigEndian(span.Slice(offset, sizeof(int)));
                offset += sizeof(int);
                if(sByteCount < 0 || offset + sByteCount + sizeof(long) != written) {
                    throw new FormatException("Invalid composite string + string + long cursor payload.");
                }
                string secondary = Encoding.UTF8.GetString(span.Slice(offset, sByteCount));
                offset += sByteCount;

                long tie = BinaryPrimitives.ReadInt64BigEndian(span.Slice(offset, sizeof(long)));
                return (primary, secondary, tie);
            },
            cancellationToken);
    }

    /// <summary>
    /// Generic terminal execution engine for three-level composite keyset (cursor-based) pagination on an
    /// <see cref="IQueryable{TSource}"/>. Evaluates independent ascending/descending sort directions for
    /// primary, secondary, and tie-breaker columns.
    /// </summary>
    /// <typeparam name="TSource">The entity or projected data model type.</typeparam>
    /// <typeparam name="TPrimary">The comparable primary key type used for sorting.</typeparam>
    /// <typeparam name="TSecondary">The comparable secondary key type used for sorting.</typeparam>
    /// <typeparam name="TTieBreaker">The comparable tertiary tie-breaker key type used for uniqueness.</typeparam>
    /// <param name="source">The source queryable. Must be ordered with exactly 3 levels, consistent with the seek predicate.</param>
    /// <param name="request">The cursor request specifying the seek position, item limit, and direction.</param>
    /// <param name="primaryKeySelector">A lambda expression identifying the primary key property to seek on.</param>
    /// <param name="secondaryKeySelector">A lambda expression identifying the secondary key property to seek on.</param>
    /// <param name="tieBreakerSelector">A lambda expression identifying the tertiary tie-breaker key property to seek on.</param>
    /// <param name="cursorEncoder">A delegate converting primary, secondary, and tie-breaker key instances into an opaque <see cref="CursorToken"/>.</param>
    /// <param name="cursorDecoder">A delegate converting an opaque <see cref="CursorToken"/> back into primary, secondary, and tie-breaker key instances.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the database query to complete.</param>
    /// <returns>
    /// A task representing the asynchronous query execution, containing a <see cref="CursorResult{TSource}"/>
    /// with the items and boundary metadata.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="source"/>, any key selector, <paramref name="cursorEncoder"/>, or
    /// <paramref name="cursorDecoder"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="InvalidOperationException">Thrown when the queryable's ordering doesn't have exactly 3 levels.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the <paramref name="cancellationToken"/> is canceled.</exception>
    public static async Task<CursorResult<TSource>> ToCursorResultAsync<TSource, TPrimary, TSecondary, TTieBreaker>(
        this IQueryable<TSource> source,
        CursorRequest request,
        Expression<Func<TSource, TPrimary>> primaryKeySelector,
        Expression<Func<TSource, TSecondary>> secondaryKeySelector,
        Expression<Func<TSource, TTieBreaker>> tieBreakerSelector,
        Func<TPrimary, TSecondary, TTieBreaker, CursorToken> cursorEncoder,
        Func<CursorToken, (TPrimary Primary, TSecondary Secondary, TTieBreaker TieBreaker)> cursorDecoder,
        CancellationToken cancellationToken = default)
        where TPrimary : IComparable<TPrimary>
        where TSecondary : IComparable<TSecondary>
        where TTieBreaker : IComparable<TTieBreaker> {

        Preca.ThrowIfNull(source);
        Preca.ThrowIfNull(primaryKeySelector);
        Preca.ThrowIfNull(secondaryKeySelector);
        Preca.ThrowIfNull(tieBreakerSelector);
        Preca.ThrowIfNull(cursorEncoder);
        Preca.ThrowIfNull(cursorDecoder);

        IQueryable<TSource> query = source;
        bool hasPrevious = false;

        // 1. Analyze independent sorting directions for all three levels. Throws if the queryable's
        //    OrderBy/ThenBy chain doesn't match exactly 3 levels - see ExtractSortDirections remarks.
        bool[] sortDirections = ExtractSortDirections(source.Expression, expectedLevelCount: 3);
        bool primaryIsDescending = sortDirections[0];
        bool secondaryIsDescending = sortDirections[1];
        bool tieBreakerIsDescending = sortDirections[2];

        // 2. Build and inject composite seek predicate
        if(!request.Cursor.IsEmpty) {
            (TPrimary pivotPrimary, TSecondary pivotSecondary, TTieBreaker pivotTieBreaker) = cursorDecoder(request.Cursor);
            hasPrevious = true;

            bool primarySeekGreater = (!primaryIsDescending && request.Direction == CursorDirection.Forward) ||
                                       (primaryIsDescending && request.Direction == CursorDirection.Backward);

            bool secondarySeekGreater = (!secondaryIsDescending && request.Direction == CursorDirection.Forward) ||
                                        (secondaryIsDescending && request.Direction == CursorDirection.Backward);

            bool tieSeekGreater = (!tieBreakerIsDescending && request.Direction == CursorDirection.Forward) ||
                                  (tieBreakerIsDescending && request.Direction == CursorDirection.Backward);

            ParameterExpression parameter = primaryKeySelector.Parameters[0];
            Expression remappedSecondaryBody = ParameterReplacer.Replace(secondaryKeySelector.Body, secondaryKeySelector.Parameters[0], parameter);
            Expression remappedTieBreakerBody = ParameterReplacer.Replace(tieBreakerSelector.Body, tieBreakerSelector.Parameters[0], parameter);

            ConstantExpression primaryConst = Expression.Constant(pivotPrimary, typeof(TPrimary));
            ConstantExpression secondaryConst = Expression.Constant(pivotSecondary, typeof(TSecondary));
            ConstantExpression tieConst = Expression.Constant(pivotTieBreaker, typeof(TTieBreaker));

            BinaryExpression primaryComp = BuildComparisonExpression(primaryKeySelector.Body, primaryConst, primarySeekGreater);
            BinaryExpression primaryEqual = Expression.Equal(primaryKeySelector.Body, primaryConst);
            BinaryExpression secondaryComp = BuildComparisonExpression(remappedSecondaryBody, secondaryConst, secondarySeekGreater);
            BinaryExpression secondaryEqual = Expression.Equal(remappedSecondaryBody, secondaryConst);
            BinaryExpression tieComp = BuildComparisonExpression(remappedTieBreakerBody, tieConst, tieSeekGreater);

            // Logic: (Primary seek) OR (Primary == pivot AND Secondary seek) OR (Primary == pivot AND Secondary == pivot AND TieBreaker seek)
            BinaryExpression secondLevel = Expression.AndAlso(primaryEqual, secondaryComp);
            BinaryExpression thirdLevel = Expression.AndAlso(Expression.AndAlso(primaryEqual, secondaryEqual), tieComp);
            BinaryExpression compositePredicate = Expression.OrElse(primaryComp, Expression.OrElse(secondLevel, thirdLevel));

            Expression<Func<TSource, bool>> lambda = Expression.Lambda<Func<TSource, bool>>(compositePredicate, parameter);
            query = query.Where(lambda);

            if(request.Direction == CursorDirection.Backward) {
                // Invert each column's direction individually
                IOrderedQueryable<TSource> reordered = primaryIsDescending
                    ? query.OrderBy(primaryKeySelector)
                    : query.OrderByDescending(primaryKeySelector);

                reordered = secondaryIsDescending
                    ? reordered.ThenBy(secondaryKeySelector)
                    : reordered.ThenByDescending(secondaryKeySelector);

                query = tieBreakerIsDescending
                    ? reordered.ThenBy(tieBreakerSelector)
                    : reordered.ThenByDescending(tieBreakerSelector);
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
        Func<TSource, (TPrimary Primary, TSecondary Secondary, TTieBreaker TieBreaker)> compiledSelectors =
            (Func<TSource, (TPrimary, TSecondary, TTieBreaker)>)CompiledComposite3SelectorCache.GetOrAdd(
                (primaryKeySelector, secondaryKeySelector, tieBreakerSelector),
                static key => {
                    Expression<Func<TSource, TPrimary>> p = (Expression<Func<TSource, TPrimary>>)key.Item1;
                    Expression<Func<TSource, TSecondary>> s = (Expression<Func<TSource, TSecondary>>)key.Item2;
                    Expression<Func<TSource, TTieBreaker>> t = (Expression<Func<TSource, TTieBreaker>>)key.Item3;
                    Func<TSource, TPrimary> pCompiled = p.Compile();
                    Func<TSource, TSecondary> sCompiled = s.Compile();
                    Func<TSource, TTieBreaker> tCompiled = t.Compile();
                    return (TSource item) => (pCompiled(item), sCompiled(item), tCompiled(item));
                });

        (TPrimary startP, TSecondary startS, TTieBreaker startT) = compiledSelectors(rawItems[0]);
        (TPrimary endP, TSecondary endS, TTieBreaker endT) = compiledSelectors(rawItems[^1]);

        CursorToken startCursor = cursorEncoder(startP, startS, startT);
        CursorToken endCursor = cursorEncoder(endP, endS, endT);

        CursorMetadata metadata = new(startCursor, endCursor, hasPrevious, hasNext);
        return new CursorResult<TSource>(new EquatableArray<TSource>(rawItems), metadata);
    }

    #endregion
}