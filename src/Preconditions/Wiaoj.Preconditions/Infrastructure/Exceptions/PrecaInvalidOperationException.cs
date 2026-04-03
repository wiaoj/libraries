namespace Wiaoj.Preconditions.Exceptions;
/// <summary>
/// Thrown when an operation is invalid for the current state of an object.
/// This is Preca's equivalent to <see cref="InvalidOperationException"/> with enhanced debugging information.
/// </summary>
public class PrecaInvalidOperationException : InvalidOperationException {

    /// <summary>
    /// Gets the context or operation name that caused the invalid operation.
    /// </summary>
    public string? OperationContext { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="PrecaInvalidOperationException"/> class.
    /// </summary>
    public PrecaInvalidOperationException()
        : base("Operation is not valid due to the current state of the object.") { }

    /// <summary>
    /// Initializes a new instance of the <see cref="PrecaInvalidOperationException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public PrecaInvalidOperationException(string? message)
        : base(message) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="PrecaInvalidOperationException"/> class with a specified error message 
    /// and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception, or a null reference if no inner exception is specified.</param>
    public PrecaInvalidOperationException(string? message, Exception? innerException)
        : base(message, innerException) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="PrecaInvalidOperationException"/> class with a specified error message and operation context information.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="operationContext">The context or operation name that caused the error.</param>
    public PrecaInvalidOperationException(string? message, string? operationContext)
        : base(message) {
        this.OperationContext = operationContext;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PrecaInvalidOperationException"/> class with a specified error message, operation context, and inner exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="operationContext">The context or operation name that caused the error.</param>
    /// <param name="innerException">The exception that is the cause of the current exception, or a null reference if no inner exception is specified.</param>
    public PrecaInvalidOperationException(string? message, string? operationContext, Exception? innerException)
        : base(message, innerException) {
        this.OperationContext = operationContext;
    }
}