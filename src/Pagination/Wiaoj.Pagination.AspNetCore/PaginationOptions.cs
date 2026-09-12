namespace Wiaoj.Pagination.AspNetCore;

/// <summary>
/// Provides configuration options for ASP.NET Core pagination middleware and endpoint filters.
/// </summary>
/// <remarks>
/// Set application-wide with <c>services.AddPagination(...)</c>; an endpoint's
/// <c>WithPagination(options =&gt; ...)</c> then applies on top of those, not on top of fresh defaults.
/// </remarks>
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

    /// <summary>Copies these options, so an endpoint can refine them without changing the application's.</summary>
    internal PaginationOptions Clone() {
        return new PaginationOptions {
            EnableLinkHeaders = this.EnableLinkHeaders,
            EnableETag = this.EnableETag
        };
    }
}

/// <summary>
/// The kind of paging an endpoint performs.
/// </summary>
public enum PaginationStyle {
    /// <summary>Page number and size; the response carries <see cref="PageMetadata"/>.</summary>
    Offset,

    /// <summary>Keyset cursors; the response carries <see cref="CursorMetadata"/>.</summary>
    Cursor
}
