using Wiaoj.Pagination.OpenApi;
using Wiaoj.Preconditions;

#pragma warning disable IDE0130
namespace Microsoft.AspNetCore.OpenApi;
#pragma warning restore IDE0130

/// <summary>
/// Registers the pagination contribution to generated OpenAPI documents.
/// </summary>
public static class PaginationOpenApiExtensions {
    /// <summary>
    /// Documents the pagination result types, and — on every endpoint marked with <c>WithPagination()</c> —
    /// the paging query parameters and the <c>Link</c> / <c>ETag</c> headers and <c>304</c> response the
    /// pagination filter produces.
    /// </summary>
    /// <param name="options">The OpenAPI options to add the transformer to.</param>
    /// <returns>The same options, for chaining.</returns>
    /// <remarks>
    /// <para>
    /// None of what the pagination filter does is visible in a handler's signature, so a document generated
    /// without this describes an endpoint that answers only 200 and sets no headers — which is not the
    /// endpoint that exists.
    /// </para>
    /// <para>
    /// Neither is the shape of what it returns. <c>PagedResult&lt;T&gt;</c> and <c>CursorResult&lt;T&gt;</c>
    /// serialise through hand-written converters, and a schema generator reading their properties finds
    /// nothing it recognises — so both arrive in the document as an empty schema, and a generated client
    /// types the response as <c>unknown</c>. The schema transformer added here writes their real shape.
    /// </para>
    /// <para>
    /// Endpoints without the marker are left untouched, so this is safe to add once for the whole document.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// builder.Services.AddOpenApi(options => options.AddWiaojPagination());
    /// </code>
    /// </example>
    public static OpenApiOptions AddWiaojPagination(this OpenApiOptions options) {
        Preca.ThrowIfNull(options);

        options.AddSchemaTransformer<PaginationSchemaTransformer>();
        options.AddOperationTransformer<PaginationOperationTransformer>();
        return options;
    }
}
