using System.Diagnostics.Contracts;

namespace Wiaoj.Results;

/// <summary>
/// <see cref="ValueTask"/> overloads of the core async combinators.
/// </summary>
/// <remarks>
/// <para>
/// Use these overloads when the underlying operation is expected to complete
/// synchronously in the common case — for example, in-memory lookups or
/// cache reads. <see cref="ValueTask"/> avoids a heap allocation when the
/// operation does not actually go async.
/// </para>
/// <para>
/// For I/O-bound operations that always go async, the <see cref="Task"/>-based
/// overloads in <see cref="ResultsExtensions"/> are preferable because
/// <see cref="ValueTask"/> is more expensive than <see cref="Task"/> in the
/// fully-async path.
/// </para>
/// <para>
/// This is a separate <c>static class</c> (not a <c>partial</c> of
/// <see cref="ResultsExtensions"/>) to prevent overload-resolution ambiguity
/// between the <see cref="Task"/>- and <see cref="ValueTask"/>-based overloads
/// when both receiver types are in scope.
/// </para>
/// </remarks>
public static class ResultsValueTaskExtensions {

    // ── ThenAsync ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Awaits a <see cref="ValueTask{TResult}"/> of <see cref="Result{TValue}"/>
    /// and, if successful, executes <paramref name="next"/>.
    /// </summary>
    /// <typeparam name="T">The value type of the incoming result.</typeparam>
    /// <typeparam name="TNext">The value type produced by <paramref name="next"/>.</typeparam>
    /// <param name="valueTask">The <see cref="ValueTask{TResult}"/> to await.</param>
    /// <param name="next">
    /// The async function to invoke when the result is successful.
    /// Must return a <see cref="Result{TValue}"/> so it can itself fail.
    /// </param>
    /// <param name="cancellationToken">
    /// Checked after <paramref name="valueTask"/> completes and before
    /// <paramref name="next"/> is invoked.
    /// </param>
    /// <returns>
    /// The <see cref="Result{TValue}"/> returned by <paramref name="next"/>, or
    /// the original errors if the incoming result was already a failure.
    /// </returns>
    [Pure]
    public static async ValueTask<Result<TNext>> ThenAsync<T, TNext>(
        this ValueTask<Result<T>> valueTask,
        Func<T, ValueTask<Result<TNext>>> next,
        CancellationToken cancellationToken = default) {

        Result<T> result = await valueTask.ConfigureAwait(false);
        if(result.IsFailure) return result.Errors.ToList();

        cancellationToken.ThrowIfCancellationRequested();
        return await next(result.Value).ConfigureAwait(false);
    }

    /// <summary>
    /// Awaits a <see cref="ValueTask{TResult}"/> of <see cref="Result{TValue}"/>
    /// and, if successful, executes <paramref name="next"/>, forwarding the
    /// <paramref name="cancellationToken"/>.
    /// </summary>
    /// <typeparam name="T">The value type of the incoming result.</typeparam>
    /// <typeparam name="TNext">The value type produced by <paramref name="next"/>.</typeparam>
    /// <param name="valueTask">The <see cref="ValueTask{TResult}"/> to await.</param>
    /// <param name="next">
    /// The async function to invoke when the result is successful.
    /// Receives the unwrapped value and a <see cref="CancellationToken"/>.
    /// </param>
    /// <param name="cancellationToken">
    /// Checked before invoking <paramref name="next"/> and forwarded to it.
    /// </param>
    /// <returns>
    /// The <see cref="Result{TValue}"/> returned by <paramref name="next"/>, or
    /// the original errors if the incoming result was already a failure.
    /// </returns>
    [Pure]
    public static async ValueTask<Result<TNext>> ThenAsync<T, TNext>(
        this ValueTask<Result<T>> valueTask,
        Func<T, CancellationToken, ValueTask<Result<TNext>>> next,
        CancellationToken cancellationToken = default) {

        Result<T> result = await valueTask.ConfigureAwait(false);
        if(result.IsFailure) return result.Errors.ToList();

        cancellationToken.ThrowIfCancellationRequested();
        return await next(result.Value, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// From a synchronous <see cref="Result{TValue}"/>, executes
    /// <paramref name="next"/> returning a <see cref="ValueTask{TResult}"/>.
    /// </summary>
    /// <typeparam name="T">The value type of the incoming result.</typeparam>
    /// <typeparam name="TNext">The value type produced by <paramref name="next"/>.</typeparam>
    /// <param name="result">The synchronous result to evaluate.</param>
    /// <param name="next">The async function to invoke when the result is successful.</param>
    /// <returns>
    /// The <see cref="Result{TValue}"/> returned by <paramref name="next"/>, or
    /// the original errors if <paramref name="result"/> was already a failure.
    /// </returns>
    [Pure]
    public static async ValueTask<Result<TNext>> ThenAsync<T, TNext>(
        this Result<T> result,
        Func<T, ValueTask<Result<TNext>>> next) {

        if(result.IsFailure) return result.Errors.ToList();
        return await next(result.Value).ConfigureAwait(false);
    }

    /// <summary>
    /// From a synchronous <see cref="Result{TValue}"/>, executes
    /// <paramref name="next"/> returning a <see cref="ValueTask{TResult}"/>,
    /// forwarding the <paramref name="cancellationToken"/>.
    /// </summary>
    /// <typeparam name="T">The value type of the incoming result.</typeparam>
    /// <typeparam name="TNext">The value type produced by <paramref name="next"/>.</typeparam>
    /// <param name="result">The synchronous result to evaluate.</param>
    /// <param name="next">
    /// The async function to invoke when the result is successful.
    /// Receives the unwrapped value and a <see cref="CancellationToken"/>.
    /// </param>
    /// <param name="cancellationToken">Forwarded to <paramref name="next"/>.</param>
    /// <returns>
    /// The <see cref="Result{TValue}"/> returned by <paramref name="next"/>, or
    /// the original errors if <paramref name="result"/> was already a failure.
    /// </returns>
    [Pure]
    public static async ValueTask<Result<TNext>> ThenAsync<T, TNext>(
        this Result<T> result,
        Func<T, CancellationToken, ValueTask<Result<TNext>>> next,
        CancellationToken cancellationToken = default) {

        if(result.IsFailure) return result.Errors.ToList();
        return await next(result.Value, cancellationToken).ConfigureAwait(false);
    }

    // ── MatchAsync ────────────────────────────────────────────────────────────

    /// <summary>
    /// Awaits a <see cref="ValueTask{TResult}"/> of <see cref="Result{TValue}"/>
    /// and applies synchronous match functions to produce a value of
    /// <typeparamref name="TResult"/>.
    /// </summary>
    /// <typeparam name="T">The value type of the result.</typeparam>
    /// <typeparam name="TResult">The output type of both match functions.</typeparam>
    /// <param name="valueTask">The <see cref="ValueTask{TResult}"/> to await.</param>
    /// <param name="onValue">Invoked with the unwrapped value when the result is successful.</param>
    /// <param name="onError">Invoked with the error list when the result is a failure.</param>
    /// <param name="cancellationToken">
    /// Checked after the task completes and before the match functions are called.
    /// </param>
    /// <returns>The value returned by whichever branch was executed.</returns>
    [Pure]
    public static async ValueTask<TResult> MatchAsync<T, TResult>(
        this ValueTask<Result<T>> valueTask,
        Func<T, TResult> onValue,
        Func<IReadOnlyList<Error>, TResult> onError,
        CancellationToken cancellationToken = default) {

        Result<T> result = await valueTask.ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return result.Match(onValue, onError);
    }

    /// <summary>
    /// Awaits a <see cref="ValueTask{TResult}"/> of <see cref="Result{TValue}"/>
    /// and applies asynchronous match functions returning
    /// <see cref="ValueTask{TResult}"/>.
    /// </summary>
    /// <typeparam name="T">The value type of the result.</typeparam>
    /// <typeparam name="TResult">The output type of both match functions.</typeparam>
    /// <param name="valueTask">The <see cref="ValueTask{TResult}"/> to await.</param>
    /// <param name="onValue">Async function invoked when the result is successful.</param>
    /// <param name="onError">Async function invoked when the result is a failure.</param>
    /// <param name="cancellationToken">
    /// Checked after the task completes and before the chosen branch is executed.
    /// </param>
    /// <returns>The value produced by whichever async branch was executed.</returns>
    [Pure]
    public static async ValueTask<TResult> MatchAsync<T, TResult>(
        this ValueTask<Result<T>> valueTask,
        Func<T, ValueTask<TResult>> onValue,
        Func<IReadOnlyList<Error>, ValueTask<TResult>> onError,
        CancellationToken cancellationToken = default) {

        Result<T> result = await valueTask.ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        return result.IsFailure
            ? await onError(result.Errors).ConfigureAwait(false)
            : await onValue(result.Value).ConfigureAwait(false);
    }

    // ── MapAsync ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Awaits a <see cref="ValueTask{TResult}"/> of <see cref="Result{TValue}"/>
    /// and, if successful, transforms the value with a synchronous
    /// <paramref name="mapper"/>.
    /// The mapper cannot itself fail — use
    /// <see cref="ThenAsync{T, TNext}(ValueTask{Result{T}}, Func{T, ValueTask{Result{TNext}}}, CancellationToken)"/>
    /// when the transformation may produce an error.
    /// </summary>
    /// <typeparam name="T">The value type of the incoming result.</typeparam>
    /// <typeparam name="TNew">The type produced by <paramref name="mapper"/>.</typeparam>
    /// <param name="valueTask">The <see cref="ValueTask{TResult}"/> to await.</param>
    /// <param name="mapper">A synchronous, infallible transformation function.</param>
    /// <returns>
    /// A successful <see cref="Result{TValue}"/> containing the mapped value,
    /// or the original errors.
    /// </returns>
    [Pure]
    public static async ValueTask<Result<TNew>> MapAsync<T, TNew>(
        this ValueTask<Result<T>> valueTask,
        Func<T, TNew> mapper) {

        Result<T> result = await valueTask.ConfigureAwait(false);
        if(result.IsFailure) return result.Errors.ToList();
        return Result.Success(mapper(result.Value));
    }

    /// <summary>
    /// Awaits a <see cref="ValueTask{TResult}"/> of <see cref="Result{TValue}"/>
    /// and, if successful, transforms the value with an async
    /// <paramref name="mapper"/> returning a <see cref="ValueTask{TResult}"/>.
    /// </summary>
    /// <typeparam name="T">The value type of the incoming result.</typeparam>
    /// <typeparam name="TNew">The type produced by <paramref name="mapper"/>.</typeparam>
    /// <param name="valueTask">The <see cref="ValueTask{TResult}"/> to await.</param>
    /// <param name="mapper">An async, infallible transformation function.</param>
    /// <returns>
    /// A successful <see cref="Result{TValue}"/> containing the mapped value,
    /// or the original errors.
    /// </returns>
    [Pure]
    public static async ValueTask<Result<TNew>> MapAsync<T, TNew>(
        this ValueTask<Result<T>> valueTask,
        Func<T, ValueTask<TNew>> mapper) {

        Result<T> result = await valueTask.ConfigureAwait(false);
        if(result.IsFailure) return result.Errors.ToList();
        return Result.Success(await mapper(result.Value).ConfigureAwait(false));
    }

    // ── AsValueTask ───────────────────────────────────────────────────────────

    /// <summary>
    /// Wraps <paramref name="result"/> in a completed
    /// <see cref="ValueTask{TResult}"/>.
    /// Useful when an interface requires a <see cref="ValueTask{TResult}"/> but the
    /// value is already available synchronously.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="result">The result to wrap.</param>
    /// <returns>
    /// A completed <see cref="ValueTask{TResult}"/> containing
    /// <paramref name="result"/>.
    /// </returns>
    public static ValueTask<Result<T>> AsValueTask<T>(this Result<T> result)
        => ValueTask.FromResult(result);

    /// <summary>
    /// Converts a <see cref="Task{TResult}"/> of <see cref="Result{TValue}"/> to a
    /// <see cref="ValueTask{TResult}"/> of <see cref="Result{TValue}"/>.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="task">The task to convert.</param>
    /// <returns>
    /// A <see cref="ValueTask{TResult}"/> backed by <paramref name="task"/>.
    /// </returns>
    public static ValueTask<Result<T>> AsValueTask<T>(this Task<Result<T>> task)
        => new(task);
}