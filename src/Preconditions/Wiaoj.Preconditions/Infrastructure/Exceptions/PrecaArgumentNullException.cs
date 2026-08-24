namespace Wiaoj.Preconditions.Exceptions;

/// <summary>
/// Exception thrown when a required argument is null.
/// </summary>
public class PrecaArgumentNullException : ArgumentNullException {
    /// <summary>Initializes a new instance of the <see cref="PrecaArgumentNullException"/> class.</summary>
    public PrecaArgumentNullException() : base() { }

    /// <summary>Initializes a new instance of the <see cref="PrecaArgumentNullException"/> class with a parameter name.</summary>
    /// <param name="paramName">The name of the null parameter.</param>
    public PrecaArgumentNullException(string? paramName) : base(paramName) { }

    /// <summary>Initializes a new instance of the <see cref="PrecaArgumentNullException"/> class with a message and inner exception.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public PrecaArgumentNullException(string? message, Exception? innerException) : base(message, innerException) { }

    /// <summary>Initializes a new instance of the <see cref="PrecaArgumentNullException"/> class with a parameter name and message.</summary>
    /// <param name="paramName">The name of the null parameter.</param>
    /// <param name="message">The error message.</param>
    public PrecaArgumentNullException(string? paramName, string? message) : base(paramName, message) { }
}