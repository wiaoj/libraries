namespace Wiaoj.Results;

public readonly partial record struct Error {
    /// <summary>
    /// Creates an Authentication error with a specific code and description.
    /// </summary>
    /// <param name="code">The error code.</param>
    /// <param name="description">The error description.</param>
    /// <returns>A new Error instance with <see cref="ErrorType"/> set to <see cref="ErrorType.Authentication"/>.</returns>
    public static Error Authentication(string code, string description) {
        return new(code, description, ErrorType.Authentication);
    }

    /// <summary>
    /// Creates an Authentication error with a specific code, description, and metadata.
    /// </summary>
    /// <param name="code">The error code.</param>
    /// <param name="description">The error description.</param>
    /// <param name="metadata">Optional metadata related to the authentication failure.</param>
    /// <returns>A new Error instance with <see cref="ErrorType"/> set to <see cref="ErrorType.Authentication"/>.</returns> 
    public static Error Authentication(string code,
                                       string description,
                                       IReadOnlyDictionary<string, object> metadata) {
        return new(code, description, ErrorType.Authentication, metadata);
    }
}