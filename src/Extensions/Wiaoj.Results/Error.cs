namespace Wiaoj.Results;

/// <summary>
/// Represents a rich, structured error object indicating the reason for an operation's failure.
/// This structure is immutable and features value-based equality.
/// Factory methods are organized into partial files based on ErrorType.
/// </summary>
public readonly partial record struct Error {
    /// <summary>A machine-readable code for the error (e.g., "User.NotFound").</summary>
    public string Code { get; }

    /// <summary>A human-readable description of the error.</summary>
    public string Description { get; }

    /// <summary>The <see cref="ErrorType"/> that categorizes the error.</summary>
    public ErrorType Type { get; }

    /// <summary>Optional dictionary containing additional contextual data about the error.</summary>
    public IReadOnlyDictionary<string, object> Metadata { get; }

    public Error(string code, string description, ErrorType type) : this(code, description, type, null) { }

    public Error(string code, string description, ErrorType type, IReadOnlyDictionary<string, object>? metadata = null) {
        this.Code = code;
        this.Description = description;
        this.Type = type;
        this.Metadata = metadata ?? new Dictionary<string, object>();
    }
}

/// <summary>
/// Defines the category of an error as a "Smart Enum", which can carry rich data and behavior.
/// </summary>
public enum ErrorType {
    Failure,
    Unexpected,
    Validation,
    NotFound,
    Conflict,
    Authentication,
    Authorization,
    Forbidden,
    ServiceUnavailable,
    Infrastructure,
}