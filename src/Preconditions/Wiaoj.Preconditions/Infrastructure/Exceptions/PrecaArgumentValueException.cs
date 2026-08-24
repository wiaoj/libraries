namespace Wiaoj.Preconditions.Exceptions;

/// <summary>
/// Thrown when an argument has a specific, disallowed value (e.g. MaxValue, MinValue).
/// Inherits from <see cref="ArgumentOutOfRangeException"/> for semantic correctness and compatibility.
/// </summary>
public class PrecaArgumentValueException : ArgumentOutOfRangeException {
    /// <summary>
    /// Initializes a new instance of the <see cref="PrecaArgumentValueException"/> class.
    /// </summary>
    public PrecaArgumentValueException() : base() { }

    /// <summary>
    /// Initializes a new instance of the <see cref="PrecaArgumentValueException"/> class with a parameter name.
    /// </summary>
    /// <param name="paramName">The name of the invalid parameter.</param>
    public PrecaArgumentValueException(string? paramName) : base(paramName) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="PrecaArgumentValueException"/> class with an error message and inner exception.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public PrecaArgumentValueException(string? message, Exception? innerException) : base(message, innerException) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="PrecaArgumentValueException"/> class with a parameter name and error message.
    /// </summary>
    /// <param name="paramName">The name of the invalid parameter.</param>
    /// <param name="message">The error message.</param>
    public PrecaArgumentValueException(string? paramName, string? message) : base(paramName, message) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="PrecaArgumentValueException"/> class with parameter name, actual value, and error message.
    /// </summary>
    /// <param name="paramName">The name of the invalid parameter.</param>
    /// <param name="actualValue">The actual invalid value.</param>
    /// <param name="message">The error message.</param>
    public PrecaArgumentValueException(string? paramName, object? actualValue, string? message)
        : base(paramName, actualValue, message) { }
}