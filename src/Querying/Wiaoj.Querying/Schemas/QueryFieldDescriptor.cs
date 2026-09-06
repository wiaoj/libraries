namespace Wiaoj.Querying;

/// <summary>
/// A read-only description of one field a schema exposes to callers.
/// </summary>
/// <remarks>
/// This is the schema's documentable surface. The rules themselves stay internal — a descriptor carries what
/// a caller is allowed to know (the name they write in a query, whether they may filter or sort on it, and
/// with which operators), not how the rule is applied.
/// </remarks>
/// <param name="Name">The name callers use in <c>filter</c> and <c>sort</c> expressions.</param>
/// <param name="Type">The CLR type of the underlying property.</param>
/// <param name="IsFilterable">Whether the field may appear in a filter.</param>
/// <param name="IsSortable">Whether the field may appear in a sort.</param>
/// <param name="AllowedOperators">The operators permitted on this field; empty when it is not filterable.</param>
public readonly record struct QueryFieldDescriptor(
    string Name,
    Type Type,
    bool IsFilterable,
    bool IsSortable,
    IReadOnlyList<QueryOperator> AllowedOperators);
