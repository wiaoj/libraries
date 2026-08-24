namespace Wiaoj.Preconditions.Exceptions;

/// <summary>
/// Exception thrown when an argument value is outside the allowable range.
/// </summary>
public class PrecaArgumentOutOfRangeException : ArgumentOutOfRangeException {
    /// <summary>Initializes a new instance of the <see cref="PrecaArgumentOutOfRangeException"/> class.</summary>
    public PrecaArgumentOutOfRangeException() : base() { }

    /// <summary>Initializes a new instance of the <see cref="PrecaArgumentOutOfRangeException"/> class with a parameter name.</summary>
    /// <param name="paramName">The parameter name.</param>
    public PrecaArgumentOutOfRangeException(string? paramName) : base(paramName) { }

    /// <summary>Initializes a new instance of the <see cref="PrecaArgumentOutOfRangeException"/> class with a message and inner exception.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public PrecaArgumentOutOfRangeException(string? message, Exception? innerException) : base(message, innerException) { }

    /// <summary>Initializes a new instance of the <see cref="PrecaArgumentOutOfRangeException"/> class with a parameter name and message.</summary>
    /// <param name="paramName">The parameter name.</param>
    /// <param name="message">The error message.</param>
    public PrecaArgumentOutOfRangeException(string? paramName, string? message) : base(paramName, message) { }

    /// <summary>Initializes a new instance of the <see cref="PrecaArgumentOutOfRangeException"/> class with actual value details.</summary>
    /// <param name="paramName">The parameter name.</param>
    /// <param name="actualValue">The invalid actual value.</param>
    /// <param name="message">The error message.</param>
    public PrecaArgumentOutOfRangeException(string? paramName, object? actualValue, string? message) : base(paramName, actualValue, message) { }
}