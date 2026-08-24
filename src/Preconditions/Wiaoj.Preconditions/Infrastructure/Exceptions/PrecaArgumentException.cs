namespace Wiaoj.Preconditions.Exceptions;

/// <summary>
/// Exception thrown when an argument does not meet the required precondition.
/// </summary>
public class PrecaArgumentException : ArgumentException {
    /// <summary>Initializes a new instance of the <see cref="PrecaArgumentException"/> class.</summary>
    public PrecaArgumentException() : base() { }

    /// <summary>Initializes a new instance of the <see cref="PrecaArgumentException"/> class with a message.</summary>
    /// <param name="message">The error message.</param>
    public PrecaArgumentException(string? message) : base(message) { }

    /// <summary>Initializes a new instance of the <see cref="PrecaArgumentException"/> class with a message and inner exception.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public PrecaArgumentException(string? message, Exception? innerException) : base(message, innerException) { }

    /// <summary>Initializes a new instance of the <see cref="PrecaArgumentException"/> class with a message and parameter name.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="paramName">The invalid parameter name.</param>
    public PrecaArgumentException(string? message, string? paramName) : base(message, paramName) { }

    /// <summary>Initializes a new instance of the <see cref="PrecaArgumentException"/> class with all details.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="paramName">The invalid parameter name.</param>
    /// <param name="innerException">The inner exception.</param>
    public PrecaArgumentException(string? message, string? paramName, Exception? innerException) : base(message, paramName, innerException) { }
}