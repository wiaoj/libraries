namespace Wiaoj.Results; 
/// <summary>
/// Defines a common contract for operation results, allowing polymorphic access
/// to success/failure states without knowing the generic value type at compile time.
/// <para>
/// Particularly useful in pipeline behaviors or middleware where
/// the concrete <see cref="Result{TValue}"/>. type is not known.
/// </para>
/// </summary>
public interface IResult {
    /// <summary>
    /// Gets a value indicating whether the result represents a failure.
    /// </summary>
    bool IsFailure { get; }

    /// <summary>
    /// Gets a value indicating whether the result represents a success.
    /// </summary>
    bool IsSuccess { get; }

    /// <summary>
    /// Gets the first error of a failed operation.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown if accessed when <see cref="IsSuccess"/> is <c>true</c>.
    /// </exception>
    Error FirstError { get; }

    /// <summary>
    /// Gets the list of errors.
    /// Returns an empty list if the result is successful.
    /// </summary>
    IReadOnlyList<Error> Errors { get; }
}