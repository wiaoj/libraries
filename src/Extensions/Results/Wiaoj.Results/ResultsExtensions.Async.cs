using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;

namespace Wiaoj.Results;

/// <summary>
/// Async extension methods for chaining and matching on <see cref="Result{TValue}"/>.
/// </summary>
/// <remarks>
/// <para>
/// Every method in this class follows one of two signatures based on where the
/// result comes from:
/// </para>
/// <list type="bullet">
///   <item><description>
///     <c>this Task&lt;Result&lt;T&gt;&gt;</c> — the left side is already async;
///     the method awaits it before deciding whether to continue.
///   </description></item>
///   <item><description>
///     <c>this Result&lt;T&gt;</c> — the left side is synchronous;
///     the method executes the right-side async function immediately.
///   </description></item>
/// </list>
/// <para>
/// All overloads short-circuit on error: if the incoming result is already a failure
/// the next function is never called and the errors are propagated as-is.
/// </para>
/// </remarks>
public static partial class ResultsExtensions {

    // ── ThenAsync ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Awaits <paramref name="task"/> and, if successful, executes <paramref name="next"/>.
    /// Equivalent to an async <c>Bind</c> / <c>FlatMap</c> on <see cref="Result{TValue}"/>.
    /// </summary>
    /// <typeparam name="T">The value type of the incoming result.</typeparam>
    /// <typeparam name="TNext">The value type produced by <paramref name="next"/>.</typeparam>
    /// <param name="task">The task to await.</param>
    /// <param name="next">
    /// The async function to invoke when the result is successful.
    /// Receives the unwrapped value. Must return a <see cref="Result{TValue}"/>
    /// so it can itself fail.
    /// </param>
    /// <param name="cancellationToken">
    /// Checked after <paramref name="task"/> completes and before <paramref name="next"/>
    /// is invoked. Does not cancel an already-running <paramref name="next"/>.
    /// </param>
    /// <returns>
    /// The <see cref="Result{TValue}"/> returned by <paramref name="next"/>, or
    /// the original errors if the incoming result was already a failure.
    /// </returns>
    [Pure]
    public static async Task<Result<TNext>> ThenAsync<T, TNext>(
        this Task<Result<T>> task,
        Func<T, Task<Result<TNext>>> next,
        CancellationToken cancellationToken = default) {

        Result<T> result = await task.ConfigureAwait(false);
        if(result.IsFailure) return result.Errors.ToList();

        cancellationToken.ThrowIfCancellationRequested();
        return await next(result.Value).ConfigureAwait(false);
    }

    /// <summary>
    /// Awaits <paramref name="task"/> and, if successful, executes <paramref name="next"/>,
    /// passing both the unwrapped value and the <paramref name="cancellationToken"/>.
    /// </summary>
    /// <typeparam name="T">The value type of the incoming result.</typeparam>
    /// <typeparam name="TNext">The value type produced by <paramref name="next"/>.</typeparam>
    /// <param name="task">The task to await.</param>
    /// <param name="next">
    /// The async function to invoke when the result is successful.
    /// Receives the unwrapped value and a <see cref="CancellationToken"/>.
    /// </param>
    /// <param name="cancellationToken">
    /// Forwarded to <paramref name="next"/> and checked before invocation.
    /// </param>
    /// <returns>
    /// The <see cref="Result{TValue}"/> returned by <paramref name="next"/>, or
    /// the original errors if the incoming result was already a failure.
    /// </returns>
    [Pure]
    public static async Task<Result<TNext>> ThenAsync<T, TNext>(
        this Task<Result<T>> task,
        Func<T, CancellationToken, Task<Result<TNext>>> next,
        CancellationToken cancellationToken = default) {

        Result<T> result = await task.ConfigureAwait(false);
        if(result.IsFailure) return result.Errors.ToList();

        cancellationToken.ThrowIfCancellationRequested();
        return await next(result.Value, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Awaits <paramref name="task"/> and, if successful, executes the synchronous
    /// <paramref name="next"/>.
    /// </summary>
    /// <typeparam name="T">The value type of the incoming result.</typeparam>
    /// <typeparam name="TNext">The value type produced by <paramref name="next"/>.</typeparam>
    /// <param name="task">The task to await.</param>
    /// <param name="next">
    /// A synchronous function returning a <see cref="Result{TValue}"/>.
    /// Useful when the next step itself does not need to be async but the
    /// incoming result is still on a <see cref="Task"/>.
    /// </param>
    /// <returns>
    /// The <see cref="Result{TValue}"/> returned by <paramref name="next"/>, or
    /// the original errors if the incoming result was already a failure.
    /// </returns>
    [Pure]
    public static async Task<Result<TNext>> ThenAsync<T, TNext>(
        this Task<Result<T>> task,
        Func<T, Result<TNext>> next) {

        Result<T> result = await task.ConfigureAwait(false);
        if(result.IsFailure) return result.Errors.ToList();
        return next(result.Value);
    }

    /// <summary>
    /// From a synchronous <see cref="Result{TValue}"/>, executes <paramref name="next"/>
    /// asynchronously.
    /// </summary>
    /// <typeparam name="T">The value type of the incoming result.</typeparam>
    /// <typeparam name="TNext">The value type produced by <paramref name="next"/>.</typeparam>
    /// <param name="result">The synchronous result to evaluate.</param>
    /// <param name="next">
    /// The async function to invoke when <paramref name="result"/> is successful.
    /// </param>
    /// <returns>
    /// The <see cref="Result{TValue}"/> returned by <paramref name="next"/>, or
    /// the original errors if <paramref name="result"/> was already a failure.
    /// </returns>
    [Pure]
    public static async Task<Result<TNext>> ThenAsync<T, TNext>(
        this Result<T> result,
        Func<T, Task<Result<TNext>>> next) {

        if(result.IsFailure) return result.Errors.ToList();
        return await next(result.Value).ConfigureAwait(false);
    }

    /// <summary>
    /// From a synchronous <see cref="Result{TValue}"/>, executes <paramref name="next"/>
    /// asynchronously, forwarding the <paramref name="cancellationToken"/>.
    /// </summary>
    /// <typeparam name="T">The value type of the incoming result.</typeparam>
    /// <typeparam name="TNext">The value type produced by <paramref name="next"/>.</typeparam>
    /// <param name="result">The synchronous result to evaluate.</param>
    /// <param name="next">
    /// The async function to invoke when <paramref name="result"/> is successful.
    /// Receives both the unwrapped value and the <see cref="CancellationToken"/>.
    /// </param>
    /// <param name="cancellationToken">Forwarded to <paramref name="next"/>.</param>
    /// <returns>
    /// The <see cref="Result{TValue}"/> returned by <paramref name="next"/>, or
    /// the original errors if <paramref name="result"/> was already a failure.
    /// </returns>
    [Pure]
    public static async Task<Result<TNext>> ThenAsync<T, TNext>(
        this Result<T> result,
        Func<T, CancellationToken, Task<Result<TNext>>> next,
        CancellationToken cancellationToken = default) {

        if(result.IsFailure) return result.Errors.ToList();
        return await next(result.Value, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// From a synchronous <see cref="Result{TValue}"/>, executes <paramref name="next"/>
    /// which returns a plain <see cref="Task{TResult}"/> (not a <see cref="Result{TValue}"/>).
    /// The awaited value is automatically wrapped in a successful <see cref="Result{TValue}"/>.
    /// </summary>
    /// <typeparam name="T">The value type of the incoming result.</typeparam>
    /// <typeparam name="TNext">The type returned by <paramref name="next"/>.</typeparam>
    /// <param name="result">The synchronous result to evaluate.</param>
    /// <param name="next">
    /// An async function that returns a plain value. Use this when the operation
    /// cannot fail (e.g., a pure transformation or a fire-and-forget side-effect
    /// that still needs to produce a value).
    /// </param>
    /// <returns>
    /// A successful <see cref="Result{TValue}"/> wrapping the awaited value, or
    /// the original errors if <paramref name="result"/> was already a failure.
    /// </returns>
    [Pure]
    public static async Task<Result<TNext>> ThenAsync<T, TNext>(
        this Result<T> result,
        Func<T, Task<TNext>> next) {

        if(result.IsFailure) return result.Errors.ToList();
        TNext value = await next(result.Value).ConfigureAwait(false);
        return Result.Success(value);
    }

    /// <summary>
    /// From a synchronous <see cref="Result{TValue}"/>, executes <paramref name="next"/>
    /// which returns a plain <see cref="Task{TResult}"/>, forwarding the
    /// <paramref name="cancellationToken"/>.
    /// The awaited value is automatically wrapped in a successful <see cref="Result{TValue}"/>.
    /// </summary>
    /// <typeparam name="T">The value type of the incoming result.</typeparam>
    /// <typeparam name="TNext">The type returned by <paramref name="next"/>.</typeparam>
    /// <param name="result">The synchronous result to evaluate.</param>
    /// <param name="next">
    /// An async function that returns a plain value.
    /// Receives the unwrapped value and the <see cref="CancellationToken"/>.
    /// </param>
    /// <param name="cancellationToken">Forwarded to <paramref name="next"/>.</param>
    /// <returns>
    /// A successful <see cref="Result{TValue}"/> wrapping the awaited value, or
    /// the original errors if <paramref name="result"/> was already a failure.
    /// </returns>
    [Pure]
    public static async Task<Result<TNext>> ThenAsync<T, TNext>(
        this Result<T> result,
        Func<T, CancellationToken, Task<TNext>> next,
        CancellationToken cancellationToken = default) {

        if(result.IsFailure) return result.Errors.ToList();
        TNext value = await next(result.Value, cancellationToken).ConfigureAwait(false);
        return Result.Success(value);
    }

    // ── MatchAsync ────────────────────────────────────────────────────────────

    /// <summary>
    /// Awaits <paramref name="task"/> and applies synchronous match functions
    /// to produce a value of <typeparamref name="TResult"/>.
    /// </summary>
    /// <typeparam name="T">The value type of the result.</typeparam>
    /// <typeparam name="TResult">The output type of both match functions.</typeparam>
    /// <param name="task">The task to await.</param>
    /// <param name="onValue">Invoked with the unwrapped value when the result is successful.</param>
    /// <param name="onError">Invoked with the error list when the result is a failure.</param>
    /// <param name="cancellationToken">
    /// Checked after the task completes and before the match functions are called.
    /// </param>
    /// <returns>The value returned by whichever branch was executed.</returns>
    [Pure]
    public static async Task<TResult> MatchAsync<T, TResult>(
        this Task<Result<T>> task,
        Func<T, TResult> onValue,
        Func<IReadOnlyList<Error>, TResult> onError,
        CancellationToken cancellationToken = default) {

        Result<T> result = await task.ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return result.Match(onValue, onError);
    }

    /// <summary>
    /// Awaits <paramref name="task"/> and applies asynchronous match functions
    /// to produce a value of <typeparamref name="TResult"/>.
    /// </summary>
    /// <typeparam name="T">The value type of the result.</typeparam>
    /// <typeparam name="TResult">The output type of both match functions.</typeparam>
    /// <param name="task">The task to await.</param>
    /// <param name="onValue">Async function invoked when the result is successful.</param>
    /// <param name="onError">Async function invoked when the result is a failure.</param>
    /// <param name="cancellationToken">
    /// Checked after the task completes and before the chosen branch is executed.
    /// </param>
    /// <returns>The value produced by whichever async branch was executed.</returns>
    [Pure]
    public static async Task<TResult> MatchAsync<T, TResult>(
        this Task<Result<T>> task,
        Func<T, Task<TResult>> onValue,
        Func<IReadOnlyList<Error>, Task<TResult>> onError,
        CancellationToken cancellationToken = default) {

        Result<T> result = await task.ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        return result.IsFailure
            ? await onError(result.Errors).ConfigureAwait(false)
            : await onValue(result.Value).ConfigureAwait(false);
    }

    // ── MapAsync ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Awaits <paramref name="task"/> and, if successful, transforms the value
    /// using a synchronous <paramref name="mapper"/>.
    /// Unlike <see cref="ThenAsync{T, TNext}(Task{Result{T}}, Func{T, Result{TNext}})"/>,
    /// the mapper cannot itself fail — use <c>ThenAsync</c> when the transformation
    /// may produce an error.
    /// </summary>
    /// <typeparam name="T">The value type of the incoming result.</typeparam>
    /// <typeparam name="TNew">The type produced by <paramref name="mapper"/>.</typeparam>
    /// <param name="task">The task to await.</param>
    /// <param name="mapper">A synchronous, infallible transformation function.</param>
    /// <returns>
    /// A successful <see cref="Result{TValue}"/> containing the mapped value,
    /// or the original errors.
    /// </returns>
    [Pure]
    public static async Task<Result<TNew>> MapAsync<T, TNew>(
        this Task<Result<T>> task,
        Func<T, TNew> mapper) {

        Result<T> result = await task.ConfigureAwait(false);
        if(result.IsFailure) return result.Errors.ToList();
        return Result.Success(mapper(result.Value));
    }

    /// <summary>
    /// Awaits <paramref name="task"/> and, if successful, transforms the value
    /// using an async <paramref name="mapper"/> that returns a plain value.
    /// </summary>
    /// <typeparam name="T">The value type of the incoming result.</typeparam>
    /// <typeparam name="TNew">The type produced by <paramref name="mapper"/>.</typeparam>
    /// <param name="task">The task to await.</param>
    /// <param name="mapper">An async, infallible transformation function.</param>
    /// <returns>
    /// A successful <see cref="Result{TValue}"/> containing the mapped value,
    /// or the original errors.
    /// </returns>
    [Pure]
    public static async Task<Result<TNew>> MapAsync<T, TNew>(
        this Task<Result<T>> task,
        Func<T, Task<TNew>> mapper) {

        Result<T> result = await task.ConfigureAwait(false);
        if(result.IsFailure) return result.Errors.ToList();
        return Result.Success(await mapper(result.Value).ConfigureAwait(false));
    }

    /// <summary>
    /// Awaits <paramref name="task"/> and, if successful, applies <paramref name="mapper"/>
    /// which itself returns a <see cref="Result{TValue}"/>.
    /// The result is flattened — this prevents nesting a
    /// <see cref="Result{TValue}"/> inside another <see cref="Result{TValue}"/>.
    /// </summary>
    /// <typeparam name="T">The value type of the incoming result.</typeparam>
    /// <typeparam name="TNew">The value type inside the <see cref="Result{TValue}"/> returned by <paramref name="mapper"/>.</typeparam>
    /// <param name="task">The task to await.</param>
    /// <param name="mapper">
    /// A synchronous function that returns a <see cref="Result{TValue}"/>.
    /// If the mapper itself fails, those errors are propagated.
    /// </param>
    /// <returns>
    /// The flattened <see cref="Result{TValue}"/> from <paramref name="mapper"/>,
    /// or the original errors if the incoming result was already a failure.
    /// </returns>
    [Pure]
    public static async Task<Result<TNew>> MapAsync<T, TNew>(
        this Task<Result<T>> task,
        Func<T, Result<TNew>> mapper) {

        Result<T> result = await task.ConfigureAwait(false);
        if(result.IsFailure) return result.Errors.ToList();
        return mapper(result.Value);
    }

    // ── MapSuccessAsync ───────────────────────────────────────────────────────

    /// <summary>
    /// Awaits <paramref name="task"/> and discards the value, converting a
    /// <see cref="Result{TValue}"/> to <see cref="Result{TValue}"/> where
    /// <c>TValue</c> is <see cref="Success"/>.
    /// <para>
    /// Use this at the end of a chain when the caller only needs to know
    /// whether the overall operation succeeded, not what value it produced.
    /// </para>
    /// </summary>
    /// <typeparam name="T">The value type being discarded.</typeparam>
    /// <param name="task">The task to await.</param>
    /// <param name="cancellationToken">Checked after the task completes.</param>
    /// <returns>
    /// <see cref="Result{TValue}"/> of <see cref="Success"/> on success,
    /// or the original errors on failure.
    /// </returns>
    [Pure]
    public static async Task<Result<Success>> MapSuccessAsync<T>(
        this Task<Result<T>> task,
        CancellationToken cancellationToken = default) {

        Result<T> result = await task.ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return result.Map(_ => Success.Default);
    }

    // ── RecoverAsync ──────────────────────────────────────────────────────────

    /// <summary>
    /// Awaits <paramref name="task"/> and, if failed, attempts to recover by
    /// invoking the synchronous <paramref name="recover"/> fallback.
    /// Has no effect when the result is already successful.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="task">The task to await.</param>
    /// <param name="recover">
    /// A synchronous fallback function that receives the error list and returns
    /// a replacement value.
    /// </param>
    /// <returns>
    /// A successful <see cref="Result{TValue}"/> containing either the original
    /// value or the fallback, depending on the outcome.
    /// </returns>
    [Pure]
    public static async Task<Result<T>> RecoverAsync<T>(
        this Task<Result<T>> task,
        Func<IReadOnlyList<Error>, T> recover) {

        Result<T> result = await task.ConfigureAwait(false);
        return result.Recover(recover);
    }

    /// <summary>
    /// Awaits <paramref name="task"/> and, if failed, attempts to recover by
    /// invoking the asynchronous <paramref name="recover"/> fallback.
    /// Has no effect when the result is already successful.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="task">The task to await.</param>
    /// <param name="recover">
    /// An async fallback function that receives the error list and returns
    /// a replacement value.
    /// </param>
    /// <returns>
    /// A successful <see cref="Result{TValue}"/> containing either the original
    /// value or the awaited fallback.
    /// </returns>
    [Pure]
    public static async Task<Result<T>> RecoverAsync<T>(
        this Task<Result<T>> task,
        Func<IReadOnlyList<Error>, Task<T>> recover) {

        Result<T> result = await task.ConfigureAwait(false);
        if(result.IsSuccess) return result;
        return await recover(result.Errors).ConfigureAwait(false);
    }

    /// <summary>
    /// From a synchronous <see cref="Result{TValue}"/>, if failed, attempts to
    /// recover using the asynchronous <paramref name="recover"/> fallback.
    /// Has no effect when the result is already successful.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="result">The synchronous result to evaluate.</param>
    /// <param name="recover">
    /// An async fallback function that receives the error list.
    /// </param>
    /// <returns>
    /// A successful <see cref="Result{TValue}"/> containing either the original
    /// value or the awaited fallback.
    /// </returns>
    [Pure]
    public static async Task<Result<T>> RecoverAsync<T>(
        this Result<T> result,
        Func<IReadOnlyList<Error>, Task<T>> recover) {

        if(result.IsSuccess) return result;
        return await recover(result.Errors).ConfigureAwait(false);
    }

    // ── DoAsync ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Awaits <paramref name="task"/> and, if successful, executes the synchronous
    /// <paramref name="action"/> as a side-effect without changing the result.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="task">The task to await.</param>
    /// <param name="action">A synchronous side-effect, e.g., logging.</param>
    /// <returns>The original <see cref="Result{TValue}"/> unchanged.</returns>
    public static async Task<Result<T>> DoAsync<T>(
        this Task<Result<T>> task,
        Action<T> action) {

        Result<T> result = await task.ConfigureAwait(false);
        if(result.IsSuccess) action(result.Value);
        return result;
    }

    /// <summary>
    /// Awaits <paramref name="task"/> and, if successful, executes the asynchronous
    /// <paramref name="action"/>, forwarding the <paramref name="cancellationToken"/>.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="task">The task to await.</param>
    /// <param name="action">
    /// An async side-effect that receives the unwrapped value and a
    /// <see cref="CancellationToken"/>.
    /// </param>
    /// <param name="cancellationToken">
    /// Checked before invoking <paramref name="action"/> and forwarded to it.
    /// </param>
    /// <returns>The original <see cref="Result{TValue}"/> unchanged.</returns>
    public static async Task<Result<T>> DoAsync<T>(
        this Task<Result<T>> task,
        Func<T, CancellationToken, Task> action,
        CancellationToken cancellationToken = default) {

        Result<T> result = await task.ConfigureAwait(false);
        if(result.IsSuccess) {
            cancellationToken.ThrowIfCancellationRequested();
            await action(result.Value, cancellationToken).ConfigureAwait(false);
        }
        return result;
    }

    /// <summary>
    /// From a synchronous <see cref="Result{TValue}"/>, if successful, executes
    /// the asynchronous <paramref name="action"/>.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="result">The synchronous result to evaluate.</param>
    /// <param name="action">
    /// An async side-effect that receives the unwrapped value and a
    /// <see cref="CancellationToken"/>.
    /// </param>
    /// <param name="cancellationToken">
    /// Checked before invoking <paramref name="action"/> and forwarded to it.
    /// </param>
    /// <returns>The original <see cref="Result{TValue}"/> unchanged.</returns>
    public static async Task<Result<T>> DoAsync<T>(
        this Result<T> result,
        Func<T, CancellationToken, Task> action,
        CancellationToken cancellationToken = default) {

        if(result.IsSuccess) {
            cancellationToken.ThrowIfCancellationRequested();
            await action(result.Value, cancellationToken).ConfigureAwait(false);
        }
        return result;
    }

    /// <summary>
    /// Awaits <paramref name="task"/> and, if successful, executes the parameterless
    /// asynchronous <paramref name="action"/>.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="task">The task to await.</param>
    /// <param name="action">
    /// A parameterless async side-effect that receives only a
    /// <see cref="CancellationToken"/>. Use when you don't need the value,
    /// e.g., invalidating a cache entry.
    /// </param>
    /// <param name="cancellationToken">
    /// Checked before invoking <paramref name="action"/> and forwarded to it.
    /// </param>
    /// <returns>The original <see cref="Result{TValue}"/> unchanged.</returns>
    public static async Task<Result<T>> DoAsync<T>(
        this Task<Result<T>> task,
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken = default) {

        Result<T> result = await task.ConfigureAwait(false);
        if(result.IsSuccess) {
            cancellationToken.ThrowIfCancellationRequested();
            await action(cancellationToken).ConfigureAwait(false);
        }
        return result;
    }

    /// <summary>
    /// From a synchronous <see cref="Result{TValue}"/>, if successful, executes the
    /// parameterless asynchronous <paramref name="action"/>.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="result">The synchronous result to evaluate.</param>
    /// <param name="action">A parameterless async side-effect.</param>
    /// <param name="cancellationToken">
    /// Checked before invoking <paramref name="action"/> and forwarded to it.
    /// </param>
    /// <returns>The original <see cref="Result{TValue}"/> unchanged.</returns>
    public static async Task<Result<T>> DoAsync<T>(
        this Result<T> result,
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken = default) {

        if(result.IsSuccess) {
            cancellationToken.ThrowIfCancellationRequested();
            await action(cancellationToken).ConfigureAwait(false);
        }
        return result;
    }

    // ── IfSuccessAsync / IfFailureAsync ───────────────────────────────────────

    /// <summary>
    /// Awaits <paramref name="task"/> and, if successful, executes the synchronous
    /// <paramref name="action"/>. Alias for
    /// <see cref="DoAsync{T}(Task{Result{T}}, Action{T})"/>.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="task">The task to await.</param>
    /// <param name="action">Synchronous side-effect to run on success.</param>
    /// <param name="cancellationToken">Checked after the task completes.</param>
    /// <returns>The original <see cref="Result{TValue}"/> unchanged.</returns>
    public static async Task<Result<T>> IfSuccessAsync<T>(
        this Task<Result<T>> task,
        Action<T> action,
        CancellationToken cancellationToken = default) {

        Result<T> result = await task.ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        if(result.IsSuccess) action(result.Value);
        return result;
    }

    /// <summary>
    /// Awaits <paramref name="task"/> and, if successful, executes the asynchronous
    /// <paramref name="action"/>.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="task">The task to await.</param>
    /// <param name="action">Async side-effect to run on success.</param>
    /// <param name="cancellationToken">Forwarded to <paramref name="action"/>.</param>
    /// <returns>The original <see cref="Result{TValue}"/> unchanged.</returns>
    public static async Task<Result<T>> IfSuccessAsync<T>(
        this Task<Result<T>> task,
        Func<T, CancellationToken, Task> action,
        CancellationToken cancellationToken = default) {

        Result<T> result = await task.ConfigureAwait(false);
        if(result.IsSuccess) {
            cancellationToken.ThrowIfCancellationRequested();
            await action(result.Value, cancellationToken).ConfigureAwait(false);
        }
        return result;
    }

    /// <summary>
    /// Awaits <paramref name="task"/> and, if failed, executes the synchronous
    /// <paramref name="action"/>.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="task">The task to await.</param>
    /// <param name="action">
    /// Synchronous side-effect that receives the full error list,
    /// e.g., for logging.
    /// </param>
    /// <param name="cancellationToken">Checked after the task completes.</param>
    /// <returns>The original <see cref="Result{TValue}"/> unchanged.</returns>
    public static async Task<Result<T>> IfFailureAsync<T>(
        this Task<Result<T>> task,
        Action<IReadOnlyList<Error>> action,
        CancellationToken cancellationToken = default) {

        Result<T> result = await task.ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        if(result.IsFailure) action(result.Errors);
        return result;
    }

    /// <summary>
    /// Awaits <paramref name="task"/> and, if failed, executes the asynchronous
    /// <paramref name="action"/>.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="task">The task to await.</param>
    /// <param name="action">
    /// Async side-effect that receives the full error list and a
    /// <see cref="CancellationToken"/>.
    /// </param>
    /// <param name="cancellationToken">Forwarded to <paramref name="action"/>.</param>
    /// <returns>The original <see cref="Result{TValue}"/> unchanged.</returns>
    public static async Task<Result<T>> IfFailureAsync<T>(
        this Task<Result<T>> task,
        Func<IReadOnlyList<Error>, CancellationToken, Task> action,
        CancellationToken cancellationToken = default) {

        Result<T> result = await task.ConfigureAwait(false);
        if(result.IsFailure) {
            cancellationToken.ThrowIfCancellationRequested();
            await action(result.Errors, cancellationToken).ConfigureAwait(false);
        }
        return result;
    }
}