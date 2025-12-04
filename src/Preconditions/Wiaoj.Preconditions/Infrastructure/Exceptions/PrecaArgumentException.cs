namespace Wiaoj.Preconditions.Exceptions;
/// <summary>
/// Represents a base class for exceptions thrown by Wiaoj.Preca guard clauses related to arguments.
/// Inherits from ArgumentException to ensure compatibility with standard error handling patterns.
/// </summary>
public class PrecaArgumentException : ArgumentException {
    public PrecaArgumentException() { }

    public PrecaArgumentException(string? message) : base(message) { }

    public PrecaArgumentException(string? message, Exception? innerException) : base(message, innerException) { }

    public PrecaArgumentException(string? message, string? paramName) : base(message, paramName) { }

    public PrecaArgumentException(string? message, string? paramName, Exception? innerException)
        : base(message, paramName, innerException) { }
}