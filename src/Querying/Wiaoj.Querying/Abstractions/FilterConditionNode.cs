namespace Wiaoj.Querying;
/// <summary>
/// Represents a single leaf condition node in the query AST.
/// </summary>
/// <param name="Field">The target property or column name.</param>
/// <param name="Operator">The operator to apply.</param>
/// <param name="RawValue">The raw string value extracted from input.</param>
public readonly record struct FilterConditionNode(
    string Field,
    QueryOperator Operator,
    string? RawValue = null);