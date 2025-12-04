namespace Wiaoj.Preconditions.Exceptions;
/// <summary>
/// Thrown when an argument's value is outside the allowable range for a Preca guard clause.
/// Inherits from ArgumentOutOfRangeException for compatibility.
/// </summary>
public class PrecaArgumentOutOfRangeException : ArgumentOutOfRangeException {
    public PrecaArgumentOutOfRangeException() { }

    public PrecaArgumentOutOfRangeException(string? paramName) : base(paramName) { }

    public PrecaArgumentOutOfRangeException(string? message, Exception? innerException) : base(message, innerException) { }

    public PrecaArgumentOutOfRangeException(string? paramName, string? message) : base(paramName, message) { }

    public PrecaArgumentOutOfRangeException(string? paramName, object? actualValue, string? message)
        : base(paramName, actualValue, message) { }
}