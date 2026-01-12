namespace Wiaoj.Results;
/// <summary>
/// Defines a common contract for operation results, allowing polymorphic access to success/failure states.
/// This interface is particularly useful for middleware or pipeline behaviors (e.g., MediatR) 
/// where the generic type of the result is not known at compile time.
/// </summary>
public interface IResult : IDisposable {
    /// <summary>
    /// Gets a value indicating whether the result represents a failure.
    /// </summary>
    /// <value>
    /// <c>true</c> if the operation failed and contains errors; otherwise, <c>false</c>.
    /// </value>
    bool IsError { get; }

    /// <summary>
    /// Gets a value indicating whether the result represents a success.
    /// </summary>
    /// <value>
    /// <c>true</c> if the operation succeeded; otherwise, <c>false</c>.
    /// </value>
    bool IsSuccess { get; }

    /// <summary>
    /// Gets the first error of a failed operation.
    /// </summary>
    /// <value>The first <see cref="Error"/> in the error list.</value>
    /// <exception cref="InvalidOperationException">
    /// Thrown if accessed when <see cref="IsSuccess"/> is <c>true</c>.
    /// </exception>
    Error FirstError { get; }

    /// <summary>
    /// Gets the list of errors. 
    /// </summary>
    /// <value>
    /// A read-only list of <see cref="Error"/> objects. Returns an empty list if the result is successful.
    /// </value>
    IReadOnlyList<Error> Errors { get; }
}