namespace Wiaoj.Preconditions.Exceptions;
/// <summary>
/// Thrown when an operation is invalid for the current state of an object.
/// This is Preca's equivalent to InvalidOperationException with enhanced debugging information.
/// </summary>
public class PrecaInvalidOperationException : InvalidOperationException {
    /// <summary>
    /// Gets the context or operation name that caused the invalid operation.
    /// </summary>
    public string? OperationContext { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="PrecaInvalidOperationException"/> class.
    /// </summary>
    public PrecaInvalidOperationException() { }

    public PrecaInvalidOperationException(string? message) : base(message) { }

    public PrecaInvalidOperationException(string? message, Exception? innerException) : base(message, innerException) { }

    /// <summary>
    /// Initializes a new instance with operation context information.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="operationContext">The context or operation name that caused the error.</param>
    public PrecaInvalidOperationException(string? message, string? operationContext) : base(message) {
        OperationContext = operationContext;
    }

    /// <summary>
    /// Initializes a new instance with operation context and inner exception.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="operationContext">The context or operation name that caused the error.</param>
    /// <param name="innerException">The inner exception.</param>
    public PrecaInvalidOperationException(string? message, string? operationContext, Exception? innerException)
        : base(message, innerException) {
        OperationContext = operationContext;
    }
}