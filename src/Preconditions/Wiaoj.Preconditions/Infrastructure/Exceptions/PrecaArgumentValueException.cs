namespace Wiaoj.Preconditions.Exceptions;
/// <summary>
/// Thrown when an argument has a specific, disallowed value (e.g., MaxValue, MinValue).
/// Inherits from ArgumentOutOfRangeException for semantic correctness and compatibility.
/// </summary>
public class PrecaArgumentValueException : ArgumentOutOfRangeException {
    public PrecaArgumentValueException() { }

    public PrecaArgumentValueException(string? paramName) : base(paramName) { }

    public PrecaArgumentValueException(string? message, Exception? innerException) : base(message, innerException) { }

    public PrecaArgumentValueException(string? paramName, string? message) : base(paramName, message) { }

    public PrecaArgumentValueException(string? paramName, object? actualValue, string? message)
        : base(paramName, actualValue, message) { }
}