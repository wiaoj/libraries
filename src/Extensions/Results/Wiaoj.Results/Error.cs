namespace Wiaoj.Results;    
/// <summary>
/// Represents a rich, structured error object indicating the reason for an operation's failure.
/// This structure is immutable and features value-based equality.
/// </summary>
public readonly record struct Error {
    /// <summary>
    /// Gets the unique machine-readable code for the error (e.g., "User.NotFound").
    /// </summary>
    public string Code { get; }

    /// <summary>
    /// Gets the human-readable description of the error.
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// Gets the <see cref="ErrorType"/> that categorizes the error (e.g., Validation, NotFound).
    /// </summary>
    public ErrorType Type { get; }

    /// <summary>
    /// Gets the optional dictionary containing additional contextual data about the error.
    /// </summary>
    public IReadOnlyDictionary<string, object>? Metadata { get; }

    private Error(string code, string description, ErrorType type, IReadOnlyDictionary<string, object>? metadata) {
        this.Code = code;
        this.Description = description;
        this.Type = type;
        this.Metadata = metadata;
    }

    /// <summary>
    /// Creates a general failure error.
    /// </summary>
    /// <param name="code">The error code (default: "General.Failure").</param>
    /// <param name="description">The error description (default: "A failure has occurred.").</param>
    public static Error Failure(string code = "General.Failure", string description = "A failure has occurred.") {
        return new(code, description, ErrorType.Failure, null);
    }

    /// <summary>
    /// Creates an unexpected error. Use this for unhandled exceptions or system faults.
    /// </summary>
    /// <param name="code">The error code (default: "General.Unexpected").</param>
    /// <param name="description">The error description (default: "An unexpected error occurred.").</param>
    public static Error Unexpected(string code = "General.Unexpected", string description = "An unexpected error occurred.") {
        return new(code, description, ErrorType.Unexpected, null);
    }

    /// <summary>
    /// Creates a validation error (e.g., invalid input format).
    /// </summary>
    /// <param name="code">The error code.</param>
    /// <param name="description">The error description.</param>
    public static Error Validation(string code, string description) {
        return new(code, description, ErrorType.Validation, null);
    }

    /// <summary>
    /// Creates a Not Found error.
    /// </summary>
    /// <param name="code">The error code (default: "Resource.NotFound").</param>
    /// <param name="description">The error description (default: "Resource not found.").</param>
    public static Error NotFound(string code = "Resource.NotFound", string description = "Resource not found.") {
        return new(code, description, ErrorType.NotFound, null);
    }

    /// <summary>
    /// Creates a Not Found error for a specific resource and identifier.
    /// </summary>
    /// <param name="resourceName">The name of the resource (e.g., "User").</param>
    /// <param name="id">The identifier of the missing resource.</param>
    public static Error NotFound(string resourceName, object id) {
        return new($"{resourceName}.NotFound", $"{resourceName} with id '{id}' was not found.", ErrorType.NotFound, null);
    }

    /// <summary>
    /// Creates a Conflict error (e.g., duplicate unique key).
    /// </summary>
    /// <param name="code">The error code (default: "Resource.Conflict").</param>
    /// <param name="description">The error description (default: "A conflict has occurred.").</param>
    public static Error Conflict(string code = "Resource.Conflict", string description = "A conflict has occurred.") {
        return new(code, description, ErrorType.Conflict, null);
    }

    /// <summary>
    /// Creates an Unauthorized error (e.g., user is not logged in).
    /// </summary>
    /// <param name="code">The error code (default: "Auth.Unauthorized").</param>
    /// <param name="description">The error description (default: "Unauthorized access.").</param>
    public static Error Unauthorized(string code = "Auth.Unauthorized", string description = "Unauthorized access.") {
        return new(code, description, ErrorType.Unauthorized, null);
    }

    /// <summary>
    /// Creates a Forbidden error (e.g., user is logged in but lacks permissions).
    /// </summary>
    /// <param name="code">The error code (default: "Auth.Forbidden").</param>
    /// <param name="description">The error description (default: "Access forbidden.").</param>
    public static Error Forbidden(string code = "Auth.Forbidden", string description = "Access forbidden.") {
        return new(code, description, ErrorType.Forbidden, null);
    }

    /// <summary>
    /// Creates a new copy of this error with additional metadata attached.
    /// </summary>
    /// <param name="key">The metadata key.</param>
    /// <param name="value">The metadata value.</param>
    /// <returns>A new <see cref="Error"/> instance with the added metadata.</returns>
    public Error WithMetadata(string key, object value) {
        Dictionary<string, object> newMeta = this.Metadata == null
            ? []
            : new Dictionary<string, object>(this.Metadata);

        newMeta[key] = value;

        return new Error(this.Code, this.Description, this.Type, newMeta);
    }
}