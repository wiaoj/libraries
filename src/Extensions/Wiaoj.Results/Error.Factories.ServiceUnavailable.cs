namespace Wiaoj.Results;

public readonly partial record struct Error {
    /// <summary>
    /// Creates a ServiceUnavailable error with a specific code and description.
    /// Represents errors when a required service is offline or unavailable.
    /// </summary>
    /// <param name="code">The error code.</param>
    /// <param name="description">The error description.</param>
    /// <returns>A new Error instance with <see cref="ErrorType"/> set to <see cref="ErrorType.ServiceUnavailable"/>.</returns>
    public static Error ServiceUnavailable(string code, string description) {
        return new(code, description, ErrorType.ServiceUnavailable);
    }

    /// <summary>
    /// Creates a ServiceUnavailable error with a specific code, description, and metadata.
    /// </summary>
    /// <param name="code">The error code.</param>
    /// <param name="description">The error description.</param>
    /// <param name="metadata">Optional metadata related to the unavailable service (e.g., service name, endpoint, outage details).</param>
    /// <returns>A new Error instance with <see cref="ErrorType"/> set to <see cref="ErrorType.ServiceUnavailable"/>.</returns> 
    public static Error ServiceUnavailable(string code,
                                         string description,
                                         IReadOnlyDictionary<string, object> metadata) {
        return new(code, description, ErrorType.ServiceUnavailable, metadata);
    }
}