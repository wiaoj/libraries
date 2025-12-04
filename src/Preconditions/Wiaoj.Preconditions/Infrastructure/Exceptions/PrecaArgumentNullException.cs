namespace Wiaoj.Preconditions.Exceptions;

/// <summary>
/// Thrown when a null argument is passed to a Preca guard clause. 
/// Inherits from <see cref="ArgumentNullException"/> for compatibility.
/// </summary>
public class PrecaArgumentNullException : ArgumentNullException {
    public PrecaArgumentNullException() { }

    public PrecaArgumentNullException(string? paramName) : base(paramName) { }

    public PrecaArgumentNullException(string? message, Exception? innerException) : base(message, innerException) { }

    public PrecaArgumentNullException(string? paramName, string? message) : base(paramName, message) { }
}