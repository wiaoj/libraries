namespace Wiaoj.Results;

public static partial class ResultsExtensions {

    // ── Ensure (sync) ─────────────────────────────────────────────────────────

    /// <summary>
    /// Validates a condition against the value.
    /// Returns <paramref name="error"/> when <paramref name="predicate"/> is <c>false</c>.
    /// Has no effect when the result is already a failure.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="result">The result to validate.</param>
    /// <param name="predicate">
    /// The condition to check. Receives the unwrapped value.
    /// Not called when <paramref name="result"/> is already a failure.
    /// </param>
    /// <param name="error">The error to return when <paramref name="predicate"/> is <c>false</c>.</param>
    /// <returns>
    /// The original <paramref name="result"/> when successful and the predicate holds;
    /// <paramref name="error"/> when the predicate fails;
    /// the original errors when the result was already a failure.
    /// </returns>
    public static Result<T> Ensure<T>(
        this Result<T> result,
        Func<T, bool> predicate,
        Error error) {

        if(result.IsError) return result;
        if(!predicate(result.Value)) return error;
        return result;
    }

    /// <summary>
    /// Validates a value-independent condition.
    /// Returns <paramref name="error"/> when <paramref name="predicate"/> is <c>false</c>.
    /// Has no effect when the result is already a failure.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="result">The result to validate.</param>
    /// <param name="predicate">
    /// A parameterless condition. Use when the check does not depend on the value,
    /// e.g., a feature-flag check or a time-window constraint.
    /// </param>
    /// <param name="error">The error to return when <paramref name="predicate"/> is <c>false</c>.</param>
    /// <returns>
    /// The original <paramref name="result"/> when successful and the predicate holds;
    /// <paramref name="error"/> when the predicate fails;
    /// the original errors when the result was already a failure.
    /// </returns>
    public static Result<T> Ensure<T>(
        this Result<T> result,
        Func<bool> predicate,
        Error error) {

        if(result.IsError) return result;
        if(!predicate()) return error;
        return result;
    }

    // ── EnsureAsync ───────────────────────────────────────────────────────────

    /// <summary>
    /// Awaits <paramref name="task"/> and validates the value with a synchronous
    /// <paramref name="predicate"/>.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="task">The task to await.</param>
    /// <param name="predicate">
    /// The condition to check. Not called when the result is already a failure.
    /// </param>
    /// <param name="error">The error to return when <paramref name="predicate"/> is <c>false</c>.</param>
    /// <returns>
    /// The original result when successful and the predicate holds;
    /// <paramref name="error"/> when the predicate fails;
    /// the original errors when the result was already a failure.
    /// </returns>
    public static async Task<Result<T>> EnsureAsync<T>(
        this Task<Result<T>> task,
        Func<T, bool> predicate,
        Error error) {

        Result<T> result = await task.ConfigureAwait(false);
        if(result.IsError) return result;
        if(!predicate(result.Value)) return error;
        return result;
    }

    /// <summary>
    /// Awaits <paramref name="task"/> and validates the value with an asynchronous
    /// <paramref name="predicate"/>.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="task">The task to await.</param>
    /// <param name="predicate">
    /// An async condition, e.g., a uniqueness check against the database.
    /// Not called when the result is already a failure.
    /// </param>
    /// <param name="error">The error to return when <paramref name="predicate"/> is <c>false</c>.</param>
    /// <returns>
    /// The original result when successful and the predicate holds;
    /// <paramref name="error"/> when the predicate fails;
    /// the original errors when the result was already a failure.
    /// </returns>
    public static async Task<Result<T>> EnsureAsync<T>(
        this Task<Result<T>> task,
        Func<T, Task<bool>> predicate,
        Error error) {

        Result<T> result = await task.ConfigureAwait(false);
        if(result.IsError) return result;
        if(!await predicate(result.Value).ConfigureAwait(false)) return error;
        return result;
    }

    /// <summary>
    /// From a synchronous <see cref="Result{TValue}"/>, validates the value with an
    /// asynchronous <paramref name="predicate"/>.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="result">The synchronous result to validate.</param>
    /// <param name="predicate">An async condition. Not called when the result is already a failure.</param>
    /// <param name="error">The error to return when <paramref name="predicate"/> is <c>false</c>.</param>
    /// <returns>
    /// The original result when the predicate holds;
    /// <paramref name="error"/> when it fails;
    /// the original errors when the result was already a failure.
    /// </returns>
    public static async Task<Result<T>> EnsureAsync<T>(
        this Result<T> result,
        Func<T, Task<bool>> predicate,
        Error error) {

        if(result.IsError) return result;
        if(!await predicate(result.Value).ConfigureAwait(false)) return error;
        return result;
    }

    /// <summary>
    /// Awaits <paramref name="task"/> and validates the value using an async
    /// <paramref name="predicate"/>. When the predicate fails, produces the error
    /// dynamically via <paramref name="errorFactory"/>.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="task">The task to await.</param>
    /// <param name="predicate">An async condition. Not called when the result is already a failure.</param>
    /// <param name="errorFactory">
    /// An async function that receives the value and produces a contextual error.
    /// Only called when <paramref name="predicate"/> returns <c>false</c>.
    /// </param>
    /// <returns>
    /// The original result when the predicate holds;
    /// the error produced by <paramref name="errorFactory"/> when it fails;
    /// the original errors when the result was already a failure.
    /// </returns>
    public static async Task<Result<T>> EnsureAsync<T>(
        this Task<Result<T>> task,
        Func<T, Task<bool>> predicate,
        Func<T, Task<Error>> errorFactory) {

        Result<T> result = await task.ConfigureAwait(false);
        if(result.IsError) return result;

        if(!await predicate(result.Value).ConfigureAwait(false))
            return await errorFactory(result.Value).ConfigureAwait(false);

        return result;
    }
}