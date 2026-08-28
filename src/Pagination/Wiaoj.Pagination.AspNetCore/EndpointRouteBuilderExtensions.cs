using Microsoft.AspNetCore.Http;
using Wiaoj.Pagination.AspNetCore;
using Wiaoj.Pagination.AspNetCore.Filters;
using Wiaoj.Preconditions;
using Wiaoj.Preconditions.Exceptions;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Microsoft.AspNetCore.Builder;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Extension methods for configuring pagination endpoint filters on route handlers.
/// </summary>
public static class EndpointRouteBuilderExtensions {

    /// <summary>
    /// Adds automatic RFC 8288 <c>Link</c> headers, <c>Pagination</c> metadata, 
    /// and <c>ETag</c> / <c>304 Not Modified</c> evaluation to the endpoint using default options.
    /// </summary>
    /// <remarks>
    /// Reuses a shared, pre-allocated filter instance to eliminate heap allocations.
    /// </remarks>
    /// <typeparam name="TBuilder">The endpoint convention builder type.</typeparam>
    /// <param name="builder">The endpoint builder.</param>
    /// <returns>The endpoint builder for chaining.</returns>
    /// <exception cref="PrecaArgumentNullException">Thrown when <paramref name="builder"/> is <see langword="null"/>.</exception>
    /// <example>
    /// <code>
    /// app.MapGet("/api/products", async (AppDbContext db, PageRequest request) => {
    ///     return await db.Products.ToPagedResultAsync(request);
    /// }).WithPagination();
    /// </code>
    /// </example>
    public static TBuilder WithPagination<TBuilder>(this TBuilder builder) where TBuilder : IEndpointConventionBuilder {
        Preca.ThrowIfNull(builder);

        builder.AddEndpointFilter(PaginationEndpointFilter.Default);
        return builder;
    }

    /// <summary>
    /// Adds automatic RFC 8288 <c>Link</c> headers, <c>Pagination</c> metadata, 
    /// and <c>ETag</c> / <c>304 Not Modified</c> evaluation to the endpoint using custom configured options.
    /// </summary>
    /// <typeparam name="TBuilder">The endpoint convention builder type.</typeparam>
    /// <param name="builder">The endpoint builder.</param>
    /// <param name="configureOptions">An action to configure pagination options.</param>
    /// <returns>The endpoint builder for chaining.</returns>
    /// <exception cref="PrecaArgumentNullException">Thrown when <paramref name="builder"/> or <paramref name="configureOptions"/> is <see langword="null"/>.</exception>
    /// <example>
    /// <code>
    /// app.MapGet("/api/orders", async (AppDbContext db, CursorRequest request) => {
    ///     return await db.Orders.ToCursorResultAsync(request, o => o.Id);
    /// }).WithPagination(options => {
    ///     options.EnableETag = false;
    ///     options.MetadataHeaderName = "Pagination-Metadata";
    /// });
    /// </code>
    /// </example>
    public static TBuilder WithPagination<TBuilder>(
        this TBuilder builder,
        Action<PaginationOptions> configureOptions) where TBuilder : IEndpointConventionBuilder {

        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNull(configureOptions);

        PaginationOptions options = new();
        configureOptions(options);

        builder.AddEndpointFilter(new PaginationEndpointFilter(options));
        return builder;
    }
}