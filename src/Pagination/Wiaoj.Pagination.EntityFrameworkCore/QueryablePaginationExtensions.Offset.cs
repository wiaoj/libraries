using System.Runtime.CompilerServices;
using Wiaoj.Pagination;
using Wiaoj.Preconditions;
using Wiaoj.Preconditions.Exceptions;
using Wiaoj.Primitives.Collections;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Microsoft.EntityFrameworkCore;
#pragma warning restore IDE0130 // Namespace does not match folder structure


/// <summary>
/// Provides asynchronous Entity Framework Core extensions for paginating <see cref="IQueryable{T}"/> sources.
/// </summary>
public static partial class QueryablePaginationExtensions {

    #region Offset Pagination

    /// <summary>
    /// Asynchronously executes offset-based pagination on an <see cref="IQueryable{T}"/> source
    /// utilizing a 64-bit integer (<see cref="long"/>) count query.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Execution Pipeline:</b>
    /// <list type="number">
    ///   <item><description>Executes a fast <c>COUNT_BIG(*)</c> via <see cref="EntityFrameworkQueryableExtensions.LongCountAsync{TSource}(IQueryable{TSource}, CancellationToken)"/>.</description></item>
    ///   <item><description>If <c>TotalCount == 0</c> or <c>Skip &gt;= TotalCount</c>, immediately returns an empty <see cref="PagedResult{T}"/> without issuing a second SQL data query.</description></item>
    ///   <item><description>Otherwise, queries the database slice using <c>.Skip(skip).Take(pageSize)</c> and wraps the items into an immutable <see cref="EquatableArray{T}"/>.</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    /// <typeparam name="T">The entity or projected data model type.</typeparam>
    /// <param name="source">The source queryable. Must be explicitly ordered before calling pagination.</param>
    /// <param name="request">The offset pagination request parameters (PageNumber and PageSize).</param>
    /// <param name="cancellationToken">A token to observe while waiting for the database query to complete.</param>
    /// <returns>
    /// A task representing the asynchronous query execution, containing a <see cref="PagedResult{T}"/> 
    /// with the items and offset metadata.
    /// </returns>
    /// <exception cref="PrecaArgumentNullException">Thrown when <paramref name="source"/> is <see langword="null"/>.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the <paramref name="cancellationToken"/> is canceled.</exception>
    /// <example>
    /// <code>
    /// var request = new PageRequest(pageNumber: 2, pageSize: 20);
    /// 
    /// var result = await dbContext.Products
    ///     .AsNoTracking()
    ///     .Where(p => p.IsActive)
    ///     .OrderBy(p => p.Id)
    ///     .ToPagedResultAsync(request, cancellationToken);
    /// </code>
    /// </example>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task<PagedResult<T>> ToPagedResultAsync<T>(
        this IQueryable<T> source,
        PageRequest request,
        CancellationToken cancellationToken = default) {

        Preca.ThrowIfNull(source);

        return ToPagedResultInternalAsync(source, request.PageNumber, request.PageSize, request.CalculateSkip(), cancellationToken);
    }

    /// <summary>
    /// Asynchronously executes offset-based pagination on an <see cref="IQueryable{T}"/> source with raw numbers.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Internally encapsulates the raw integer parameters into an immutable <see cref="PageRequest"/> struct
    /// to enforce boundary clamping and overflow protection.
    /// </para>
    /// </remarks>
    /// <typeparam name="T">The entity or projected data model type.</typeparam>
    /// <param name="source">The source queryable. Must be explicitly ordered before calling pagination.</param>
    /// <param name="pageNumber">The 1-based page index. Clamped to minimum 1.</param>
    /// <param name="pageSize">The maximum items per page. Clamped between 1 and <see cref="PageRequest.MaxPageSize"/>.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the database query to complete.</param>
    /// <returns>
    /// A task representing the asynchronous query execution, containing a <see cref="PagedResult{T}"/>.
    /// </returns>
    /// <exception cref="PrecaArgumentNullException">Thrown when <paramref name="source"/> is <see langword="null"/>.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the <paramref name="cancellationToken"/> is canceled.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task<PagedResult<T>> ToPagedResultAsync<T>(
        this IQueryable<T> source,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default) {

        Preca.ThrowIfNull(source);

        PageRequest request = new(pageNumber, pageSize);
        return ToPagedResultInternalAsync(source, request.PageNumber, request.PageSize, request.CalculateSkip(), cancellationToken);
    }

    private static async Task<PagedResult<T>> ToPagedResultInternalAsync<T>(
        IQueryable<T> source,
        int pageNumber,
        int pageSize,
        int skip,
        CancellationToken cancellationToken) {

        // 1. 64-bit bigint supported count query (COUNT_BIG(*))
        long totalCount = await source.LongCountAsync(cancellationToken).ConfigureAwait(false);

        // Optimization: If table is empty or requested page is out of bounds, skip the data SELECT query entirely
        if(totalCount == 0 || skip >= totalCount) {
            return new PagedResult<T>([], new PageMetadata(totalCount, pageNumber, pageSize));
        }

        // 2. Fetch the paginated data slice
        List<T> itemsList = await source
            .Skip(skip)
            .Take(pageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        PageMetadata metadata = new(totalCount, pageNumber, pageSize);
        return new PagedResult<T>(itemsList, metadata);
    }

    #endregion
}