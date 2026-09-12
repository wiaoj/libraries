namespace Wiaoj.Querying;

/// <summary>
/// Specifies the sort direction for query ordering operations.
/// </summary>
public enum SortDirection : byte {
    /// <summary>
    /// Sorts in ascending order (lowest to highest).
    /// </summary>
    Ascending = 0,

    /// <summary>
    /// Sorts in descending order (highest to lowest).
    /// </summary>
    Descending = 1
}