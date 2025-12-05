namespace Wiaoj.Results;     
/// <summary>
/// Provides extension methods for handling asynchronous operations with <see cref="Result{TValue}"/>.
/// </summary>
public static class ResultsExtensions {

    /// <summary>
    /// Asynchronously awaits a result and, if successful, executes the next asynchronous operation.
    /// If the initial result is an error, the errors are propagated and the next step is skipped.
    /// </summary>
    /// <typeparam name="T">The type of the current value.</typeparam>
    /// <typeparam name="TNext">The type of the next value.</typeparam>
    /// <param name="task">The task representing the current result.</param>
    /// <param name="next">The asynchronous function to execute if the current result is success.</param>
    /// <returns>A task representing the result of the chain.</returns>
    public static async Task<Result<TNext>> ThenAsync<T, TNext>(
        this Task<Result<T>> task,
        Func<T, Task<Result<TNext>>> next) {

        Result<T> result = await task.ConfigureAwait(false);
        if (result.IsError) {
            return result.Errors.ToList();
        }

        return await next(result.Value).ConfigureAwait(false);
    }

    /// <summary>
    /// Chains an asynchronous operation to a synchronous result.
    /// </summary>
    /// <typeparam name="T">The type of the current value.</typeparam>
    /// <typeparam name="TNext">The type of the next value.</typeparam>
    /// <param name="result">The current synchronous result.</param>
    /// <param name="next">The asynchronous function to execute if the current result is success.</param>
    /// <returns>A task representing the result of the chain.</returns>
    public static async Task<Result<TNext>> ThenAsync<T, TNext>(
        this Result<T> result,
        Func<T, Task<Result<TNext>>> next) {

        if (result.IsError) {
            return result.Errors.ToList();
        }

        return await next(result.Value).ConfigureAwait(false);
    }

    /// <summary>
    /// Asynchronously matches the result to a value or an error handler.
    /// </summary>
    /// <typeparam name="T">The type of the result value.</typeparam>
    /// <typeparam name="TResult">The return type of the match.</typeparam>
    /// <param name="task">The task representing the result.</param>
    /// <param name="onValue">The function to execute if successful.</param>
    /// <param name="onError">The function to execute if failed.</param>
    /// <returns>The result of the match operation.</returns>
    public static async Task<TResult> MatchAsync<T, TResult>(
        this Task<Result<T>> task,
        Func<T, TResult> onValue,
        Func<IReadOnlyList<Error>, TResult> onError) {

        Result<T> result = await task.ConfigureAwait(false);
        return result.Match(onValue, onError);
    }

    /// <summary>
    /// Asynchronously matches the result to an asynchronous value or error handler.
    /// </summary>
    /// <typeparam name="T">The type of the result value.</typeparam>
    /// <typeparam name="TResult">The return type of the match.</typeparam>
    /// <param name="task">The task representing the result.</param>
    /// <param name="onValue">The asynchronous function to execute if successful.</param>
    /// <param name="onError">The asynchronous function to execute if failed.</param>
    /// <returns>The result of the match operation.</returns>
    public static async Task<TResult> MatchAsync<T, TResult>(
        this Task<Result<T>> task,
        Func<T, Task<TResult>> onValue,
        Func<IReadOnlyList<Error>, Task<TResult>> onError) {

        Result<T> result = await task.ConfigureAwait(false);
        if (result.IsError) {
            return await onError(result.Errors).ConfigureAwait(false);
        }

        return await onValue(result.Value).ConfigureAwait(false);
    }
}