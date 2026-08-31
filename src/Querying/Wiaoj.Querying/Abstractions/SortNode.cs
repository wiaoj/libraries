using Wiaoj.Preconditions;

namespace Wiaoj.Querying;

/// <summary>
/// Represents an individual field and its direction within a sort expression.
/// </summary>
/// <param name="Field">The target property or exposed field name to sort on.</param>
/// <param name="Direction">The sort direction to apply.</param>
public readonly record struct SortNode(
    string Field,
    SortDirection Direction = SortDirection.Ascending) {

    /// <summary>
    /// Gets a value indicating whether the sort direction is ascending.
    /// </summary>
    public bool IsAscending => this.Direction == SortDirection.Ascending;

    /// <summary>
    /// Gets a value indicating whether the sort direction is descending.
    /// </summary>
    public bool IsDescending => this.Direction == SortDirection.Descending;

    /// <summary>
    /// Creates a new <see cref="SortNode"/> with an ascending sort direction.
    /// </summary>
    /// <param name="field">The target property or exposed field name.</param>
    /// <returns>A new <see cref="SortNode"/> configured for ascending order.</returns>
    public static SortNode Ascending(string field) {
        Preca.ThrowIfEmptyOrWhiteSpace(field);
        return new(field.Trim(), SortDirection.Ascending);
    }

    /// <summary>
    /// Creates a new <see cref="SortNode"/> with a descending sort direction.
    /// </summary>
    /// <param name="field">The target property or exposed field name.</param>
    /// <returns>A new <see cref="SortNode"/> configured for descending order.</returns>
    public static SortNode Descending(string field) {
        Preca.ThrowIfEmptyOrWhiteSpace(field);
        return new(field.Trim(), SortDirection.Descending);
    }
}