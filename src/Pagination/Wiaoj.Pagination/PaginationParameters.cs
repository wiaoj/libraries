namespace Wiaoj.Pagination;

/// <summary>
/// Defines standard HTTP query string parameter names used across pagination mechanisms.
/// </summary>
public static class PaginationParameters {
    /// <summary>
    /// The query parameter name for the 1-based page index in offset pagination (<c>"page"</c>).
    /// </summary>
    public const string Page = "page";

    /// <summary>
    /// The query parameter name for the page capacity/size in offset pagination (<c>"size"</c>).
    /// </summary>
    public const string Size = "size";

    /// <summary>
    /// The query parameter name for the seek cursor token in keyset pagination (<c>"cursor"</c>).
    /// </summary>
    public const string Cursor = "cursor";

    /// <summary>
    /// The query parameter name for the traversal direction in keyset pagination (<c>"direction"</c>).
    /// </summary>
    public const string Direction = "direction";

    /// <summary>
    /// The query parameter name for item limits in keyset or alternate pagination (<c>"limit"</c>).
    /// </summary>
    public const string Limit = "limit";

    /// <summary>
    /// Gets all standard pagination query string parameter names.
    /// </summary>
    public static readonly string[] All = [Page, Size, Cursor, Direction, Limit];
}
