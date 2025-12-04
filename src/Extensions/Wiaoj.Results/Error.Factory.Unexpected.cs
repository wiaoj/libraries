using System.Diagnostics.CodeAnalysis;

namespace Wiaoj.Results;

public readonly partial record struct Error {
    /// <summary>
    /// Creates an Unexpected error using default code and description.
    /// Suitable for representing a general, unexpected issue without specific details yet.
    /// </summary> 
    public static Error Unexpected() => Unexpected("General.Unexpected", "An unexpected error occurred.");

    /// <summary>
    /// Creates an Unexpected error with a specified code and description.
    /// Suitable for wrapping exceptions or unexpected system states when a custom code is needed.
    /// </summary>
    /// <param name="code">The specific error code (e.g., "System.IO.FileNotFound").</param>
    /// <param name="description">The human-readable description of the error.</param> 
    public static Error Unexpected(string code, string description) => new(code, description, ErrorType.Unexpected);

    /// <summary>
    /// Creates an Unexpected error with a specified code, description, and optional metadata.
    /// Suitable for providing additional context for the unexpected error.
    /// </summary>
    /// <param name="code">The specific error code.</param>
    /// <param name="description">The human-readable description of the error.</param>
    /// <param name="metadata">Optional metadata associated with the error.</param> 
    public static Error Unexpected(string code,
                                   string description,
                                   IReadOnlyDictionary<string, object> metadata) => new(code, description, ErrorType.Unexpected, metadata);
     
    public static Error Unexpected([NotNull] Exception exception) {
        ArgumentNullException.ThrowIfNull(exception);
        return Unexpected("General.Unexpected",
            string.IsNullOrWhiteSpace(exception.Message) ? "An unexpected error occurred." : exception.Message,
            new Dictionary<string, object> {
               { "ExceptionType", exception.GetType().Name },
            });
    }

    public static Error Unexpected([NotNull] Exception exception, string code, string description) {
        ArgumentNullException.ThrowIfNull(exception);
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        return Unexpected(code, description,
            new Dictionary<string, object> {
               { "ExceptionType", exception.GetType().Name },
            });
    }

    public static Error Unexpected([NotNull] Exception exception, string code, string description, IReadOnlyDictionary<string, object> metadata) {
        ArgumentNullException.ThrowIfNull(exception);
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentNullException.ThrowIfNull(metadata);
        return Unexpected(code, description,
            new Dictionary<string, object>(metadata) {
                { "ExceptionType", exception.GetType().Name },
            });
    }

    /// <summary>
    /// Creates an Unexpected error by wrapping a caught <see cref="Exception"/> with additional metadata.
    /// Automatically sets a default code and description based on the exception.
    /// Combines the provided metadata with exception information.
    /// </summary>
    /// <param name="exception">The caught exception to wrap.</param>
    /// <param name="metadata">Additional metadata to include with the error.</param>
    /// <returns>An Error object representing the unexpected exception with combined metadata.</returns>
    /// <remarks>
    /// This overload allows providing custom metadata while still capturing essential exception details.
    /// The exception's type name is automatically added to the metadata dictionary.
    /// Like other exception-handling methods, sensitive details like stack traces are intentionally excluded.
    /// </remarks>
    public static Error Unexpected([NotNull] Exception exception,
                                   IReadOnlyDictionary<string, object> metadata) {
        ArgumentNullException.ThrowIfNull(exception);
        ArgumentNullException.ThrowIfNull(metadata);

        string description = string.IsNullOrWhiteSpace(exception.Message)
            ? "An unexpected error occurred."
            : exception.Message;
        return Unexpected("General.Unexpected",
                          description,
                          new Dictionary<string, object>(metadata) {
                            { "ExceptionType", exception.GetType().Name },
                          });
    }
}