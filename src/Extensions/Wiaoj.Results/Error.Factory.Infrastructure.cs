namespace Wiaoj.Results;

public readonly partial record struct Error {
    /// <summary>
    /// Creates an Infrastructure error with a specific code and description.
    /// Suitable for errors related to databases, networks, file systems, etc.
    /// </summary>
    /// <param name="code">The error code.</param>
    /// <param name="description">The error description.</param>
    /// <returns>A new Error instance with <see cref="ErrorType"/> set to <see cref="ErrorType.Infrastructure"/>.</returns> 
    public static Error Infrastructure(string code, string description)
        => new(code, description, ErrorType.Infrastructure);

    /// <summary>
    /// Creates an Infrastructure error with a specific code, description, and metadata.
    /// </summary>
    /// <param name="code">The error code.</param>
    /// <param name="description">The error description.</param>
    /// <param name="metadata">Optional metadata related to the infrastructure failure (e.g., database error codes, network details, exception info).</param>
    /// <returns>A new Error instance with <see cref="ErrorType"/> set to <see cref="ErrorType.Infrastructure"/>.</returns>
    public static Error Infrastructure(string code,
                                       string description,
                                       IReadOnlyDictionary<string, object> metadata)
        => new(code, description, ErrorType.Infrastructure, metadata);
}