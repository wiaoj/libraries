namespace Wiaoj.Querying;

/// <summary>
/// Defines supported comparison, pattern matching, and collection query operators.
/// </summary>
public enum QueryOperator : byte {
    /// <summary>
    /// Equal comparison (<c>eq</c> or <c>==</c>).
    /// </summary>
    Equal = 1,

    /// <summary>
    /// Not equal comparison (<c>neq</c> or <c>!=</c>).
    /// </summary>
    NotEqual = 2,

    /// <summary>
    /// Greater than comparison (<c>gt</c> or <c>&gt;</c>).
    /// </summary>
    GreaterThan = 3,

    /// <summary>
    /// Greater than or equal comparison (<c>gte</c> or <c>&gt;=</c>).
    /// </summary>
    GreaterThanOrEqual = 4,

    /// <summary>
    /// Less than comparison (<c>lt</c> or <c>&lt;</c>).
    /// </summary>
    LessThan = 5,

    /// <summary>
    /// Less than or equal comparison (<c>lte</c> or <c>&lt;=</c>).
    /// </summary>
    LessThanOrEqual = 6,

    /// <summary>
    /// Substring search (<c>contains</c>).
    /// </summary>
    Contains = 7,

    /// <summary>
    /// Prefix search (<c>startsWith</c>).
    /// </summary>
    StartsWith = 8,

    /// <summary>
    /// Suffix search (<c>endsWith</c>).
    /// </summary>
    EndsWith = 9,

    /// <summary>
    /// Set inclusion check (<c>in</c>).
    /// </summary>
    In = 10,

    /// <summary>
    /// Set exclusion check (<c>notIn</c>).
    /// </summary>
    NotIn = 11,

    /// <summary>
    /// Range boundary check (<c>between</c>).
    /// </summary>
    Between = 12,

    /// <summary>
    /// Null check (<c>isNull</c>).
    /// </summary>
    IsNull = 13,

    /// <summary>
    /// Not null check (<c>isNotNull</c>).
    /// </summary>
    IsNotNull = 14
}