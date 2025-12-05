namespace Wiaoj.Results;     
/// <summary>
/// Provides static factory methods to create <see cref="Result{TValue}"/> instances.
/// Useful for creating results without explicitly specifying the generic type.
/// </summary>
public static class Result {
    /// <summary>
    /// Creates a successful result for an operation that does not return a value.
    /// </summary>
    /// <returns>A <see cref="Result{Success}"/> representing success.</returns>
    public static Result<Success> Success() => Wiaoj.Results.Success.Default;

    /// <summary>
    /// Creates a successful result containing the specified value.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="value">The value to wrap.</param>
    /// <returns>A <see cref="Result{T}"/> containing the value.</returns>
    public static Result<T> Success<T>(T value) => value;

    /// <summary>
    /// Creates a failed result from a single error.
    /// </summary>
    /// <param name="error">The error.</param>
    /// <returns>A <see cref="Result{Success}"/> containing the error.</returns>
    public static Result<Success> Failure(Error error) => error;

    /// <summary>
    /// Creates a failed result from a list of errors.
    /// </summary>
    /// <param name="errors">The list of errors.</param>
    /// <returns>A <see cref="Result{Success}"/> containing the errors.</returns>
    public static Result<Success> Failure(List<Error> errors) => errors;

    /// <summary>
    /// Creates a failed result for a specific generic type.
    /// Useful when you need to return a <see cref="Result{T}"/> from a method that failed.
    /// </summary>
    /// <typeparam name="T">The expected value type of the result.</typeparam>
    /// <param name="error">The error.</param>
    /// <returns>A <see cref="Result{T}"/> containing the error.</returns>
    public static Result<T> Failure<T>(Error error) => error;
}