using Wiaoj.Preconditions;
using Wiaoj.Querying.OpenApi;

#pragma warning disable IDE0130
namespace Microsoft.AspNetCore.OpenApi;
#pragma warning restore IDE0130

/// <summary>
/// Registers the querying contribution to generated OpenAPI documents.
/// </summary>
public static class QueryingOpenApiExtensions {
    /// <summary>
    /// Documents, on every endpoint marked with <c>WithQueryValidation&lt;T&gt;()</c>, the filter and sort
    /// surface its schema accepts, and the 400 it answers when a query violates that schema.
    /// </summary>
    /// <param name="options">The OpenAPI options to add the transformer to.</param>
    /// <returns>The same options, for chaining.</returns>
    /// <remarks>
    /// <para>
    /// <c>QueryRequest</c> binds through <c>BindAsync</c>, which document generation treats as opaque,
    /// so without this an endpoint accepting a query documents no parameters at all — while in fact it
    /// accepts a filter language over a fixed set of fields.
    /// </para>
    /// <para>
    /// Endpoints without the marker are left untouched, so this is safe to add once for the whole document.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// builder.Services.AddOpenApi(options => options.AddWiaojQuerying());
    /// </code>
    /// </example>
    public static OpenApiOptions AddWiaojQuerying(this OpenApiOptions options) {
        Preca.ThrowIfNull(options);

        options.AddOperationTransformer<QueryValidationOperationTransformer>();
        return options;
    }
}
