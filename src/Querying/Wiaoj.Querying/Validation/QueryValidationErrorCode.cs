namespace Wiaoj.Querying;

/// <summary>
/// Defines categorical diagnostic error codes produced during query request validation.
/// </summary>
public enum QueryValidationErrorCode : byte {
    /// <summary>The specified field is not configured or allowed for filtering.</summary>
    FieldNotFilterable = 1,

    /// <summary>The specified operator is not permitted for the target property.</summary>
    OperatorNotAllowed = 2,

    /// <summary>The raw value could not be parsed into the target property type.</summary>
    InvalidValueFormat = 3,

    /// <summary>The range expression does not conform to the expected boundary syntax.</summary>
    MalformedRange = 4,

    /// <summary>The specified field is not configured or allowed for sorting.</summary>
    FieldNotSortable = 5,

    /// <summary>The total number of filters exceeds the configured maximum limit.</summary>
    MaxFilterCountExceeded = 6,

    /// <summary>The number of values in a collection operation exceeds the configured maximum limit.</summary>
    MaxInValuesCountExceeded = 7,

    /// <summary>The total number of sort fields exceeds the configured maximum limit.</summary>
    MaxSortFieldsCountExceeded = 8,

    /// <summary>A filter's raw value exceeds the configured maximum character length.</summary>
    FilterValueTooLong = 9,

    /// <summary>The free-text search term exceeds the configured maximum character length.</summary>
    SearchTermTooLong = 10
}