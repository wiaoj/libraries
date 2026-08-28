using System.Diagnostics.Contracts;

namespace Wiaoj.Results;

/// <summary>
/// <see cref="ValueTask"/> overloads of the core asynchronous combinators.
/// </summary>
public static class ResultsValueTaskExtensions {

    // ── ThenAsync ─────────────────────────────────────────────────────────────

    /// <summary>Awaits <paramref name="valueTask"/> and, if successful, executes <paramref name="next"/>.</summary>
    [Pure]
    public static async ValueTask<Result<TNext>> ThenAsync<T, TNext>(
        this ValueTask<Result<T>> valueTask,
        Func<T, ValueTask<Result<TNext>>> next,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(next);

        Result<T> result = await valueTask.ConfigureAwait(false);
        if(result.IsFailure) return result.ToFailure<TNext>();

        cancellationToken.ThrowIfCancellationRequested();
        return await next(result.Value).ConfigureAwait(false);
    }

    /// <summary>Awaits <paramref name="valueTask"/> and, if successful, executes <paramref name="next"/> forwarding cancellation token.</summary>
    [Pure]
    public static async ValueTask<Result<TNext>> ThenAsync<T, TNext>(
        this ValueTask<Result<T>> valueTask,
        Func<T, CancellationToken, ValueTask<Result<TNext>>> next,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(next);

        Result<T> result = await valueTask.ConfigureAwait(false);
        if(result.IsFailure) return result.ToFailure<TNext>();

        cancellationToken.ThrowIfCancellationRequested();
        return await next(result.Value, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>From a synchronous result, executes <paramref name="next"/> returning <see cref="ValueTask{TResult}"/>.</summary>
    [Pure]
    public static async ValueTask<Result<TNext>> ThenAsync<T, TNext>(
        this Result<T> result,
        Func<T, ValueTask<Result<TNext>>> next) {
        ArgumentNullException.ThrowIfNull(next);

        if(result.IsFailure) return result.ToFailure<TNext>();
        return await next(result.Value).ConfigureAwait(false);
    }

    /// <summary>From a synchronous result, executes <paramref name="next"/> returning <see cref="ValueTask{TResult}"/> forwarding cancellation token.</summary>
    [Pure]
    public static async ValueTask<Result<TNext>> ThenAsync<T, TNext>(
        this Result<T> result,
        Func<T, CancellationToken, ValueTask<Result<TNext>>> next,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(next);

        if(result.IsFailure) return result.ToFailure<TNext>();
        return await next(result.Value, cancellationToken).ConfigureAwait(false);
    }

    // ── MapAsync ──────────────────────────────────────────────────────────────

    /// <summary>Awaits <paramref name="valueTask"/> and transforms the value with a synchronous mapper.</summary>
    [Pure]
    public static async ValueTask<Result<TNew>> MapAsync<T, TNew>(
        this ValueTask<Result<T>> valueTask,
        Func<T, TNew> mapper) {
        ArgumentNullException.ThrowIfNull(mapper);

        Result<T> result = await valueTask.ConfigureAwait(false);
        if(result.IsFailure) return result.ToFailure<TNew>();
        return Result.Success(mapper(result.Value));
    }

    /// <summary>Awaits <paramref name="valueTask"/> and transforms the value with an asynchronous mapper.</summary>
    [Pure]
    public static async ValueTask<Result<TNew>> MapAsync<T, TNew>(
        this ValueTask<Result<T>> valueTask,
        Func<T, ValueTask<TNew>> mapper) {
        ArgumentNullException.ThrowIfNull(mapper);

        Result<T> result = await valueTask.ConfigureAwait(false);
        if(result.IsFailure) return result.ToFailure<TNew>();
        return Result.Success(await mapper(result.Value).ConfigureAwait(false));
    }

    // ── EnsureAsync ───────────────────────────────────────────────────────────

    /// <summary>Awaits <paramref name="valueTask"/> and validates value with a synchronous predicate.</summary>
    [Pure]
    public static async ValueTask<Result<T>> EnsureAsync<T>(
        this ValueTask<Result<T>> valueTask,
        Func<T, bool> predicate,
        Error error) {
        ArgumentNullException.ThrowIfNull(predicate);

        Result<T> result = await valueTask.ConfigureAwait(false);
        if(result.IsFailure) return result;
        if(!predicate(result.Value)) return error;
        return result;
    }

    /// <summary>Awaits <paramref name="valueTask"/> and validates value with an asynchronous predicate.</summary>
    [Pure]
    public static async ValueTask<Result<T>> EnsureAsync<T>(
        this ValueTask<Result<T>> valueTask,
        Func<T, ValueTask<bool>> predicate,
        Error error) {
        ArgumentNullException.ThrowIfNull(predicate);

        Result<T> result = await valueTask.ConfigureAwait(false);
        if(result.IsFailure) return result;
        if(!await predicate(result.Value).ConfigureAwait(false)) return error;
        return result;
    }

    // ── DoAsync & TapAsync ────────────────────────────────────────────────────

    /// <summary>Awaits <paramref name="valueTask"/> and executes synchronous side-effect.</summary>
    public static async ValueTask<Result<T>> DoAsync<T>(
        this ValueTask<Result<T>> valueTask,
        Action<T> action) {
        ArgumentNullException.ThrowIfNull(action);

        Result<T> result = await valueTask.ConfigureAwait(false);
        if(result.IsSuccess) action(result.Value);
        return result;
    }

    /// <summary>Alias for <see cref="DoAsync{T}(ValueTask{Result{T}}, Action{T})"/>.</summary>
    public static ValueTask<Result<T>> TapAsync<T>(this ValueTask<Result<T>> valueTask, Action<T> action) =>
        DoAsync(valueTask, action);

    /// <summary>Awaits <paramref name="valueTask"/> and executes asynchronous side-effect.</summary>
    public static async ValueTask<Result<T>> DoAsync<T>(
        this ValueTask<Result<T>> valueTask,
        Func<T, CancellationToken, ValueTask> action,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(action);

        Result<T> result = await valueTask.ConfigureAwait(false);
        if(result.IsSuccess) {
            cancellationToken.ThrowIfCancellationRequested();
            await action(result.Value, cancellationToken).ConfigureAwait(false);
        }
        return result;
    }

    /// <summary>Alias for <see cref="DoAsync{T}(ValueTask{Result{T}}, Func{T, CancellationToken, ValueTask}, CancellationToken)"/>.</summary>
    public static ValueTask<Result<T>> TapAsync<T>(
        this ValueTask<Result<T>> valueTask,
        Func<T, CancellationToken, ValueTask> action,
        CancellationToken cancellationToken = default) => DoAsync(valueTask, action, cancellationToken);

    // ── IfSuccessAsync / IfFailureAsync / TapErrorAsync ───────────────────────

    /// <summary>Awaits <paramref name="valueTask"/> and executes synchronous success action.</summary>
    public static async ValueTask<Result<T>> IfSuccessAsync<T>(
        this ValueTask<Result<T>> valueTask,
        Action<T> action,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(action);

        Result<T> result = await valueTask.ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        if(result.IsSuccess) action(result.Value);
        return result;
    }

    /// <summary>Awaits <paramref name="valueTask"/> and executes synchronous failure action.</summary>
    public static async ValueTask<Result<T>> IfFailureAsync<T>(
        this ValueTask<Result<T>> valueTask,
        Action<IReadOnlyList<Error>> action,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(action);

        Result<T> result = await valueTask.ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        if(result.IsFailure) action(result.Errors);
        return result;
    }

    /// <summary>Alias for <see cref="IfFailureAsync{T}(ValueTask{Result{T}}, Action{IReadOnlyList{Error}}, CancellationToken)"/>.</summary>
    public static ValueTask<Result<T>> TapErrorAsync<T>(
        this ValueTask<Result<T>> valueTask,
        Action<IReadOnlyList<Error>> action,
        CancellationToken cancellationToken = default) => IfFailureAsync(valueTask, action, cancellationToken);

    // ── BiMapAsync ────────────────────────────────────────────────────────────

    /// <summary>Awaits <paramref name="valueTask"/> and applies two-track transformation.</summary>
    [Pure]
    public static async ValueTask<Result<TNew>> BiMapAsync<T, TNew>(
        this ValueTask<Result<T>> valueTask,
        Func<T, TNew> mapSuccess,
        Func<IReadOnlyList<Error>, Error> mapError) {
        ArgumentNullException.ThrowIfNull(mapSuccess);
        ArgumentNullException.ThrowIfNull(mapError);

        Result<T> result = await valueTask.ConfigureAwait(false);
        return result.BiMap(mapSuccess, mapError);
    }

    // ── MatchAsync ────────────────────────────────────────────────────────────

    /// <summary>Awaits <paramref name="valueTask"/> and applies synchronous match functions.</summary>
    [Pure]
    public static async ValueTask<TResult> MatchAsync<T, TResult>(
        this ValueTask<Result<T>> valueTask,
        Func<T, TResult> onValue,
        Func<IReadOnlyList<Error>, TResult> onError,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(onValue);
        ArgumentNullException.ThrowIfNull(onError);

        Result<T> result = await valueTask.ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return result.Match(onValue, onError);
    }

    /// <summary>Awaits <paramref name="valueTask"/> and applies asynchronous match functions.</summary>
    [Pure]
    public static async ValueTask<TResult> MatchAsync<T, TResult>(
        this ValueTask<Result<T>> valueTask,
        Func<T, ValueTask<TResult>> onValue,
        Func<IReadOnlyList<Error>, ValueTask<TResult>> onError,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(onValue);
        ArgumentNullException.ThrowIfNull(onError);

        Result<T> result = await valueTask.ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        return result.IsFailure
            ? await onError(result.Errors).ConfigureAwait(false)
            : await onValue(result.Value).ConfigureAwait(false);
    }

    // ── AsValueTask ───────────────────────────────────────────────────────────

    /// <summary>Wraps <paramref name="result"/> into a completed <see cref="ValueTask{TResult}"/>.</summary>
    [Pure]
    public static ValueTask<Result<T>> AsValueTask<T>(this Result<T> result) =>
        ValueTask.FromResult(result);

    /// <summary>Converts a <see cref="Task{TResult}"/> of <see cref="Result{TValue}"/> to a <see cref="ValueTask{TResult}"/>.</summary>
    [Pure]
    public static ValueTask<Result<T>> AsValueTask<T>(this Task<Result<T>> task) {
        ArgumentNullException.ThrowIfNull(task);
        return new(task);
    }
}