using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using Wiaoj.Pagination;
using Wiaoj.Preconditions;
using Wiaoj.Preconditions.Exceptions;
using Wiaoj.Primitives.Buffers;
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
    private static readonly ConcurrentDictionary<Type, PropertyInfo?> PrimaryKeyPropertyCache = new();
    private static readonly ConcurrentDictionary<PropertyInfo, LambdaExpression> GetterExpressionCache = new();

    #region Keyset (Cursor) Pagination - Integer Types

    /// <summary>
    /// Asynchronously executes keyset (cursor-based) pagination on an <see cref="IQueryable{TSource}"/> 
    /// ordered by a 64-bit integer (<see cref="long"/>) key, utilizing the N+1 count elimination technique.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Performance:</b> Eliminates the need for expensive <c>COUNT(*)</c> and large <c>OFFSET</c> 
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
    /// Asynchronously executes keyset (cursor-based) pagination on an <see cref="IQueryable{TSource}"/> 
    /// ordered by an unsigned 64-bit integer (<see cref="ulong"/>) key, utilizing the N+1 count elimination technique.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Performance:</b> Requests <c>Limit + 1</c> items from the database to evaluate page boundaries without executing an additional count query.
    /// </para>
    /// <para>
    /// <b>Binary Encoding:</b> Cursors are encoded into an opaque 8-byte big-endian binary payload wrapped in a Base64Url string.
    /// </para>
    /// </remarks>
    /// <typeparam name="TSource">The entity or projected data model type.</typeparam>
    /// <param name="source">The source queryable. Must be explicitly ordered before calling pagination.</param>
    /// <param name="request">The cursor request specifying the seek position, item limit, and direction.</param>
    /// <param name="keySelector">A lambda expression identifying the unsigned 64-bit integer key property (e.g. <c>x => x.Id</c>).</param>
    /// <param name="cancellationToken">A token to observe while waiting for the database query to complete.</param>
    /// <returns>
    /// A task representing the asynchronous query execution, containing a <see cref="CursorResult{TSource}"/> 
    /// with the items window and boundary metadata.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> or <paramref name="keySelector"/> is <see langword="null"/>.</exception>
    /// <exception cref="FormatException">Thrown when an incoming cursor token does not contain a valid unsigned 64-bit integer payload.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the <paramref name="cancellationToken"/> is canceled.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task<CursorResult<TSource>> ToCursorResultAsync<TSource>(
        this IQueryable<TSource> source,
        CursorRequest request,
        Expression<Func<TSource, ulong>> keySelector,
        CancellationToken cancellationToken = default) {

        Preca.ThrowIfNull(source);
        Preca.ThrowIfNull(keySelector);

        return ToCursorResultAsync(
            source,
            request,
            keySelector,
            cursorEncoder: static id => {
                Span<byte> buffer = stackalloc byte[sizeof(ulong)];
                BinaryPrimitives.WriteUInt64BigEndian(buffer, id);
                return CursorToken.FromBytes(buffer);
            },
            cursorDecoder: static token => {
                Span<byte> buffer = stackalloc byte[sizeof(ulong)];
                if(!token.TryDecode(buffer, out int bytesWritten) || bytesWritten != sizeof(ulong)) {
                    throw new FormatException("Invalid unsigned 64-bit integer cursor payload.");
                }
                return BinaryPrimitives.ReadUInt64BigEndian(buffer);
            },
            cancellationToken);
    }

    /// <summary>
    /// Asynchronously executes keyset (cursor-based) pagination on an <see cref="IQueryable{TSource}"/> 
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

        if(TryGetTieBreaker<TSource, long>(keySelector, out Expression<Func<TSource, long>>? tieBreaker)) {
            return ToCursorResultAsync(source, request, keySelector, tieBreaker,
                static (primary, tie) => {
                    Span<byte> buffer = stackalloc byte[sizeof(int) + sizeof(long)];
                    BinaryPrimitives.WriteInt32BigEndian(buffer[..sizeof(int)], primary);
                    BinaryPrimitives.WriteInt64BigEndian(buffer[sizeof(int)..], tie);
                    return CursorToken.FromBytes(buffer);
                },
                static token => {
                    Span<byte> buffer = stackalloc byte[sizeof(int) + sizeof(long)];
                    if(!token.TryDecode(buffer, out int written) || written != (sizeof(int) + sizeof(long))) {
                        throw new FormatException("Invalid composite int + long cursor payload.");
                    }
                    return (BinaryPrimitives.ReadInt32BigEndian(buffer[..sizeof(int)]), BinaryPrimitives.ReadInt64BigEndian(buffer[sizeof(int)..]));
                },
                cancellationToken);
        }

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
    /// Asynchronously executes keyset (cursor-based) pagination on an <see cref="IQueryable{TSource}"/> 
    /// ordered by an unsigned 32-bit integer (<see cref="uint"/>) key, utilizing the N+1 count elimination technique.
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
    /// <param name="keySelector">A lambda expression identifying the unsigned 32-bit integer key property (e.g. <c>x => x.Id</c>).</param>
    /// <param name="cancellationToken">A token to observe while waiting for the database query to complete.</param>
    /// <returns>
    /// A task representing the asynchronous query execution, containing a <see cref="CursorResult{TSource}"/> 
    /// with the items window and boundary metadata.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> or <paramref name="keySelector"/> is <see langword="null"/>.</exception>
    /// <exception cref="FormatException">Thrown when an incoming cursor token does not contain a valid unsigned 32-bit integer payload.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the <paramref name="cancellationToken"/> is canceled.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task<CursorResult<TSource>> ToCursorResultAsync<TSource>(
        this IQueryable<TSource> source,
        CursorRequest request,
        Expression<Func<TSource, uint>> keySelector,
        CancellationToken cancellationToken = default) {

        Preca.ThrowIfNull(source);
        Preca.ThrowIfNull(keySelector);

        return ToCursorResultAsync(
            source,
            request,
            keySelector,
            cursorEncoder: static id => {
                Span<byte> buffer = stackalloc byte[sizeof(uint)];
                BinaryPrimitives.WriteUInt32BigEndian(buffer, id);
                return CursorToken.FromBytes(buffer);
            },
            cursorDecoder: static token => {
                Span<byte> buffer = stackalloc byte[sizeof(uint)];
                if(!token.TryDecode(buffer, out int bytesWritten) || bytesWritten != sizeof(uint)) {
                    throw new FormatException("Invalid unsigned 32-bit integer cursor payload.");
                }
                return BinaryPrimitives.ReadUInt32BigEndian(buffer);
            },
            cancellationToken);
    }

    /// <summary>
    /// Asynchronously executes keyset (cursor-based) pagination on an <see cref="IQueryable{TSource}"/> 
    /// ordered by a 16-bit integer (<see cref="short"/>) key, utilizing the N+1 count elimination technique.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Performance:</b> Requests <c>Limit + 1</c> items from the database to evaluate page boundaries without executing an additional count query.
    /// </para>
    /// <para>
    /// <b>Binary Encoding:</b> Cursors are encoded into an opaque 2-byte big-endian binary payload wrapped in a Base64Url string.
    /// </para>
    /// </remarks>
    /// <typeparam name="TSource">The entity or projected data model type.</typeparam>
    /// <param name="source">The source queryable. Must be explicitly ordered before calling pagination.</param>
    /// <param name="request">The cursor request specifying the seek position, item limit, and direction.</param>
    /// <param name="keySelector">A lambda expression identifying the 16-bit integer key property (e.g. <c>x => x.Id</c>).</param>
    /// <param name="cancellationToken">A token to observe while waiting for the database query to complete.</param>
    /// <returns>
    /// A task representing the asynchronous query execution, containing a <see cref="CursorResult{TSource}"/> 
    /// with the items window and boundary metadata.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> or <paramref name="keySelector"/> is <see langword="null"/>.</exception>
    /// <exception cref="FormatException">Thrown when an incoming cursor token does not contain a valid 16-bit integer payload.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the <paramref name="cancellationToken"/> is canceled.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task<CursorResult<TSource>> ToCursorResultAsync<TSource>(
        this IQueryable<TSource> source,
        CursorRequest request,
        Expression<Func<TSource, short>> keySelector,
        CancellationToken cancellationToken = default) {

        Preca.ThrowIfNull(source);
        Preca.ThrowIfNull(keySelector);

        return ToCursorResultAsync(
            source,
            request,
            keySelector,
            cursorEncoder: static id => {
                Span<byte> buffer = stackalloc byte[sizeof(short)];
                BinaryPrimitives.WriteInt16BigEndian(buffer, id);
                return CursorToken.FromBytes(buffer);
            },
            cursorDecoder: static token => {
                Span<byte> buffer = stackalloc byte[sizeof(short)];
                if(!token.TryDecode(buffer, out int bytesWritten) || bytesWritten != sizeof(short)) {
                    throw new FormatException("Invalid 16-bit integer cursor payload.");
                }
                return BinaryPrimitives.ReadInt16BigEndian(buffer);
            },
            cancellationToken);
    }

    /// <summary>
    /// Asynchronously executes keyset (cursor-based) pagination on an <see cref="IQueryable{TSource}"/> 
    /// ordered by an unsigned 16-bit integer (<see cref="ushort"/>) key, utilizing the N+1 count elimination technique.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Performance:</b> Requests <c>Limit + 1</c> items from the database to evaluate page boundaries without executing an additional count query.
    /// </para>
    /// <para>
    /// <b>Binary Encoding:</b> Cursors are encoded into an opaque 2-byte big-endian binary payload wrapped in a Base64Url string.
    /// </para>
    /// </remarks>
    /// <typeparam name="TSource">The entity or projected data model type.</typeparam>
    /// <param name="source">The source queryable. Must be explicitly ordered before calling pagination.</param>
    /// <param name="request">The cursor request specifying the seek position, item limit, and direction.</param>
    /// <param name="keySelector">A lambda expression identifying the unsigned 16-bit integer key property (e.g. <c>x => x.Id</c>).</param>
    /// <param name="cancellationToken">A token to observe while waiting for the database query to complete.</param>
    /// <returns>
    /// A task representing the asynchronous query execution, containing a <see cref="CursorResult{TSource}"/> 
    /// with the items window and boundary metadata.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> or <paramref name="keySelector"/> is <see langword="null"/>.</exception>
    /// <exception cref="FormatException">Thrown when an incoming cursor token does not contain a valid unsigned 16-bit integer payload.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the <paramref name="cancellationToken"/> is canceled.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task<CursorResult<TSource>> ToCursorResultAsync<TSource>(
        this IQueryable<TSource> source,
        CursorRequest request,
        Expression<Func<TSource, ushort>> keySelector,
        CancellationToken cancellationToken = default) {

        Preca.ThrowIfNull(source);
        Preca.ThrowIfNull(keySelector);

        return ToCursorResultAsync(
            source,
            request,
            keySelector,
            cursorEncoder: static id => {
                Span<byte> buffer = stackalloc byte[sizeof(ushort)];
                BinaryPrimitives.WriteUInt16BigEndian(buffer, id);
                return CursorToken.FromBytes(buffer);
            },
            cursorDecoder: static token => {
                Span<byte> buffer = stackalloc byte[sizeof(ushort)];
                if(!token.TryDecode(buffer, out int bytesWritten) || bytesWritten != sizeof(ushort)) {
                    throw new FormatException("Invalid unsigned 16-bit integer cursor payload.");
                }
                return BinaryPrimitives.ReadUInt16BigEndian(buffer);
            },
            cancellationToken);
    }

    /// <summary>
    /// Asynchronously executes keyset (cursor-based) pagination on an <see cref="IQueryable{TSource}"/> 
    /// ordered by an 8-bit unsigned integer (<see cref="byte"/>) key, utilizing the N+1 count elimination technique.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Performance:</b> Requests <c>Limit + 1</c> items from the database to evaluate page boundaries without executing an additional count query.
    /// </para>
    /// <para>
    /// <b>Binary Encoding:</b> Cursors are encoded into an opaque single-byte binary payload wrapped in a Base64Url string.
    /// </para>
    /// </remarks>
    /// <typeparam name="TSource">The entity or projected data model type.</typeparam>
    /// <param name="source">The source queryable. Must be explicitly ordered before calling pagination.</param>
    /// <param name="request">The cursor request specifying the seek position, item limit, and direction.</param>
    /// <param name="keySelector">A lambda expression identifying the 8-bit unsigned integer key property (e.g. <c>x => x.Id</c>).</param>
    /// <param name="cancellationToken">A token to observe while waiting for the database query to complete.</param>
    /// <returns>
    /// A task representing the asynchronous query execution, containing a <see cref="CursorResult{TSource}"/> 
    /// with the items window and boundary metadata.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> or <paramref name="keySelector"/> is <see langword="null"/>.</exception>
    /// <exception cref="FormatException">Thrown when an incoming cursor token does not contain a valid 8-bit byte payload.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the <paramref name="cancellationToken"/> is canceled.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task<CursorResult<TSource>> ToCursorResultAsync<TSource>(
        this IQueryable<TSource> source,
        CursorRequest request,
        Expression<Func<TSource, byte>> keySelector,
        CancellationToken cancellationToken = default) {

        Preca.ThrowIfNull(source);
        Preca.ThrowIfNull(keySelector);

        return ToCursorResultAsync(
            source,
            request,
            keySelector,
            cursorEncoder: static id => {
                Span<byte> buffer = [id];
                return CursorToken.FromBytes(buffer);
            },
            cursorDecoder: static token => {
                Span<byte> buffer = stackalloc byte[1];
                if(!token.TryDecode(buffer, out int bytesWritten) || bytesWritten != 1) {
                    throw new FormatException("Invalid 8-bit byte cursor payload.");
                }
                return buffer[0];
            },
            cancellationToken);
    }

    /// <summary>
    /// Asynchronously executes keyset (cursor-based) pagination on an <see cref="IQueryable{TSource}"/> 
    /// ordered by an 8-bit signed integer (<see cref="sbyte"/>) key, utilizing the N+1 count elimination technique.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Performance:</b> Requests <c>Limit + 1</c> items from the database to evaluate page boundaries without executing an additional count query.
    /// </para>
    /// <para>
    /// <b>Binary Encoding:</b> Cursors are encoded into an opaque single-byte binary payload wrapped in a Base64Url string.
    /// </para>
    /// </remarks>
    /// <typeparam name="TSource">The entity or projected data model type.</typeparam>
    /// <param name="source">The source queryable. Must be explicitly ordered before calling pagination.</param>
    /// <param name="request">The cursor request specifying the seek position, item limit, and direction.</param>
    /// <param name="keySelector">A lambda expression identifying the 8-bit signed integer key property (e.g. <c>x => x.Id</c>).</param>
    /// <param name="cancellationToken">A token to observe while waiting for the database query to complete.</param>
    /// <returns>
    /// A task representing the asynchronous query execution, containing a <see cref="CursorResult{TSource}"/> 
    /// with the items window and boundary metadata.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> or <paramref name="keySelector"/> is <see langword="null"/>.</exception>
    /// <exception cref="FormatException">Thrown when an incoming cursor token does not contain a valid 8-bit signed integer payload.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the <paramref name="cancellationToken"/> is canceled.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task<CursorResult<TSource>> ToCursorResultAsync<TSource>(
        this IQueryable<TSource> source,
        CursorRequest request,
        Expression<Func<TSource, sbyte>> keySelector,
        CancellationToken cancellationToken = default) {

        Preca.ThrowIfNull(source);
        Preca.ThrowIfNull(keySelector);

        return ToCursorResultAsync(
            source,
            request,
            keySelector,
            cursorEncoder: static id => {
                Span<byte> buffer = [(byte)id];
                return CursorToken.FromBytes(buffer);
            },
            cursorDecoder: static token => {
                Span<byte> buffer = stackalloc byte[1];
                if(!token.TryDecode(buffer, out int bytesWritten) || bytesWritten != 1) {
                    throw new FormatException("Invalid 8-bit signed integer cursor payload.");
                }
                return (sbyte)buffer[0];
            },
            cancellationToken);
    }

    /// <summary>
    /// Asynchronously executes keyset (cursor-based) pagination on an <see cref="IQueryable{TSource}"/> 
    /// ordered by a 128-bit signed integer (<see cref="Int128"/>) key, utilizing the N+1 count elimination technique.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Performance:</b> Requests <c>Limit + 1</c> items from the database to evaluate page boundaries without executing an additional count query.
    /// </para>
    /// <para>
    /// <b>Binary Encoding:</b> Cursors are encoded into an opaque 16-byte big-endian binary payload wrapped in a Base64Url string.
    /// </para>
    /// </remarks>
    /// <typeparam name="TSource">The entity or projected data model type.</typeparam>
    /// <param name="source">The source queryable. Must be explicitly ordered before calling pagination.</param>
    /// <param name="request">The cursor request specifying the seek position, item limit, and direction.</param>
    /// <param name="keySelector">A lambda expression identifying the 128-bit signed integer key property (e.g. <c>x => x.Id</c>).</param>
    /// <param name="cancellationToken">A token to observe while waiting for the database query to complete.</param>
    /// <returns>
    /// A task representing the asynchronous query execution, containing a <see cref="CursorResult{TSource}"/> 
    /// with the items window and boundary metadata.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> or <paramref name="keySelector"/> is <see langword="null"/>.</exception>
    /// <exception cref="FormatException">Thrown when an incoming cursor token does not contain a valid 128-bit signed integer payload.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the <paramref name="cancellationToken"/> is canceled.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task<CursorResult<TSource>> ToCursorResultAsync<TSource>(
        this IQueryable<TSource> source,
        CursorRequest request,
        Expression<Func<TSource, Int128>> keySelector,
        CancellationToken cancellationToken = default) {

        Preca.ThrowIfNull(source);
        Preca.ThrowIfNull(keySelector);

        return ToCursorResultAsync(
            source,
            request,
            keySelector,
            cursorEncoder: static id => {
                Span<byte> buffer = stackalloc byte[16];
                BinaryPrimitives.WriteInt128BigEndian(buffer, id);
                return CursorToken.FromBytes(buffer);
            },
            cursorDecoder: static token => {
                Span<byte> buffer = stackalloc byte[16];
                if(!token.TryDecode(buffer, out int bytesWritten) || bytesWritten != 16) {
                    throw new FormatException("Invalid 128-bit signed integer cursor payload.");
                }
                return BinaryPrimitives.ReadInt128BigEndian(buffer);
            },
            cancellationToken);
    }

    /// <summary>
    /// Asynchronously executes keyset (cursor-based) pagination on an <see cref="IQueryable{TSource}"/> 
    /// ordered by a 128-bit unsigned integer (<see cref="UInt128"/>) key, utilizing the N+1 count elimination technique.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Performance:</b> Requests <c>Limit + 1</c> items from the database to evaluate page boundaries without executing an additional count query.
    /// </para>
    /// <para>
    /// <b>Binary Encoding:</b> Cursors are encoded into an opaque 16-byte big-endian binary payload wrapped in a Base64Url string.
    /// </para>
    /// </remarks>
    /// <typeparam name="TSource">The entity or projected data model type.</typeparam>
    /// <param name="source">The source queryable. Must be explicitly ordered before calling pagination.</param>
    /// <param name="request">The cursor request specifying the seek position, item limit, and direction.</param>
    /// <param name="keySelector">A lambda expression identifying the 128-bit unsigned integer key property (e.g. <c>x => x.Id</c>).</param>
    /// <param name="cancellationToken">A token to observe while waiting for the database query to complete.</param>
    /// <returns>
    /// A task representing the asynchronous query execution, containing a <see cref="CursorResult{TSource}"/> 
    /// with the items window and boundary metadata.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> or <paramref name="keySelector"/> is <see langword="null"/>.</exception>
    /// <exception cref="FormatException">Thrown when an incoming cursor token does not contain a valid 128-bit unsigned integer payload.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the <paramref name="cancellationToken"/> is canceled.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task<CursorResult<TSource>> ToCursorResultAsync<TSource>(
        this IQueryable<TSource> source,
        CursorRequest request,
        Expression<Func<TSource, UInt128>> keySelector,
        CancellationToken cancellationToken = default) {

        Preca.ThrowIfNull(source);
        Preca.ThrowIfNull(keySelector);

        return ToCursorResultAsync(
            source,
            request,
            keySelector,
            cursorEncoder: static id => {
                Span<byte> buffer = stackalloc byte[16];
                BinaryPrimitives.WriteUInt128BigEndian(buffer, id);
                return CursorToken.FromBytes(buffer);
            },
            cursorDecoder: static token => {
                Span<byte> buffer = stackalloc byte[16];
                if(!token.TryDecode(buffer, out int bytesWritten) || bytesWritten != 16) {
                    throw new FormatException("Invalid 128-bit unsigned integer cursor payload.");
                }
                return BinaryPrimitives.ReadUInt128BigEndian(buffer);
            },
            cancellationToken);
    }

    #endregion

    #region Keyset (Cursor) Pagination - Floating-Point & Decimal Types

    /// <summary>
    /// Asynchronously executes keyset (cursor-based) pagination on an <see cref="IQueryable{TSource}"/> 
    /// ordered by a 64-bit double-precision floating point (<see cref="double"/>) key, utilizing the N+1 count elimination technique.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Performance:</b> Requests <c>Limit + 1</c> items from the database to evaluate page boundaries without executing an additional count query.
    /// </para>
    /// <para>
    /// <b>Binary Encoding:</b> Cursors are encoded into an opaque 8-byte big-endian binary payload wrapped in a Base64Url string.
    /// </para>
    /// </remarks>
    /// <typeparam name="TSource">The entity or projected data model type.</typeparam>
    /// <param name="source">The source queryable. Must be explicitly ordered before calling pagination.</param>
    /// <param name="request">The cursor request specifying the seek position, item limit, and direction.</param>
    /// <param name="keySelector">A lambda expression identifying the 64-bit floating-point key property (e.g. <c>x => x.Score</c>).</param>
    /// <param name="cancellationToken">A token to observe while waiting for the database query to complete.</param>
    /// <returns>
    /// A task representing the asynchronous query execution, containing a <see cref="CursorResult{TSource}"/> 
    /// with the items window and boundary metadata.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> or <paramref name="keySelector"/> is <see langword="null"/>.</exception>
    /// <exception cref="FormatException">Thrown when an incoming cursor token does not contain a valid 64-bit double payload.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the <paramref name="cancellationToken"/> is canceled.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task<CursorResult<TSource>> ToCursorResultAsync<TSource>(
        this IQueryable<TSource> source,
        CursorRequest request,
        Expression<Func<TSource, double>> keySelector,
        CancellationToken cancellationToken = default) {

        Preca.ThrowIfNull(source);
        Preca.ThrowIfNull(keySelector);

        return ToCursorResultAsync(
            source,
            request,
            keySelector,
            cursorEncoder: static val => {
                Span<byte> buffer = stackalloc byte[sizeof(double)];
                BinaryPrimitives.WriteDoubleBigEndian(buffer, val);
                return CursorToken.FromBytes(buffer);
            },
            cursorDecoder: static token => {
                Span<byte> buffer = stackalloc byte[sizeof(double)];
                if(!token.TryDecode(buffer, out int bytesWritten) || bytesWritten != sizeof(double)) {
                    throw new FormatException("Invalid 64-bit double cursor payload.");
                }
                return BinaryPrimitives.ReadDoubleBigEndian(buffer);
            },
            cancellationToken);
    }

    /// <summary>
    /// Asynchronously executes keyset (cursor-based) pagination on an <see cref="IQueryable{TSource}"/> 
    /// ordered by a 32-bit single-precision floating point (<see cref="float"/>) key, utilizing the N+1 count elimination technique.
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
    /// <param name="keySelector">A lambda expression identifying the 32-bit floating-point key property (e.g. <c>x => x.Score</c>).</param>
    /// <param name="cancellationToken">A token to observe while waiting for the database query to complete.</param>
    /// <returns>
    /// A task representing the asynchronous query execution, containing a <see cref="CursorResult{TSource}"/> 
    /// with the items window and boundary metadata.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> or <paramref name="keySelector"/> is <see langword="null"/>.</exception>
    /// <exception cref="FormatException">Thrown when an incoming cursor token does not contain a valid 32-bit float payload.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the <paramref name="cancellationToken"/> is canceled.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task<CursorResult<TSource>> ToCursorResultAsync<TSource>(
        this IQueryable<TSource> source,
        CursorRequest request,
        Expression<Func<TSource, float>> keySelector,
        CancellationToken cancellationToken = default) {

        Preca.ThrowIfNull(source);
        Preca.ThrowIfNull(keySelector);

        return ToCursorResultAsync(
            source,
            request,
            keySelector,
            cursorEncoder: static val => {
                Span<byte> buffer = stackalloc byte[sizeof(float)];
                BinaryPrimitives.WriteSingleBigEndian(buffer, val);
                return CursorToken.FromBytes(buffer);
            },
            cursorDecoder: static token => {
                Span<byte> buffer = stackalloc byte[sizeof(float)];
                if(!token.TryDecode(buffer, out int bytesWritten) || bytesWritten != sizeof(float)) {
                    throw new FormatException("Invalid 32-bit float cursor payload.");
                }
                return BinaryPrimitives.ReadSingleBigEndian(buffer);
            },
            cancellationToken);
    }

    /// <summary>
    /// Asynchronously executes keyset (cursor-based) pagination on an <see cref="IQueryable{TSource}"/> 
    /// ordered by a 16-bit half-precision floating point (<see cref="Half"/>) key, utilizing the N+1 count elimination technique.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Performance:</b> Requests <c>Limit + 1</c> items from the database to evaluate page boundaries without executing an additional count query.
    /// </para>
    /// <para>
    /// <b>Binary Encoding:</b> Cursors are encoded into an opaque 2-byte big-endian binary payload wrapped in a Base64Url string.
    /// </para>
    /// </remarks>
    /// <typeparam name="TSource">The entity or projected data model type.</typeparam>
    /// <param name="source">The source queryable. Must be explicitly ordered before calling pagination.</param>
    /// <param name="request">The cursor request specifying the seek position, item limit, and direction.</param>
    /// <param name="keySelector">A lambda expression identifying the 16-bit half-precision floating-point key property.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the database query to complete.</param>
    /// <returns>
    /// A task representing the asynchronous query execution, containing a <see cref="CursorResult{TSource}"/> 
    /// with the items window and boundary metadata.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> or <paramref name="keySelector"/> is <see langword="null"/>.</exception>
    /// <exception cref="FormatException">Thrown when an incoming cursor token does not contain a valid 16-bit half payload.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the <paramref name="cancellationToken"/> is canceled.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task<CursorResult<TSource>> ToCursorResultAsync<TSource>(
        this IQueryable<TSource> source,
        CursorRequest request,
        Expression<Func<TSource, Half>> keySelector,
        CancellationToken cancellationToken = default) {

        Preca.ThrowIfNull(source);
        Preca.ThrowIfNull(keySelector);

        return ToCursorResultAsync(
            source,
            request,
            keySelector,
            cursorEncoder: static val => {
                Span<byte> buffer = stackalloc byte[2];
                BinaryPrimitives.WriteHalfBigEndian(buffer, val);
                return CursorToken.FromBytes(buffer);
            },
            cursorDecoder: static token => {
                Span<byte> buffer = stackalloc byte[2];
                if(!token.TryDecode(buffer, out int bytesWritten) || bytesWritten != 2) {
                    throw new FormatException("Invalid 16-bit half cursor payload.");
                }
                return BinaryPrimitives.ReadHalfBigEndian(buffer);
            },
            cancellationToken);
    }

    /// <summary>
    /// Asynchronously executes keyset (cursor-based) pagination on an <see cref="IQueryable{TSource}"/> 
    /// ordered by a 128-bit decimal (<see cref="decimal"/>) monetary or numeric key, utilizing the N+1 count elimination technique.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Binary Precision:</b> Deconstructs the 128-bit decimal into four 32-bit integer constituent bits, 
    /// encoding them as big-endian binary integers to preserve exact decimal precision without floating-point rounding errors.
    /// </para>
    /// </remarks>
    /// <typeparam name="TSource">The entity or projected data model type.</typeparam>
    /// <param name="source">The source queryable. Must be explicitly ordered before calling pagination.</param>
    /// <param name="request">The cursor request specifying the seek position, item limit, and direction.</param>
    /// <param name="keySelector">A lambda expression identifying the 128-bit decimal key property (e.g. <c>x => x.Price</c>).</param>
    /// <param name="cancellationToken">A token to observe while waiting for the database query to complete.</param>
    /// <returns>
    /// A task representing the asynchronous query execution, containing a <see cref="CursorResult{TSource}"/> 
    /// with the items window and boundary metadata.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> or <paramref name="keySelector"/> is <see langword="null"/>.</exception>
    /// <exception cref="FormatException">Thrown when an incoming cursor token does not contain a valid 16-byte decimal payload.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the <paramref name="cancellationToken"/> is canceled.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task<CursorResult<TSource>> ToCursorResultAsync<TSource>(
        this IQueryable<TSource> source,
        CursorRequest request,
        Expression<Func<TSource, decimal>> keySelector,
        CancellationToken cancellationToken = default) {

        Preca.ThrowIfNull(source);
        Preca.ThrowIfNull(keySelector);

        if(TryGetTieBreaker<TSource, long>(keySelector, out Expression<Func<TSource, long>>? tieBreaker)) {
            return ToCursorResultAsync(source, request, keySelector, tieBreaker, cancellationToken);
        }

        return ToCursorResultAsync(
            source,
            request,
            keySelector,
            cursorEncoder: static dec => {
                Span<byte> buffer = stackalloc byte[16];
                Span<int> bits = stackalloc int[4];
                decimal.TryGetBits(dec, bits, out _);
                for(int i = 0; i < 4; i++) {
                    BinaryPrimitives.WriteInt32BigEndian(buffer.Slice(i * 4, 4), bits[i]);
                }
                return CursorToken.FromBytes(buffer);
            },
            cursorDecoder: static token => {
                Span<byte> buffer = stackalloc byte[16];
                if(!token.TryDecode(buffer, out int bytesWritten) || bytesWritten != 16) {
                    throw new FormatException("Invalid 128-bit decimal cursor payload.");
                }
                Span<int> bits = stackalloc int[4];
                for(int i = 0; i < 4; i++) {
                    bits[i] = BinaryPrimitives.ReadInt32BigEndian(buffer.Slice(i * 4, 4));
                }
                return new decimal(bits);
            },
            cancellationToken);
    }

    #endregion

    #region Keyset (Cursor) Pagination - Date & Time Types

    /// <summary>
    /// Asynchronously executes keyset (cursor-based) pagination on an <see cref="IQueryable{TSource}"/> 
    /// ordered by a <see cref="DateTime"/> timestamp key, utilizing the N+1 count elimination technique.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Chronological Precision:</b> Encodes the 64-bit integer tick count (<see cref="DateTime.Ticks"/>) in big-endian format, 
    /// preserving 100-nanosecond resolution without string formatting overhead.
    /// </para>
    /// </remarks>
    /// <typeparam name="TSource">The entity or projected data model type.</typeparam>
    /// <param name="source">The source queryable. Must be explicitly ordered before calling pagination.</param>
    /// <param name="request">The cursor request specifying the seek position, item limit, and direction.</param>
    /// <param name="keySelector">A lambda expression identifying the <see cref="DateTime"/> timestamp property (e.g. <c>x => x.Timestamp</c>).</param>
    /// <param name="cancellationToken">A token to observe while waiting for the database query to complete.</param>
    /// <returns>
    /// A task representing the asynchronous query execution, containing a <see cref="CursorResult{TSource}"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> or <paramref name="keySelector"/> is <see langword="null"/>.</exception>
    /// <exception cref="FormatException">Thrown when an incoming cursor token does not contain a valid 64-bit tick payload.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the <paramref name="cancellationToken"/> is canceled.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task<CursorResult<TSource>> ToCursorResultAsync<TSource>(
        this IQueryable<TSource> source,
        CursorRequest request,
        Expression<Func<TSource, DateTime>> keySelector,
        CancellationToken cancellationToken = default) {

        Preca.ThrowIfNull(source);
        Preca.ThrowIfNull(keySelector);

        if(TryGetTieBreaker<TSource, long>(keySelector, out Expression<Func<TSource, long>>? tieBreaker)) {
            return ToCursorResultAsync(source, request, keySelector, tieBreaker,
                static (primary, tie) => {
                    Span<byte> buffer = stackalloc byte[sizeof(long) + sizeof(long)];
                    BinaryPrimitives.WriteInt64BigEndian(buffer[..sizeof(long)], primary.Ticks);
                    BinaryPrimitives.WriteInt64BigEndian(buffer[sizeof(long)..], tie);
                    return CursorToken.FromBytes(buffer);
                },
                static token => {
                    Span<byte> buffer = stackalloc byte[sizeof(long) + sizeof(long)];
                    if(!token.TryDecode(buffer, out int written) || written != (sizeof(long) + sizeof(long))) {
                        throw new FormatException("Invalid composite DateTime + long cursor payload.");
                    }
                    long ticks = BinaryPrimitives.ReadInt64BigEndian(buffer[..sizeof(long)]);
                    long tie = BinaryPrimitives.ReadInt64BigEndian(buffer[sizeof(long)..]);
                    return (new DateTime(ticks, DateTimeKind.Utc), tie);
                },
                cancellationToken);
        }

        return ToCursorResultAsync(
            source,
            request,
            keySelector,
            cursorEncoder: static dt => {
                Span<byte> buffer = stackalloc byte[sizeof(long)];
                BinaryPrimitives.WriteInt64BigEndian(buffer, dt.Ticks);
                return CursorToken.FromBytes(buffer);
            },
            cursorDecoder: static token => {
                Span<byte> buffer = stackalloc byte[sizeof(long)];
                if(!token.TryDecode(buffer, out int bytesWritten) || bytesWritten != sizeof(long)) {
                    throw new FormatException("Invalid DateTime cursor payload.");
                }
                long ticks = BinaryPrimitives.ReadInt64BigEndian(buffer);
                return new DateTime(ticks, DateTimeKind.Utc);
            },
            cancellationToken);
    }

    /// <summary>
    /// Asynchronously executes keyset (cursor-based) pagination on an <see cref="IQueryable{TSource}"/> 
    /// ordered by a <see cref="DateOnly"/> calendar date key, utilizing the N+1 count elimination technique.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Binary Encoding:</b> Encodes the 32-bit day number integer (<see cref="DateOnly.DayNumber"/>) in big-endian format.
    /// </para>
    /// </remarks>
    /// <typeparam name="TSource">The entity or projected data model type.</typeparam>
    /// <param name="source">The source queryable. Must be explicitly ordered before calling pagination.</param>
    /// <param name="request">The cursor request specifying the seek position, item limit, and direction.</param>
    /// <param name="keySelector">A lambda expression identifying the <see cref="DateOnly"/> calendar date property (e.g. <c>x => x.BirthDate</c>).</param>
    /// <param name="cancellationToken">A token to observe while waiting for the database query to complete.</param>
    /// <returns>
    /// A task representing the asynchronous query execution, containing a <see cref="CursorResult{TSource}"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> or <paramref name="keySelector"/> is <see langword="null"/>.</exception>
    /// <exception cref="FormatException">Thrown when an incoming cursor token does not contain a valid 32-bit day number payload.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the <paramref name="cancellationToken"/> is canceled.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task<CursorResult<TSource>> ToCursorResultAsync<TSource>(
        this IQueryable<TSource> source,
        CursorRequest request,
        Expression<Func<TSource, DateOnly>> keySelector,
        CancellationToken cancellationToken = default) {

        Preca.ThrowIfNull(source);
        Preca.ThrowIfNull(keySelector);

        return ToCursorResultAsync(
            source,
            request,
            keySelector,
            cursorEncoder: static d => {
                Span<byte> buffer = stackalloc byte[sizeof(int)];
                BinaryPrimitives.WriteInt32BigEndian(buffer, d.DayNumber);
                return CursorToken.FromBytes(buffer);
            },
            cursorDecoder: static token => {
                Span<byte> buffer = stackalloc byte[sizeof(int)];
                if(!token.TryDecode(buffer, out int bytesWritten) || bytesWritten != sizeof(int)) {
                    throw new FormatException("Invalid DateOnly cursor payload.");
                }
                return DateOnly.FromDayNumber(BinaryPrimitives.ReadInt32BigEndian(buffer));
            },
            cancellationToken);
    }

    /// <summary>
    /// Asynchronously executes keyset (cursor-based) pagination on an <see cref="IQueryable{TSource}"/> 
    /// ordered by a <see cref="TimeOnly"/> clock time key, utilizing the N+1 count elimination technique.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Binary Encoding:</b> Encodes the 64-bit integer tick count (<see cref="TimeOnly.Ticks"/>) in big-endian format.
    /// </para>
    /// </remarks>
    /// <typeparam name="TSource">The entity or projected data model type.</typeparam>
    /// <param name="source">The source queryable. Must be explicitly ordered before calling pagination.</param>
    /// <param name="request">The cursor request specifying the seek position, item limit, and direction.</param>
    /// <param name="keySelector">A lambda expression identifying the <see cref="TimeOnly"/> time-of-day property.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the database query to complete.</param>
    /// <returns>
    /// A task representing the asynchronous query execution, containing a <see cref="CursorResult{TSource}"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> or <paramref name="keySelector"/> is <see langword="null"/>.</exception>
    /// <exception cref="FormatException">Thrown when an incoming cursor token does not contain a valid 64-bit tick payload.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the <paramref name="cancellationToken"/> is canceled.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task<CursorResult<TSource>> ToCursorResultAsync<TSource>(
        this IQueryable<TSource> source,
        CursorRequest request,
        Expression<Func<TSource, TimeOnly>> keySelector,
        CancellationToken cancellationToken = default) {

        Preca.ThrowIfNull(source);
        Preca.ThrowIfNull(keySelector);

        return ToCursorResultAsync(
            source,
            request,
            keySelector,
            cursorEncoder: static t => {
                Span<byte> buffer = stackalloc byte[sizeof(long)];
                BinaryPrimitives.WriteInt64BigEndian(buffer, t.Ticks);
                return CursorToken.FromBytes(buffer);
            },
            cursorDecoder: static token => {
                Span<byte> buffer = stackalloc byte[sizeof(long)];
                if(!token.TryDecode(buffer, out int bytesWritten) || bytesWritten != sizeof(long)) {
                    throw new FormatException("Invalid TimeOnly cursor payload.");
                }
                return new TimeOnly(BinaryPrimitives.ReadInt64BigEndian(buffer));
            },
            cancellationToken);
    }

    /// <summary>
    /// Asynchronously executes keyset (cursor-based) pagination on an <see cref="IQueryable{TSource}"/> 
    /// ordered by a <see cref="TimeSpan"/> duration or interval key, utilizing the N+1 count elimination technique.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Binary Encoding:</b> Encodes the 64-bit integer tick count (<see cref="TimeSpan.Ticks"/>) in big-endian format.
    /// </para>
    /// </remarks>
    /// <typeparam name="TSource">The entity or projected data model type.</typeparam>
    /// <param name="source">The source queryable. Must be explicitly ordered before calling pagination.</param>
    /// <param name="request">The cursor request specifying the seek position, item limit, and direction.</param>
    /// <param name="keySelector">A lambda expression identifying the <see cref="TimeSpan"/> interval property.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the database query to complete.</param>
    /// <returns>
    /// A task representing the asynchronous query execution, containing a <see cref="CursorResult{TSource}"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> or <paramref name="keySelector"/> is <see langword="null"/>.</exception>
    /// <exception cref="FormatException">Thrown when an incoming cursor token does not contain a valid 64-bit tick payload.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the <paramref name="cancellationToken"/> is canceled.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task<CursorResult<TSource>> ToCursorResultAsync<TSource>(
        this IQueryable<TSource> source,
        CursorRequest request,
        Expression<Func<TSource, TimeSpan>> keySelector,
        CancellationToken cancellationToken = default) {

        Preca.ThrowIfNull(source);
        Preca.ThrowIfNull(keySelector);

        return ToCursorResultAsync(
            source,
            request,
            keySelector,
            cursorEncoder: static ts => {
                Span<byte> buffer = stackalloc byte[sizeof(long)];
                BinaryPrimitives.WriteInt64BigEndian(buffer, ts.Ticks);
                return CursorToken.FromBytes(buffer);
            },
            cursorDecoder: static token => {
                Span<byte> buffer = stackalloc byte[sizeof(long)];
                if(!token.TryDecode(buffer, out int bytesWritten) || bytesWritten != sizeof(long)) {
                    throw new FormatException("Invalid TimeSpan cursor payload.");
                }
                return new TimeSpan(BinaryPrimitives.ReadInt64BigEndian(buffer));
            },
            cancellationToken);
    }

    #endregion

    #region Keyset (Cursor) Pagination - Specialized, Text & Identifier Types

    /// <summary>
    /// Asynchronously executes keyset (cursor-based) pagination on an <see cref="IQueryable{TSource}"/> 
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
    /// <param name="keySelector">A lambda expression identifying the <see cref="DateTimeOffset"/> timestamp property (e.g. <c>x => x.Timestamp</c>).</param>
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

        if(TryGetTieBreaker<TSource, long>(keySelector, out Expression<Func<TSource, long>>? tieBreaker)) {
            return ToCursorResultAsync(source, request, keySelector, tieBreaker, cancellationToken);
        }

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
    /// Asynchronously executes keyset (cursor-based) pagination on an <see cref="IQueryable{TSource}"/> 
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
    /// Asynchronously executes keyset (cursor-based) pagination on an <see cref="IQueryable{TSource}"/> 
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

    /// <summary>
    /// Asynchronously executes keyset (cursor-based) pagination on an <see cref="IQueryable{TSource}"/> 
    /// ordered by a UTF-16 character (<see cref="char"/>) key, utilizing the N+1 count elimination technique.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Binary Encoding:</b> Cursors are encoded into an opaque 2-byte big-endian binary payload wrapped in a Base64Url string.
    /// </para>
    /// </remarks>
    /// <typeparam name="TSource">The entity or projected data model type.</typeparam>
    /// <param name="source">The source queryable. Must be explicitly ordered before calling pagination.</param>
    /// <param name="request">The cursor request specifying the seek position, item limit, and direction.</param>
    /// <param name="keySelector">A lambda expression identifying the character key property (e.g. <c>x => x.Code</c>).</param>
    /// <param name="cancellationToken">A token to observe while waiting for the database query to complete.</param>
    /// <returns>
    /// A task representing the asynchronous query execution, containing a <see cref="CursorResult{TSource}"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> or <paramref name="keySelector"/> is <see langword="null"/>.</exception>
    /// <exception cref="FormatException">Thrown when an incoming cursor token does not contain a valid 2-byte char payload.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the <paramref name="cancellationToken"/> is canceled.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task<CursorResult<TSource>> ToCursorResultAsync<TSource>(
        this IQueryable<TSource> source,
        CursorRequest request,
        Expression<Func<TSource, char>> keySelector,
        CancellationToken cancellationToken = default) {

        Preca.ThrowIfNull(source);
        Preca.ThrowIfNull(keySelector);

        return ToCursorResultAsync(
            source,
            request,
            keySelector,
            cursorEncoder: static ch => {
                Span<byte> buffer = stackalloc byte[sizeof(ushort)];
                BinaryPrimitives.WriteUInt16BigEndian(buffer, ch);
                return CursorToken.FromBytes(buffer);
            },
            cursorDecoder: static token => {
                Span<byte> buffer = stackalloc byte[sizeof(ushort)];
                if(!token.TryDecode(buffer, out int bytesWritten) || bytesWritten != sizeof(ushort)) {
                    throw new FormatException("Invalid char cursor payload.");
                }
                return (char)BinaryPrimitives.ReadUInt16BigEndian(buffer);
            },
            cancellationToken);
    }

    /// <summary>
    /// Asynchronously executes keyset (cursor-based) pagination on an <see cref="IQueryable{TSource}"/> 
    /// ordered by a text or string (<see cref="string"/>) key, utilizing the N+1 count elimination technique.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Encoding:</b> Cursors are encoded into an opaque UTF-8 byte sequence wrapped in a Base64Url string.
    /// </para>
    /// </remarks>
    /// <typeparam name="TSource">The entity or projected data model type.</typeparam>
    /// <param name="source">The source queryable. Must be explicitly ordered before calling pagination.</param>
    /// <param name="request">The cursor request specifying the seek position, item limit, and direction.</param>
    /// <param name="keySelector">A lambda expression identifying the string key property (e.g. <c>x => x.Slug</c>).</param>
    /// <param name="cancellationToken">A token to observe while waiting for the database query to complete.</param>
    /// <returns>
    /// A task representing the asynchronous query execution, containing a <see cref="CursorResult{TSource}"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> or <paramref name="keySelector"/> is <see langword="null"/>.</exception>
    /// <exception cref="FormatException">Thrown when an incoming cursor token is not valid UTF-8 text.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the <paramref name="cancellationToken"/> is canceled.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task<CursorResult<TSource>> ToCursorResultAsync<TSource>(
        this IQueryable<TSource> source,
        CursorRequest request,
        Expression<Func<TSource, string>> keySelector,
        CancellationToken cancellationToken = default) {

        Preca.ThrowIfNull(source);
        Preca.ThrowIfNull(keySelector);

        if(TryGetTieBreaker<TSource, long>(keySelector, out Expression<Func<TSource, long>>? tieBreaker)) {
            return ToCursorResultAsync(source, request, keySelector, tieBreaker,
                static (primary, tie) => {
                    string s = primary ?? string.Empty;
                    int strByteCount = Encoding.UTF8.GetByteCount(s);
                    using ValueBuffer<byte> buffer = new(strByteCount + sizeof(long), stackalloc byte[256]);
                    Encoding.UTF8.GetBytes(s, buffer.Span[..strByteCount]);
                    BinaryPrimitives.WriteInt64BigEndian(buffer.Span[strByteCount..], tie);
                    return CursorToken.FromBytes(buffer.Span);
                },
                static token => {
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

        return ToCursorResultAsync(
            source,
            request,
            keySelector,
            cursorEncoder: static str => CursorToken.FromUtf8(str ?? string.Empty),
            cursorDecoder: static token => {
                if(token.IsEmpty) {
                    return string.Empty;
                }

                using ValueBuffer<byte> utf8Buffer = new(token.Length, stackalloc byte[512]);
                if(!token.TryDecode(utf8Buffer.Span, out int bytesWritten)) {
                    throw new FormatException("Invalid string cursor payload.");
                }
                return Encoding.UTF8.GetString(utf8Buffer.Span[..bytesWritten]);
            },
            cancellationToken);
    }

    #endregion

    #region Keyset (Cursor) Pagination - Custom Codecs

    /// <summary>
    /// Formats a <typeparamref name="TKey"/> value into a UTF-8 byte buffer, growing the buffer
    /// and retrying if the initial (stack-friendly) size is insufficient. <see cref="IUtf8SpanFormattable"/>
    /// does not expose the required length ahead of time, so a bounded grow-and-retry loop is used
    /// instead of guessing a single fixed size.
    /// </summary>
    /// <param name="key">The value to format.</param>
    /// <param name="initialBuffer">The stack-allocated buffer to try first (no heap allocation on success).</param>
    /// <param name="rented">
    /// When a pooled fallback buffer was used, the rented array (caller must return it via <see cref="ArrayPool{T}.Return"/>);
    /// otherwise <see langword="null"/> and <paramref name="initialBuffer"/> holds the formatted bytes.
    /// </param>
    /// <returns>The number of bytes written.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the formatted length exceeds the 8192-byte bound.</exception>
    private static int FormatKeyUtf8<TKey>(TKey key, Span<byte> initialBuffer, out byte[]? rented)
        where TKey : IUtf8SpanFormattable {

        if(key.TryFormat(initialBuffer, out int written, default, null)) {
            rented = null;
            return written;
        }

        // Initial 256-byte stack buffer was insufficient (extremely rare for scalar/struct keys).
        // Fall back to a pooled buffer, doubling until a documented bound. This avoids an unbounded
        // retry loop while comfortably covering any realistic struct-based identifier.
        for(int size = 1024; size <= 8192; size *= 2) {
            byte[] buffer = ArrayPool<byte>.Shared.Rent(size);
            if(key.TryFormat(buffer, out written, default, null)) {
                rented = buffer;
                return written;
            }
            ArrayPool<byte>.Shared.Return(buffer);
        }

        throw new InvalidOperationException(
            $"Failed to format key of type '{typeof(TKey).Name}' into a UTF-8 cursor payload: formatted length exceeds the 8192-byte bound.");
    }

    /// <summary>
    /// Asynchronously executes keyset (cursor-based) pagination on an <see cref="IQueryable{TSource}"/> 
    /// ordered by any custom value type or strongly-typed identifier implementing <see cref="IUtf8SpanFormattable"/> and <see cref="IUtf8SpanParsable{TSelf}"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Codecs:</b> Automatically formats and parses cursor payloads directly in UTF-8 binary format
    /// using standard .NET span interfaces, eliminating intermediate string and byte array allocations.
    /// </para>
    /// <para>
    /// <b>Domain Value Objects:</b> Ideal for strongly-typed domain identifiers (e.g. <c>Ulid</c>, custom order IDs, or composite value records)
    /// without requiring caller-provided manual encoder and decoder delegates.
    /// </para>
    /// </remarks>
    /// <typeparam name="TSource">The entity or projected data model type.</typeparam>
    /// <typeparam name="TKey">The comparable key type implementing <see cref="IUtf8SpanFormattable"/> and <see cref="IUtf8SpanParsable{TSelf}"/>.</typeparam>
    /// <param name="source">The source queryable. Must be explicitly ordered before calling pagination.</param>
    /// <param name="request">The cursor request specifying the seek position, item limit, and direction.</param>
    /// <param name="keySelector">A lambda expression identifying the custom key property.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the database query to complete.</param>
    /// <returns>
    /// A task representing the asynchronous query execution, containing a <see cref="CursorResult{TSource}"/> 
    /// with the items window and boundary metadata.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> or <paramref name="keySelector"/> is <see langword="null"/>.</exception>
    /// <exception cref="FormatException">Thrown when an incoming cursor token cannot be parsed into <typeparamref name="TKey"/>.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the <paramref name="cancellationToken"/> is canceled.</exception>
    /// <example>
    /// <code>
    /// var result = await dbContext.Invoices
    ///     .AsNoTracking()
    ///     .OrderBy(i => i.CustomInvoiceId)
    ///     .ToCursorResultAsync(request, i => i.CustomInvoiceId, cancellationToken);
    /// </code>
    /// </example>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task<CursorResult<TSource>> ToCursorResultAsync<TSource, TKey>(
        this IQueryable<TSource> source,
        CursorRequest request,
        Expression<Func<TSource, TKey>> keySelector,
        CancellationToken cancellationToken = default)
        where TKey : struct, IUtf8SpanFormattable, IUtf8SpanParsable<TKey>, IComparable<TKey> {

        Preca.ThrowIfNull(source);
        Preca.ThrowIfNull(keySelector);

        // Diğer tip-spesifik overload'larla (int, DateTime, decimal vb.) tutarlılık için:
        // seçilen key zaten primary key değilse, otomatik olarak Id/PK'yı composite tie-breaker
        // olarak enjekte et. Bu, aynı key değerine sahip birden fazla kayıt olduğunda
        // deterministik olmayan sıralama / sessiz kayıt atlama riskini ortadan kaldırır.
        if(TryGetTieBreaker<TSource, long>(keySelector, out Expression<Func<TSource, long>>? tieBreakerLong)) {
            return ToCursorResultAsync(source, request, keySelector, tieBreakerLong,
                cursorEncoder: static (key, tie) => {
                    // Layout: [4-byte BE uzunluk][TKey UTF-8 payload][8-byte BE tie-breaker]
                    // Key, kendi buffer'ına formatlanır; frame ayrı olarak o buffer'dan doğru
                    // offsetlerle kurulur - offset aritmetiğini tek bir buffer üzerinde
                    // karıştırmak (format hedefiyle okuma kaynağının aynı olmaması) hataya açıktı.
                    Span<byte> keyStackBuffer = stackalloc byte[256];
                    int keyLength = FormatKeyUtf8(key, keyStackBuffer, out byte[]? rented);
                    try {
                        ReadOnlySpan<byte> keyBytes = rented is null
                            ? keyStackBuffer[..keyLength]
                            : rented.AsSpan(0, keyLength);

                        byte[] frame = new byte[4 + keyLength + 8];
                        BinaryPrimitives.WriteInt32BigEndian(frame.AsSpan(0, 4), keyLength);
                        keyBytes.CopyTo(frame.AsSpan(4, keyLength));
                        BinaryPrimitives.WriteInt64BigEndian(frame.AsSpan(4 + keyLength, 8), tie);
                        return CursorToken.FromBytes(frame);
                    }
                    finally {
                        if(rented is not null) {
                            ArrayPool<byte>.Shared.Return(rented);
                        }
                    }
                },
                cursorDecoder: static token => {
                    if(token.IsEmpty) {
                        throw new FormatException($"Cursor token payload is empty for key type '{typeof(TKey).Name}'.");
                    }
                    using ValueBuffer<byte> buffer = new(token.Length, stackalloc byte[256]);
                    if(!token.TryDecode(buffer.Span, out int totalWritten) || totalWritten < 12) {
                        throw new FormatException($"Invalid composite cursor payload for key type '{typeof(TKey).Name}'.");
                    }
                    int keyLength = BinaryPrimitives.ReadInt32BigEndian(buffer.Span[..4]);
                    if(keyLength < 0 || 4 + keyLength + 8 != totalWritten || !TKey.TryParse(buffer.Span.Slice(4, keyLength), null, out TKey key)) {
                        throw new FormatException($"Invalid composite cursor payload for key type '{typeof(TKey).Name}'.");
                    }
                    long tie = BinaryPrimitives.ReadInt64BigEndian(buffer.Span.Slice(4 + keyLength, 8));
                    return (key, tie);
                },
                cancellationToken);
        }

        return ToCursorResultAsync(
            source,
            request,
            keySelector,
            cursorEncoder: static key => {
                Span<byte> keyStackBuffer = stackalloc byte[256];
                int keyLength = FormatKeyUtf8(key, keyStackBuffer, out byte[]? rented);
                try {
                    ReadOnlySpan<byte> keyBytes = rented is null
                        ? keyStackBuffer[..keyLength]
                        : rented.AsSpan(0, keyLength);
                    return CursorToken.FromBytes(keyBytes);
                }
                finally {
                    if(rented is not null) {
                        ArrayPool<byte>.Shared.Return(rented);
                    }
                }
            },
            cursorDecoder: static token => {
                if(token.IsEmpty) {
                    throw new FormatException($"Cursor token payload is empty for key type '{typeof(TKey).Name}'.");
                }
                using ValueBuffer<byte> buffer = new(token.Length, stackalloc byte[256]);
                if(!token.TryDecode(buffer.Span, out int written) || !TKey.TryParse(buffer.Span[..written], null, out TKey result)) {
                    throw new FormatException($"Invalid UTF-8 cursor payload for key type '{typeof(TKey).Name}'.");
                }
                return result;
            },
            cancellationToken);
    }

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

            Expression<Func<TSource, bool>> predicate = Expression.Lambda<Func<TSource, bool>>(comparison, keySelector.Parameters);
            query = query.Where(predicate);

            if(request.Direction == CursorDirection.Backward) {
                // Invert the query order for backward seek
                query = isDescending
                    ? query.OrderBy(keySelector)
                    : query.OrderByDescending(keySelector);
            }
        }

        // 3. N + 1 Technique: Fetch Limit + 1 items to eliminate COUNT(*) queries
        int fetchLimit = request.Limit + 1;
        List<TSource> rawItems = await query
            .Take(fetchLimit)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if(rawItems.Count == 0) {
            return [];
        }

        // 4. Detect if more records exist in the current seek direction, and drop the (+1) extra item
        bool hasMore = rawItems.Count > request.Limit;
        if(hasMore) {
            rawItems.RemoveAt(rawItems.Count - 1);
        }

        // 5. If backward navigation occurred, reverse in-memory to restore original ascending sequence
        if(request.Direction == CursorDirection.Backward && !request.Cursor.IsEmpty) {
            rawItems.Reverse();
        }

        // 6. Evaluate directional navigation flags (Relay/Cursor Specification)
        // Forward:  hasMore indicates HasNext,      existing cursor indicates HasPrevious
        // Backward: hasMore indicates HasPrevious,  existing cursor indicates HasNext
        bool hasNext = request.Direction == CursorDirection.Forward
            ? hasMore
            : !request.Cursor.IsEmpty;

        hasPrevious = request.Direction == CursorDirection.Forward
          ? !request.Cursor.IsEmpty
          : hasMore;

        // 7. Generate Start and End boundary cursors using cached delegate
        Func<TSource, TKey> compiledKeySelector = (Func<TSource, TKey>)CompiledKeySelectorCache.GetOrAdd(
            keySelector,
            static expr => ((Expression<Func<TSource, TKey>>)expr).Compile());

        CursorToken startCursor = cursorEncoder(compiledKeySelector(rawItems[0]));
        CursorToken endCursor = cursorEncoder(compiledKeySelector(rawItems[^1]));

        CursorMetadata metadata = new(startCursor, endCursor, hasPrevious, hasNext);
        return new CursorResult<TSource>(new EquatableArray<TSource>(rawItems), metadata);
    }

    private static bool TryGetTieBreaker<TSource, TTieBreaker>(
      LambdaExpression keySelector,
      [NotNullWhen(true)] out Expression<Func<TSource, TTieBreaker>>? tieBreaker) {

        PropertyInfo? pkProperty = PrimaryKeyPropertyCache.GetOrAdd(typeof(TSource), static type => {
            PropertyInfo? prop = type.GetProperty("Id", BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if(prop != null) return prop;

            prop = type.GetProperty($"{type.Name}Id", BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if(prop != null) return prop;

            foreach(PropertyInfo p in type.GetProperties(BindingFlags.Public | BindingFlags.Instance)) {
                if(p.GetCustomAttribute<System.ComponentModel.DataAnnotations.KeyAttribute>() != null) {
                    return p;
                }
            }

            return null;
        });

        if(pkProperty != null && pkProperty.PropertyType == typeof(TTieBreaker)) {
            if(keySelector.Body is MemberExpression memberExpr && memberExpr.Member.Name == pkProperty.Name) {
                tieBreaker = null;
                return false;
            }

            tieBreaker = (Expression<Func<TSource, TTieBreaker>>)GetterExpressionCache.GetOrAdd(pkProperty, static prop => {
                ParameterExpression param = Expression.Parameter(typeof(TSource), "x");
                MemberExpression propAccess = Expression.Property(param, prop);
                return Expression.Lambda<Func<TSource, TTieBreaker>>(propAccess, param);
            });
            return true;
        }

        tieBreaker = null;
        return false;
    }

    private static bool IsQueryOrderedDescending(Expression expression) {
        Expression? current = expression;
        while(current is MethodCallExpression methodCall) {
            if(methodCall.Method.DeclaringType == typeof(Queryable)) {
                string name = methodCall.Method.Name;
                if(name is (nameof(Queryable.OrderByDescending)) or (nameof(Queryable.ThenByDescending))) {
                    return true;
                }
                if(name is (nameof(Queryable.OrderBy)) or (nameof(Queryable.ThenBy))) {
                    return false;
                }
            }
            current = methodCall.Arguments.Count > 0 ? methodCall.Arguments[0] : null;
        }
        return false; // Default: Ascending
    }

    #endregion
}