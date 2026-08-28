namespace Wiaoj.Pagination;

/// <summary>
/// Specifies the traversal direction for keyset (cursor-based) pagination.
/// </summary>
public enum CursorDirection : byte {
    /// <summary>
    /// Traverses forward in the sequence (records after the cursor).
    /// </summary>
    Forward = 0,

    /// <summary>
    /// Traverses backward in the sequence (records before the cursor).
    /// </summary>
    Backward = 1
}