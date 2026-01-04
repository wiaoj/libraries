namespace Wiaoj.Preconditions.Exceptions;
/// <summary>
/// Thrown when an argument is not of the expected type.
/// </summary>
[StackTraceHidden]
public sealed class PrecaInvalidTypeException : ArgumentException {
    public string? ExpectedType { get; }
    public string? ActualType { get; }

    public PrecaInvalidTypeException()
        : base("The argument is not of the expected type.") { }

    public PrecaInvalidTypeException(string? message)
        : base(message) { }

    public PrecaInvalidTypeException(string? message, string? paramName)
        : base(message, paramName) { }

    public PrecaInvalidTypeException(string? paramName, string? expectedType, string? actualType)
        : base($"Argument '{paramName}' must be of type '{expectedType}', but was '{actualType}'.", paramName) {
        this.ExpectedType = expectedType;
        this.ActualType = actualType;
    }
}