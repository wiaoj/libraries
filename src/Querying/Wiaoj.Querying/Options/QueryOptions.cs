namespace Wiaoj.Querying;

/// <summary>
/// Global configuration options for query processing and payload binding.
/// </summary>
public sealed class QueryOptions {
    /// <summary>
    /// Gets or sets a value indicating whether HTTP request body payloads (RFC 10008 HTTP QUERY / POST)
    /// are accepted and parsed into <see cref="QueryRequest"/> instances. Defaults to <see langword="true"/>.
    /// </summary>
    public bool AllowBodyPayloads { get; set; } = true;

    /// <summary>
    /// Gets or sets an optional, application-wide ceiling (in bytes) applied on top of each parser's own
    /// <see cref="Parsers.IQueryPayloadParser.MaxPayloadBytes"/> and the host's general request body limit
    /// (<c>IHttpMaxRequestBodySizeFeature</c>). The binder uses the smallest of all three, so this can only
    /// tighten the effective limit below whichever of the other two is smaller — it can never loosen a
    /// parser's own limit beyond what the parser itself allows.
    /// </summary>
    /// <remarks>
    /// Leave <see langword="null"/> (the default) to rely solely on each parser's own limit and the host's.
    /// Set this when you want to raise or lower the ceiling uniformly across every registered parser without
    /// implementing a custom parser just to change a number — e.g. <c>options.MaxPayloadBytes = 128 * 1024</c>
    /// to allow larger query payloads application-wide.
    /// </remarks>
    public int? MaxPayloadBytes { get; set; }

    /// <summary>
    /// Gets the collection of query parameter names to ignore during URL query string binding.
    /// Parameters in this collection will not be bound into <see cref="FilterConditionNode"/> filters.
    /// Case-insensitive by default.
    /// </summary>
    public HashSet<string> IgnoredParameters { get; } = new(StringComparer.OrdinalIgnoreCase);
}