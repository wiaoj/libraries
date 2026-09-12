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
    /// Substring exclusion (<c>notContains</c>).
    /// </summary>
    NotContains = 8,

    /// <summary>
    /// Prefix search (<c>startsWith</c>).
    /// </summary>
    StartsWith = 9,

    /// <summary>
    /// Prefix exclusion (<c>notStartsWith</c>).
    /// </summary>
    NotStartsWith = 10,

    /// <summary>
    /// Suffix search (<c>endsWith</c>).
    /// </summary>
    EndsWith = 11,

    /// <summary>
    /// Suffix exclusion (<c>notEndsWith</c>).
    /// </summary>
    NotEndsWith = 12,

    /// <summary>
    /// Set inclusion check (<c>in</c>).
    /// </summary>
    In = 13,

    /// <summary>
    /// Set exclusion check (<c>notIn</c>).
    /// </summary>
    NotIn = 14,

    /// <summary>
    /// Range boundary inclusion check (<c>between</c>).
    /// </summary>
    Between = 15,

    /// <summary>
    /// Range boundary exclusion check (<c>notBetween</c>).
    /// </summary>
    NotBetween = 16,

    /// <summary>
    /// Null check (<c>isNull</c>).
    /// </summary>
    IsNull = 17,

    /// <summary>
    /// Not null check (<c>isNotNull</c>).
    /// </summary>
    IsNotNull = 18
}