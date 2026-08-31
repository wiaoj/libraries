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
}