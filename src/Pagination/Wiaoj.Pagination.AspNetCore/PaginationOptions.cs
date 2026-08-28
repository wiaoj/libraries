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
}