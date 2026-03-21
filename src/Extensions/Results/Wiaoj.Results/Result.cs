namespace Wiaoj.Results; 
/// <summary>
/// Provides static factory methods to create <see cref="Result{TValue}"/> instances.
/// <para>
/// Extended by:
/// <list type="bullet">
///   <item><description><c>Result.Try.cs</c> — wrapping exceptions as <see cref="Result{TValue}"/>.</description></item>
///   <item><description><c>Result.Combine.cs</c> — aggregating multiple results.</description></item>
/// </list>
/// </para>
/// </summary>
public static partial class Result {
    /// <summary>
    /// Creates a successful <see cref="Result{TValue}"/> of <see cref="Success"/>
    /// for operations that return no value.
    /// </summary>
    public static Result<Success> Success() {
        return Wiaoj.Results.Success.Default;
    }

    /// <summary>
    /// Creates a successful <see cref="Result{TValue}"/> containing
    /// <paramref name="value"/>.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="value">The value to wrap.</param>
    public static Result<T> Success<T>(T value) {
        return value;
    }

    /// <summary>
    /// Creates a failed <see cref="Result{TValue}"/> of <see cref="Success"/>
    /// from a single <paramref name="error"/>.
    /// </summary>
    public static Result<Success> Failure(Error error) {
        return error;
    }

    /// <summary>
    /// Creates a failed <see cref="Result{TValue}"/> of <see cref="Success"/>
    /// from a list of errors.
    /// </summary>
    public static Result<Success> Failure(List<Error> errors) {
        return errors;
    }

    /// <summary>
    /// Creates a failed <see cref="Result{TValue}"/> of <typeparamref name="T"/>
    /// from a single <paramref name="error"/>.
    /// Useful when a method returns <see cref="Result{TValue}"/> but needs to
    /// propagate an error without having a value to return.
    /// </summary>
    public static Result<T> Failure<T>(Error error) {
        return error;
    }
}