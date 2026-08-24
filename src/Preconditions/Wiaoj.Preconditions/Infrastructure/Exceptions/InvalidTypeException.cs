namespace Wiaoj.Preconditions.Exceptions;

/// <summary>
/// Thrown when an argument is not of the expected type.
/// </summary>
[StackTraceHidden]
public sealed class PrecaInvalidTypeException : ArgumentException {
    /// <summary>
    /// Gets the name of the expected type.
    /// </summary>
    public string? ExpectedType { get; }

    /// <summary>
    /// Gets the name of the actual type encountered.
    /// </summary>
    public string? ActualType { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="PrecaInvalidTypeException"/> class.
    /// </summary>
    public PrecaInvalidTypeException()
        : base("The argument is not of the expected type.") { }

    /// <summary>
    /// Initializes a new instance of the <see cref="PrecaInvalidTypeException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The error message.</param>
    public PrecaInvalidTypeException(string? message)
        : base(message) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="PrecaInvalidTypeException"/> class with a message and parameter name.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="paramName">The name of the parameter that caused the exception.</param>
    public PrecaInvalidTypeException(string? message, string? paramName)
        : base(message, paramName) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="PrecaInvalidTypeException"/> class with parameter name, expected type, and actual type details.
    /// </summary>
    /// <param name="paramName">The name of the parameter that caused the exception.</param>
    /// <param name="expectedType">The name of the expected type.</param>
    /// <param name="actualType">The name of the actual type.</param>
    public PrecaInvalidTypeException(string? paramName, string? expectedType, string? actualType)
        : base($"Argument '{paramName}' must be of type '{expectedType}', but was '{actualType}'.", paramName) {
        this.ExpectedType = expectedType;
        this.ActualType = actualType;
    }
}