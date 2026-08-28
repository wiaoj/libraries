namespace Wiaoj.Pagination.AspNetCore;

/// <summary>
/// Provides configuration options for ASP.NET Core pagination middleware and endpoint filters.
/// </summary>
public sealed class PaginationOptions {
    /// <summary>
    /// Gets or sets a value indicating whether RFC 8288 compliant <c>Link</c> headers should be appended to responses.
    /// Default is <see langword="true"/>.
    /// </summary>
    public bool EnableLinkHeaders { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether <c>ETag</c> evaluation and 304 Not Modified handling are enabled.
    /// Default is <see langword="true"/>.
    /// </summary>
    public bool EnableETag { get; set; } = true;

    /// <summary>
    /// Gets or sets the custom header name used to expose raw pagination metadata in the HTTP response.
    /// Follows RFC 6648 (no 'X-' prefix). Set to <see langword="null"/> to disable. Default is <c>"Pagination"</c>.
    /// </summary>
    public string? MetadataHeaderName { get; set; } = "Pagination";
}