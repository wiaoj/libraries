using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using Wiaoj.Pagination;
using Wiaoj.Preconditions;
using Wiaoj.Preconditions.Exceptions;
using Wiaoj.Primitives.Collections;
using Wiaoj.Primitives.Snowflake;

#pragma warning disable IDE0130
namespace Microsoft.EntityFrameworkCore;
#pragma warning restore IDE0130


/// <summary>
/// Provides asynchronous Entity Framework Core extensions for paginating <see cref="IQueryable{T}"/> sources.
/// </summary>
public static partial class QueryablePaginationExtensions { 
    private static readonly ConcurrentDictionary<Expression, Delegate> CompiledKeySelectorCache = new();


    #region Keyset (Cursor) Pagination - Built-in Types

    /// <summary>
    /// Asynchronously executes high-performance keyset (cursor-based) pagination on an <see cref="IQueryable{TSource}"/> 
    /// ordered by a 64-bit integer (<see cref="long"/>) key, utilizing the N+1 count elimination technique.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Performance:</b> This method completely eliminates the need for expensive <c>COUNT(*)</c> and large <c>OFFSET</c> 
    /// database queries by requesting <c>Limit + 1</c> items. The presence of the extra record determines whether a next page exists,
    /// after which it is discarded before returning the result.
    /// </para>
    /// <para>
    /// <b>Binary Encoding:</b> Cursors are encoded into an opaque 8-byte big-endian binary payload wrapped in a Base64Url string,
    /// avoiding heap allocations and string formatting overhead.
    /// </para>
    /// </remarks>
    /// <typeparam name="TSource">The entity or projected data model type.</typeparam>
    /// <param name="source">The source queryable. Must be explicitly ordered before calling pagination.</param>
    /// <param name="request">The cursor request specifying the seek position, item limit, and direction.</param>
    /// <param name="keySelector">A lambda expression identifying the 64-bit integer key property (e.g. <c>x => x.Id</c>).</param>
    /// <param name="cancellationToken">A token to observe while waiting for the database query to complete.</param>
    /// <returns>
    /// A task representing the asynchronous query execution, containing a <see cref="CursorResult{TSource}"/> 
    /// with the items window and boundary metadata.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> or <paramref name="keySelector"/> is <see langword="null"/>.</exception>
    /// <exception cref="FormatException">Thrown when an incoming cursor token does not contain a valid 64-bit integer payload.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the <paramref name="cancellationToken"/> is canceled.</exception>
    /// <example>
    /// <code>
    /// var result = await dbContext.Orders
    ///     .AsNoTracking()
    ///     .OrderBy(o => o.Id)
    ///     .ToCursorResultAsync(request, o => o.Id, cancellationToken);
    /// </code>
    /// </example>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task<CursorResult<TSource>> ToCursorResultAsync<TSource>(
        this IQueryable<TSource> source,
        CursorRequest request,
        Expression<Func<TSource, long>> keySelector,
        CancellationToken cancellationToken = default) {

        Preca.ThrowIfNull(source);
        Preca.ThrowIfNull(keySelector);

        return ToCursorResultAsync(
            source,
            request,
            keySelector,
            cursorEncoder: static id => {
                Span<byte> buffer = stackalloc byte[sizeof(long)];
                BinaryPrimitives.WriteInt64BigEndian(buffer, id);
                return CursorToken.FromBytes(buffer);
            },
            cursorDecoder: static token => {
                Span<byte> buffer = stackalloc byte[sizeof(long)];
                if(!token.TryDecode(buffer, out int bytesWritten) || bytesWritten != sizeof(long)) {
                    throw new FormatException("Invalid 64-bit integer cursor payload.");
                }
                return BinaryPrimitives.ReadInt64BigEndian(buffer);
            },
            cancellationToken);
    }

    /// <summary>
    /// Asynchronously executes high-performance keyset (cursor-based) pagination on an <see cref="IQueryable{TSource}"/> 
    /// ordered by a 32-bit integer (<see cref="int"/>) key, utilizing the N+1 count elimination technique.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Performance:</b> Requests <c>Limit + 1</c> items from the database to evaluate page boundaries without executing an additional count query.
    /// </para>
    /// <para>
    /// <b>Binary Encoding:</b> Cursors are encoded into an opaque 4-byte big-endian binary payload wrapped in a Base64Url string.
    /// </para>
    /// </remarks>
    /// <typeparam name="TSource">The entity or projected data model type.</typeparam>
    /// <param name="source">The source queryable. Must be explicitly ordered before calling pagination.</param>
    /// <param name="request">The cursor request specifying the seek position, item limit, and direction.</param>
    /// <param name="keySelector">A lambda expression identifying the 32-bit integer key property (e.g. <c>x => x.Id</c>).</param>
    /// <param name="cancellationToken">A token to observe while waiting for the database query to complete.</param>
    /// <returns>
    /// A task representing the asynchronous query execution, containing a <see cref="CursorResult{TSource}"/> 
    /// with the items window and boundary metadata.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> or <paramref name="keySelector"/> is <see langword="null"/>.</exception>
    /// <exception cref="FormatException">Thrown when an incoming cursor token does not contain a valid 32-bit integer payload.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the <paramref name="cancellationToken"/> is canceled.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task<CursorResult<TSource>> ToCursorResultAsync<TSource>(
        this IQueryable<TSource> source,
        CursorRequest request,
        Expression<Func<TSource, int>> keySelector,
        CancellationToken cancellationToken = default) {

        Preca.ThrowIfNull(source);
        Preca.ThrowIfNull(keySelector);

        return ToCursorResultAsync(
            source,
            request,
            keySelector,
            cursorEncoder: static id => {
                Span<byte> buffer = stackalloc byte[sizeof(int)];
                BinaryPrimitives.WriteInt32BigEndian(buffer, id);
                return CursorToken.FromBytes(buffer);
            },
            cursorDecoder: static token => {
                Span<byte> buffer = stackalloc byte[sizeof(int)];
                if(!token.TryDecode(buffer, out int bytesWritten) || bytesWritten != sizeof(int)) {
                    throw new FormatException("Invalid 32-bit integer cursor payload.");
                }
                return BinaryPrimitives.ReadInt32BigEndian(buffer);
            },
            cancellationToken);
    }

    /// <summary>
    /// Asynchronously executes high-performance keyset (cursor-based) pagination on an <see cref="IQueryable{TSource}"/> 
    /// ordered by a <see cref="Guid"/> key, utilizing the N+1 count elimination technique.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Binary Encoding:</b> Cursors are encoded into an opaque 16-byte binary payload wrapped in a Base64Url string.
    /// </para>
    /// </remarks>
    /// <typeparam name="TSource">The entity or projected data model type.</typeparam>
    /// <param name="source">The source queryable. Must be explicitly ordered before calling pagination.</param>
    /// <param name="request">The cursor request specifying the seek position, item limit, and direction.</param>
    /// <param name="keySelector">A lambda expression identifying the <see cref="Guid"/> key property.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the database query to complete.</param>
    /// <returns>
    /// A task representing the asynchronous query execution, containing a <see cref="CursorResult{TSource}"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> or <paramref name="keySelector"/> is <see langword="null"/>.</exception>
    /// <exception cref="FormatException">Thrown when an incoming cursor token does not contain a valid 16-byte Guid payload.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the <paramref name="cancellationToken"/> is canceled.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task<CursorResult<TSource>> ToCursorResultAsync<TSource>(
        this IQueryable<TSource> source,
        CursorRequest request,
        Expression<Func<TSource, Guid>> keySelector,
        CancellationToken cancellationToken = default) {

        Preca.ThrowIfNull(source);
        Preca.ThrowIfNull(keySelector);

        return ToCursorResultAsync(
            source,
            request,
            keySelector,
            cursorEncoder: static guid => {
                Span<byte> buffer = stackalloc byte[16];
                guid.TryWriteBytes(buffer);
                return CursorToken.FromBytes(buffer);
            },
            cursorDecoder: static token => {
                Span<byte> buffer = stackalloc byte[16];
                if(!token.TryDecode(buffer, out int bytesWritten) || bytesWritten != 16) {
                    throw new FormatException("Invalid Guid cursor payload.");
                }
                return new Guid(buffer);
            },
            cancellationToken);
    }

    /// <summary>
    /// Asynchronously executes high-performance keyset (cursor-based) pagination on an <see cref="IQueryable{TSource}"/> 
    /// ordered by a <see cref="DateTimeOffset"/> timestamp key, utilizing the N+1 count elimination technique.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Timestamp Precision:</b> The timestamp is stored as a 64-bit Unix millisecond integer in big-endian format, 
    /// ensuring exact chronology across distributed systems and time zones without timezone skew issues.
    /// </para>
    /// </remarks>
    /// <typeparam name="TSource">The entity or projected data model type.</typeparam>
    /// <param name="source">The source queryable. Must be explicitly ordered before calling pagination.</param>
    /// <param name="request">The cursor request specifying the seek position, item limit, and direction.</param>
    /// <param name="keySelector">A lambda expression identifying the <see cref="DateTimeOffset"/> timestamp property (e.g. <c>x => x.CreatedAt</c>).</param>
    /// <param name="cancellationToken">A token to observe while waiting for the database query to complete.</param>
    /// <returns>
    /// A task representing the asynchronous query execution, containing a <see cref="CursorResult{TSource}"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> or <paramref name="keySelector"/> is <see langword="null"/>.</exception>
    /// <exception cref="FormatException">Thrown when an incoming cursor token does not contain a valid timestamp payload.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the <paramref name="cancellationToken"/> is canceled.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task<CursorResult<TSource>> ToCursorResultAsync<TSource>(
        this IQueryable<TSource> source,
        CursorRequest request,
        Expression<Func<TSource, DateTimeOffset>> keySelector,
        CancellationToken cancellationToken = default) {

        Preca.ThrowIfNull(source);
        Preca.ThrowIfNull(keySelector);

        return ToCursorResultAsync(
            source,
            request,
            keySelector,
            cursorEncoder: static dto => {
                Span<byte> buffer = stackalloc byte[sizeof(long)];
                BinaryPrimitives.WriteInt64BigEndian(buffer, dto.ToUnixTimeMilliseconds());
                return CursorToken.FromBytes(buffer);
            },
            cursorDecoder: static token => {
                Span<byte> buffer = stackalloc byte[sizeof(long)];
                if(!token.TryDecode(buffer, out int bytesWritten) || bytesWritten != sizeof(long)) {
                    throw new FormatException("Invalid DateTimeOffset cursor payload.");
                }
                long unixMs = BinaryPrimitives.ReadInt64BigEndian(buffer);
                return DateTimeOffset.FromUnixTimeMilliseconds(unixMs);
            },
            cancellationToken);
    }

    /// <summary>
    /// Asynchronously executes high-performance keyset (cursor-based) pagination on an <see cref="IQueryable{TSource}"/> 
    /// ordered by a distributed <see cref="SnowflakeId"/> key, utilizing the N+1 count elimination technique.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Chronological Keyset:</b> <see cref="SnowflakeId"/> instances are naturally k-sortable (time-ordered) 64-bit identifiers.
    /// Seeking on a Snowflake ID provides optimal B-Tree index traversal performance in distributed databases.
    /// </para>
    /// <para>
    /// <b>Binary Encoding:</b> Cursors are encoded directly from the raw 64-bit integer into an 8-byte big-endian binary payload 
    /// wrapped in a Base64Url string, guaranteeing zero string allocations.
    /// </para>
    /// </remarks>
    /// <typeparam name="TSource">The entity or projected data model type.</typeparam>
    /// <param name="source">The source queryable. Must be explicitly ordered before calling pagination.</param>
    /// <param name="request">The cursor request specifying the seek position, item limit, and direction.</param>
    /// <param name="keySelector">A lambda expression identifying the <see cref="SnowflakeId"/> key property (e.g. <c>x => x.Id</c>).</param>
    /// <param name="cancellationToken">A token to observe while waiting for the database query to complete.</param>
    /// <returns>
    /// A task representing the asynchronous query execution, containing a <see cref="CursorResult{TSource}"/>.
    /// </returns>
    /// <exception cref="PrecaArgumentNullException">Thrown when <paramref name="source"/> or <paramref name="keySelector"/> is <see langword="null"/>.</exception>
    /// <exception cref="FormatException">Thrown when an incoming cursor token does not contain a valid 64-bit integer payload.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the <paramref name="cancellationToken"/> is canceled.</exception>
    /// <example>
    /// <code>
    /// var result = await dbContext.Messages
    ///     .AsNoTracking()
    ///     .OrderBy(m => m.Id)
    ///     .ToCursorResultAsync(request, m => m.Id, cancellationToken);
    /// </code>
    /// </example>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task<CursorResult<TSource>> ToCursorResultAsync<TSource>(
        this IQueryable<TSource> source,
        CursorRequest request,
        Expression<Func<TSource, SnowflakeId>> keySelector,
        CancellationToken cancellationToken = default) {

        Preca.ThrowIfNull(source);
        Preca.ThrowIfNull(keySelector);

        return ToCursorResultAsync(
            source,
            request,
            keySelector,
            cursorEncoder: static snowflake => {
                Span<byte> buffer = stackalloc byte[sizeof(long)];
                BinaryPrimitives.WriteInt64BigEndian(buffer, snowflake.Value);
                return CursorToken.FromBytes(buffer);
            },
            cursorDecoder: static token => {
                Span<byte> buffer = stackalloc byte[sizeof(long)];
                if(!token.TryDecode(buffer, out int bytesWritten) || bytesWritten != sizeof(long)) {
                    throw new FormatException("Invalid SnowflakeId cursor payload.");
                }
                return new SnowflakeId(BinaryPrimitives.ReadInt64BigEndian(buffer));
            },
            cancellationToken);
    }

    #endregion

    #region Keyset (Cursor) Pagination - Custom Codecs

    /// <summary>
    /// Asynchronously executes keyset (cursor-based) pagination on an <see cref="IQueryable{TSource}"/> source 
    /// using caller-supplied custom cursor encoder and decoder delegates.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Use Cases:</b> Use this overload for composite keys, encrypted tokens, or domain-specific identifiers
    /// that require custom serialization into an opaque <see cref="CursorToken"/>.
    /// </para>
    /// <para>
    /// <b>Execution Pipeline:</b>
    /// <list type="number">
    ///   <item><description>If a cursor is provided, decodes the pivot key using <paramref name="cursorDecoder"/> and injects an index-seek <c>WHERE</c> predicate.</description></item>
    ///   <item><description>Queries <c>Take(Limit + 1)</c> items from the database.</description></item>
    ///   <item><description>Evaluates <see cref="CursorMetadata.HasNext"/> based on the count of retrieved records and removes the trailing (+1) record.</description></item>
    ///   <item><description>Encodes the first and last records' keys into <see cref="CursorMetadata.StartCursor"/> and <see cref="CursorMetadata.EndCursor"/> via <paramref name="cursorEncoder"/>.</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    /// <typeparam name="TSource">The entity or projected data model type.</typeparam>
    /// <typeparam name="TKey">The comparable key type used for sorting and seek operations.</typeparam>
    /// <param name="source">The source queryable. Must be ordered consistently with the seek predicate.</param>
    /// <param name="request">The cursor request specifying the seek position, item limit, and direction.</param>
    /// <param name="keySelector">A lambda expression identifying the key property to seek on.</param>
    /// <param name="cursorEncoder">A delegate converting a key instance into an opaque <see cref="CursorToken"/>.</param>
    /// <param name="cursorDecoder">A delegate converting an opaque <see cref="CursorToken"/> back to a strongly-typed key instance.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the database query to complete.</param>
    /// <returns>
    /// A task representing the asynchronous query execution, containing a <see cref="CursorResult{TSource}"/> 
    /// with the items and boundary metadata.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="source"/>, <paramref name="keySelector"/>, <paramref name="cursorEncoder"/>, 
    /// or <paramref name="cursorDecoder"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="OperationCanceledException">Thrown when the <paramref name="cancellationToken"/> is canceled.</exception>
    public static async Task<CursorResult<TSource>> ToCursorResultAsync<TSource, TKey>(
        this IQueryable<TSource> source,
        CursorRequest request,
        Expression<Func<TSource, TKey>> keySelector,
        Func<TKey, CursorToken> cursorEncoder,
        Func<CursorToken, TKey> cursorDecoder,
        CancellationToken cancellationToken = default) where TKey : IComparable<TKey> {

        Preca.ThrowIfNull(source);
        Preca.ThrowIfNull(keySelector);
        Preca.ThrowIfNull(cursorEncoder);
        Preca.ThrowIfNull(cursorDecoder);

        IQueryable<TSource> query = source;
        bool hasPrevious = false;

        // 1. Detect if the incoming query is ordered DESC or ASC
        bool isDescending = IsQueryOrderedDescending(source.Expression);

        // 2. Apply seek predicate & directional ordering matrix
        if(!request.Cursor.IsEmpty) {
            TKey pivotKey = cursorDecoder(request.Cursor);
            hasPrevious = true;

            // Truth Table Matrix:
            // ASC + Forward  => key > pivot
            // ASC + Backward => key < pivot (Order DESC)
            // DESC + Forward => key < pivot
            // DESC + Backward=> key > pivot (Order ASC)
            bool seekGreaterThan = (!isDescending && request.Direction == CursorDirection.Forward) ||
                                   (isDescending && request.Direction == CursorDirection.Backward);

            BinaryExpression comparison = seekGreaterThan
                ? Expression.GreaterThan(keySelector.Body, Expression.Constant(pivotKey, typeof(TKey)))
                : Expression.LessThan(keySelector.Body, Expression.Constant(pivotKey, typeof(TKey)));

            var predicate = Expression.Lambda<Func<TSource, bool>>(comparison, keySelector.Parameters);
            query = query.Where(predicate);

            if(request.Direction == CursorDirection.Backward) {
                // Invert the query order for backward seek
                query = isDescending
                    ? query.OrderBy(keySelector)
                    : query.OrderByDescending(keySelector);
            }
        }

        // 3. N + 1 Technique: Fetch Limit + 1 items
        int fetchLimit = request.Limit + 1;
        List<TSource> rawItems = await query
            .Take(fetchLimit)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if(rawItems.Count == 0) {
            return CursorResult<TSource>.Empty;
        }

        // 4. Detect HasNext and drop the (+1) extra item
        bool hasNext = rawItems.Count > request.Limit;
        if(hasNext) {
            rawItems.RemoveAt(rawItems.Count - 1);
        }

        // 5. If backward navigation occurred, reverse in-memory to restore original sequence
        if(request.Direction == CursorDirection.Backward && !request.Cursor.IsEmpty) {
            rawItems.Reverse();
        }

        // 6. Generate Start and End boundary cursors using cached delegate
        var compiledKeySelector = (Func<TSource, TKey>)CompiledKeySelectorCache.GetOrAdd(
            keySelector,
            static expr => ((Expression<Func<TSource, TKey>>)expr).Compile());

        CursorToken startCursor = cursorEncoder(compiledKeySelector(rawItems[0]));
        CursorToken endCursor = cursorEncoder(compiledKeySelector(rawItems[^1]));

        var metadata = new CursorMetadata(startCursor, endCursor, hasPrevious, hasNext);
        return new CursorResult<TSource>(new EquatableArray<TSource>(rawItems), metadata);
    }

    private static bool IsQueryOrderedDescending(Expression expression) {
        Expression? current = expression;
        while(current is MethodCallExpression methodCall) {
            if(methodCall.Method.DeclaringType == typeof(Queryable)) {
                string name = methodCall.Method.Name;
                if(name == nameof(Queryable.OrderByDescending) || name == nameof(Queryable.ThenByDescending)) {
                    return true;
                }
                if(name == nameof(Queryable.OrderBy) || name == nameof(Queryable.ThenBy)) {
                    return false;
                }
            }
            current = methodCall.Arguments.Count > 0 ? methodCall.Arguments[0] : null;
        }
        return false; // Default: Ascending
    }

    #endregion
}