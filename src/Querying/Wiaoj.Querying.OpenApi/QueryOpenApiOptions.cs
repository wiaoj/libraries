using Microsoft.OpenApi;

namespace Wiaoj.Querying.OpenApi;

/// <summary>
/// How a filterable field is described in the generated document.
/// </summary>
public enum QueryFilterStyle {
    /// <summary>
    /// One parameter per field, typed as the field's equality value, with the permitted operators written into
    /// its description. Every generator renders this; a client writes <c>field[op]=value</c> by hand.
    /// </summary>
    Prose,

    /// <summary>
    /// One <c>deepObject</c> parameter per field whose properties are the permitted operators, each typed —
    /// the exact OpenAPI description of <c>field[op]=value</c>. A generated client gets
    /// <c>{ eq?: string; in?: string }</c> and cannot name an operator the field refuses. Support varies across
    /// generators; check yours before choosing it. The bare <c>field=value</c> shorthand keeps working on the
    /// wire but is not described.
    /// </summary>
    DeepObject,

    /// <summary>
    /// One parameter per permitted operator, each named as it is written and typed: <c>usageCount</c> for equality,
    /// <c>usageCount[gte]</c>, <c>usageCount[lte]</c>. A generated client gets
    /// <c>{ usageCount?: number; "usageCount[gte]"?: number }</c> and sends each as a plain query parameter, so it
    /// needs no serializer that understands nested objects — unlike <see cref="DeepObject"/>. Equality is described
    /// once, as the bare name, so a client cannot send the same condition two ways.
    /// </summary>
    OperatorParameters
}

/// <summary>
/// How a <c>POST</c> query endpoint, which reads the query from its body, is described.
/// </summary>
public enum QueryRequestBodyDescription {
    /// <summary>
    /// Describe the request body and keep the query parameters: the binder falls back to the query string when the body
    /// is empty, so both forms work.
    /// </summary>
    BodyAndQueryParameters,

    /// <summary>Describe only the request body, for APIs that want clients to send queries in the body.</summary>
    BodyOnly
}

/// <summary>
/// Configures how query schemas are published into OpenAPI documents.
/// </summary>
/// <remarks>
/// The defaults describe what the application enforces, named and typed the way the application configured.
/// These settings exist for when that is right and still not what a particular document wants — so the
/// transformer's output can be shaped here rather than patched by another transformer that runs after it and
/// depends on its output staying the same.
/// </remarks>
public sealed class QueryOpenApiOptions {
    /// <summary>Gets or sets how filterable fields are described. Defaults to <see cref="QueryFilterStyle.Prose"/>.</summary>
    public QueryFilterStyle FilterStyle { get; set; } = QueryFilterStyle.Prose;

    /// <summary>
    /// Gets or sets how a <c>POST</c> query endpoint is described. Defaults to
    /// <see cref="QueryRequestBodyDescription.BodyAndQueryParameters"/>.
    /// </summary>
    /// <remarks>
    /// <c>QUERY</c> endpoints (RFC 10008) cannot be described: the OpenAPI version ASP.NET Core generates (3.0 and 3.1,
    /// through Microsoft.OpenApi 2.x) has no <c>query</c> operation, and document generation leaves the method out. Their
    /// support is advertised instead by the <c>Accept-Query</c> response header on the operations that are described.
    /// </remarks>
    public QueryRequestBodyDescription RequestBodyDescription { get; set; } = QueryRequestBodyDescription.BodyAndQueryParameters;

    /// <summary>
    /// Gets or sets a callback run for every filter parameter after it is built, with the field it describes.
    /// </summary>
    /// <remarks>Use it to add examples, extensions or deprecation, with full knowledge of the field.</remarks>
    public Action<QueryFieldDescriptor, OpenApiParameter>? ConfigureFilter { get; set; }

    /// <summary>
    /// Gets or sets a callback run once per operation after every query parameter is described, with all the
    /// fields the schema exposes.
    /// </summary>
    public Action<OpenApiOperation, IReadOnlyList<QueryFieldDescriptor>>? ConfigureOperation { get; set; }
}
