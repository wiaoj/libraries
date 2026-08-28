using System.Diagnostics.Contracts;

namespace Wiaoj.Results;

/// <summary>
/// Asynchronous extension methods for chaining, transforming, matching, and side-effects on <see cref="Result{TValue}"/>.
/// </summary>
public static partial class ResultsExtensions {

    // ── ThenAsync ─────────────────────────────────────────────────────────────

    /// <summary>Awaits <paramref name="task"/> and, if successful, executes <paramref name="next"/>.</summary>
    [Pure]
    public static async Task<Result<TNext>> ThenAsync<T, TNext>(
        this Task<Result<T>> task,
        Func<T, Task<Result<TNext>>> next,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentNullException.ThrowIfNull(next);

        Result<T> result = await task.ConfigureAwait(false);
        if(result.IsFailure) return result.ToFailure<TNext>();

        cancellationToken.ThrowIfCancellationRequested();
        return await next(result.Value).ConfigureAwait(false);
    }

    /// <summary>Awaits <paramref name="task"/> and, if successful, executes <paramref name="next"/> forwarding cancellation token.</summary>
    [Pure]
    public static async Task<Result<TNext>> ThenAsync<T, TNext>(
        this Task<Result<T>> task,
        Func<T, CancellationToken, Task<Result<TNext>>> next,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentNullException.ThrowIfNull(next);

        Result<T> result = await task.ConfigureAwait(false);
        if(result.IsFailure) return result.ToFailure<TNext>();

        cancellationToken.ThrowIfCancellationRequested();
        return await next(result.Value, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Awaits <paramref name="task"/> and, if successful, executes synchronous <paramref name="next"/>.</summary>
    [Pure]
    public static async Task<Result<TNext>> ThenAsync<T, TNext>(
        this Task<Result<T>> task,
        Func<T, Result<TNext>> next) {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentNullException.ThrowIfNull(next);

        Result<T> result = await task.ConfigureAwait(false);
        if(result.IsFailure) return result.ToFailure<TNext>();
        return next(result.Value);
    }

    /// <summary>From a synchronous result, executes <paramref name="next"/> asynchronously.</summary>
    [Pure]
    public static async Task<Result<TNext>> ThenAsync<T, TNext>(
        this Result<T> result,
        Func<T, Task<Result<TNext>>> next) {
        ArgumentNullException.ThrowIfNull(next);

        if(result.IsFailure) return result.ToFailure<TNext>();
        return await next(result.Value).ConfigureAwait(false);
    }

    /// <summary>From a synchronous result, executes <paramref name="next"/> asynchronously forwarding cancellation token.</summary>
    [Pure]
    public static async Task<Result<TNext>> ThenAsync<T, TNext>(
        this Result<T> result,
        Func<T, CancellationToken, Task<Result<TNext>>> next,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(next);

        if(result.IsFailure) return result.ToFailure<TNext>();
        return await next(result.Value, cancellationToken).ConfigureAwait(false);
    }

    // ── MapAsync ──────────────────────────────────────────────────────────────

    /// <summary>Awaits <paramref name="task"/> and transforms the value using a synchronous mapper.</summary>
    [Pure]
    public static async Task<Result<TNew>> MapAsync<T, TNew>(
        this Task<Result<T>> task,
        Func<T, TNew> mapper) {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentNullException.ThrowIfNull(mapper);

        Result<T> result = await task.ConfigureAwait(false);
        if(result.IsFailure) return result.ToFailure<TNew>();
        return Result.Success(mapper(result.Value));
    }

    /// <summary>Awaits <paramref name="task"/> and transforms the value using an asynchronous mapper.</summary>
    [Pure]
    public static async Task<Result<TNew>> MapAsync<T, TNew>(
        this Task<Result<T>> task,
        Func<T, Task<TNew>> mapper) {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentNullException.ThrowIfNull(mapper);

        Result<T> result = await task.ConfigureAwait(false);
        if(result.IsFailure) return result.ToFailure<TNew>();
        return Result.Success(await mapper(result.Value).ConfigureAwait(false));
    }

    /// <summary>Awaits <paramref name="task"/> and transforms the value using an asynchronous mapper forwarding cancellation token.</summary>
    [Pure]
    public static async Task<Result<TNew>> MapAsync<T, TNew>(
        this Task<Result<T>> task,
        Func<T, CancellationToken, Task<TNew>> mapper,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentNullException.ThrowIfNull(mapper);

        Result<T> result = await task.ConfigureAwait(false);
        if(result.IsFailure) return result.ToFailure<TNew>();

        cancellationToken.ThrowIfCancellationRequested();
        TNew mappedValue = await mapper(result.Value, cancellationToken).ConfigureAwait(false);
        return Result.Success(mappedValue);
    }

    /// <summary>From a synchronous result, transforms the value using an asynchronous mapper.</summary>
    [Pure]
    public static async Task<Result<TNew>> MapAsync<T, TNew>(
        this Result<T> result,
        Func<T, Task<TNew>> mapper) {
        ArgumentNullException.ThrowIfNull(mapper);

        if(result.IsFailure) return result.ToFailure<TNew>();
        TNew mappedValue = await mapper(result.Value).ConfigureAwait(false);
        return Result.Success(mappedValue);
    }

    /// <summary>From a synchronous result, transforms the value using an asynchronous mapper forwarding cancellation token.</summary>
    [Pure]
    public static async Task<Result<TNew>> MapAsync<T, TNew>(
        this Result<T> result,
        Func<T, CancellationToken, Task<TNew>> mapper,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(mapper);

        if(result.IsFailure) return result.ToFailure<TNew>();
        cancellationToken.ThrowIfCancellationRequested();
        TNew mappedValue = await mapper(result.Value, cancellationToken).ConfigureAwait(false);
        return Result.Success(mappedValue);
    }

    /// <summary>Awaits <paramref name="task"/> and applies a mapper returning a result, flattening it.</summary>
    [Pure]
    public static async Task<Result<TNew>> MapAsync<T, TNew>(
        this Task<Result<T>> task,
        Func<T, Result<TNew>> mapper) {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentNullException.ThrowIfNull(mapper);

        Result<T> result = await task.ConfigureAwait(false);
        if(result.IsFailure) return result.ToFailure<TNew>();
        return mapper(result.Value);
    }

    // ── MapSuccessAsync ───────────────────────────────────────────────────────

    /// <summary>Awaits <paramref name="task"/> and discards the value, returning <see cref="Success"/>.</summary>
    [Pure]
    public static async Task<Result<Success>> MapSuccessAsync<T>(
        this Task<Result<T>> task,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(task);

        Result<T> result = await task.ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        if(result.IsFailure) return result.ToFailure<Success>();
        return Wiaoj.Results.Success.Default;
    }

    // ── MatchAsync ────────────────────────────────────────────────────────────

    /// <summary>Awaits <paramref name="task"/> and applies synchronous match functions.</summary>
    [Pure]
    public static async Task<TResult> MatchAsync<T, TResult>(
        this Task<Result<T>> task,
        Func<T, TResult> onValue,
        Func<IReadOnlyList<Error>, TResult> onError,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentNullException.ThrowIfNull(onValue);
        ArgumentNullException.ThrowIfNull(onError);

        Result<T> result = await task.ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return result.Match(onValue, onError);
    }

    /// <summary>Awaits <paramref name="task"/> and applies asynchronous match functions.</summary>
    [Pure]
    public static async Task<TResult> MatchAsync<T, TResult>(
        this Task<Result<T>> task,
        Func<T, Task<TResult>> onValue,
        Func<IReadOnlyList<Error>, Task<TResult>> onError,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentNullException.ThrowIfNull(onValue);
        ArgumentNullException.ThrowIfNull(onError);

        Result<T> result = await task.ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        return result.IsFailure
            ? await onError(result.Errors).ConfigureAwait(false)
            : await onValue(result.Value).ConfigureAwait(false);
    }

    // ── RecoverAsync ──────────────────────────────────────────────────────────

    /// <summary>Awaits <paramref name="task"/> and, if failed, recovers using a synchronous fallback.</summary>
    [Pure]
    public static async Task<Result<T>> RecoverAsync<T>(
        this Task<Result<T>> task,
        Func<IReadOnlyList<Error>, T> recover) {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentNullException.ThrowIfNull(recover);

        Result<T> result = await task.ConfigureAwait(false);
        return result.Recover(recover);
    }

    /// <summary>Awaits <paramref name="task"/> and, if failed, recovers using an asynchronous fallback.</summary>
    [Pure]
    public static async Task<Result<T>> RecoverAsync<T>(
        this Task<Result<T>> task,
        Func<IReadOnlyList<Error>, Task<T>> recover) {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentNullException.ThrowIfNull(recover);

        Result<T> result = await task.ConfigureAwait(false);
        if(result.IsSuccess) return result;
        return await recover(result.Errors).ConfigureAwait(false);
    }

    /// <summary>From a synchronous result, if failed, recovers using an asynchronous fallback.</summary>
    [Pure]
    public static async Task<Result<T>> RecoverAsync<T>(
        this Result<T> result,
        Func<IReadOnlyList<Error>, Task<T>> recover) {
        ArgumentNullException.ThrowIfNull(recover);

        if(result.IsSuccess) return result;
        return await recover(result.Errors).ConfigureAwait(false);
    }

    // ── DoAsync & TapAsync ────────────────────────────────────────────────────

    /// <summary>Awaits <paramref name="task"/> and executes synchronous side-effect.</summary>
    public static async Task<Result<T>> DoAsync<T>(
        this Task<Result<T>> task,
        Action<T> action) {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentNullException.ThrowIfNull(action);

        Result<T> result = await task.ConfigureAwait(false);
        if(result.IsSuccess) action(result.Value);
        return result;
    }

    /// <summary>Alias for <see cref="DoAsync{T}(Task{Result{T}}, Action{T})"/>.</summary>
    public static Task<Result<T>> TapAsync<T>(this Task<Result<T>> task, Action<T> action) {
        return DoAsync(task, action);
    }

    /// <summary>Awaits <paramref name="task"/> and executes asynchronous side-effect.</summary>
    public static async Task<Result<T>> DoAsync<T>(
        this Task<Result<T>> task,
        Func<T, CancellationToken, Task> action,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentNullException.ThrowIfNull(action);

        Result<T> result = await task.ConfigureAwait(false);
        if(result.IsSuccess) {
            cancellationToken.ThrowIfCancellationRequested();
            await action(result.Value, cancellationToken).ConfigureAwait(false);
        }
        return result;
    }

    /// <summary>Alias for <see cref="DoAsync{T}(Task{Result{T}}, Func{T, CancellationToken, Task}, CancellationToken)"/>.</summary>
    public static Task<Result<T>> TapAsync<T>(
        this Task<Result<T>> task,
        Func<T, CancellationToken, Task> action,
        CancellationToken cancellationToken = default) {
        return DoAsync(task, action, cancellationToken);
    }

    /// <summary>From a synchronous result, executes asynchronous side-effect.</summary>
    public static async Task<Result<T>> DoAsync<T>(
        this Result<T> result,
        Func<T, CancellationToken, Task> action,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(action);

        if(result.IsSuccess) {
            cancellationToken.ThrowIfCancellationRequested();
            await action(result.Value, cancellationToken).ConfigureAwait(false);
        }
        return result;
    }

    /// <summary>Alias for <see cref="DoAsync{T}(Result{T}, Func{T, CancellationToken, Task}, CancellationToken)"/>.</summary>
    public static Task<Result<T>> TapAsync<T>(
        this Result<T> result,
        Func<T, CancellationToken, Task> action,
        CancellationToken cancellationToken = default) {
        return DoAsync(result, action, cancellationToken);
    }

    /// <summary>Awaits <paramref name="task"/> and executes parameterless asynchronous side-effect.</summary>
    public static async Task<Result<T>> DoAsync<T>(
        this Task<Result<T>> task,
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentNullException.ThrowIfNull(action);

        Result<T> result = await task.ConfigureAwait(false);
        if(result.IsSuccess) {
            cancellationToken.ThrowIfCancellationRequested();
            await action(cancellationToken).ConfigureAwait(false);
        }
        return result;
    }

    /// <summary>Alias for <see cref="DoAsync{T}(Task{Result{T}}, Func{CancellationToken, Task}, CancellationToken)"/>.</summary>
    public static Task<Result<T>> TapAsync<T>(
        this Task<Result<T>> task,
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken = default) {
        return DoAsync(task, action, cancellationToken);
    }

    /// <summary>From a synchronous result, executes parameterless asynchronous side-effect.</summary>
    public static async Task<Result<T>> DoAsync<T>(
        this Result<T> result,
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(action);

        if(result.IsSuccess) {
            cancellationToken.ThrowIfCancellationRequested();
            await action(cancellationToken).ConfigureAwait(false);
        }
        return result;
    }

    /// <summary>Alias for <see cref="DoAsync{T}(Result{T}, Func{CancellationToken, Task}, CancellationToken)"/>.</summary>
    public static Task<Result<T>> TapAsync<T>(
        this Result<T> result,
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken = default) {
        return DoAsync(result, action, cancellationToken);
    }

    // ── IfSuccessAsync / IfFailureAsync / TapErrorAsync ───────────────────────

    /// <summary>Awaits <paramref name="task"/> and executes synchronous success action.</summary>
    public static async Task<Result<T>> IfSuccessAsync<T>(
        this Task<Result<T>> task,
        Action<T> action,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentNullException.ThrowIfNull(action);

        Result<T> result = await task.ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        if(result.IsSuccess) action(result.Value);
        return result;
    }

    /// <summary>Awaits <paramref name="task"/> and executes asynchronous success action.</summary>
    public static async Task<Result<T>> IfSuccessAsync<T>(
        this Task<Result<T>> task,
        Func<T, CancellationToken, Task> action,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentNullException.ThrowIfNull(action);

        Result<T> result = await task.ConfigureAwait(false);
        if(result.IsSuccess) {
            cancellationToken.ThrowIfCancellationRequested();
            await action(result.Value, cancellationToken).ConfigureAwait(false);
        }
        return result;
    }

    /// <summary>Awaits <paramref name="task"/> and executes synchronous failure action.</summary>
    public static async Task<Result<T>> IfFailureAsync<T>(
        this Task<Result<T>> task,
        Action<IReadOnlyList<Error>> action,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentNullException.ThrowIfNull(action);

        Result<T> result = await task.ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        if(result.IsFailure) action(result.Errors);
        return result;
    }

    /// <summary>Alias for <see cref="IfFailureAsync{T}(Task{Result{T}}, Action{IReadOnlyList{Error}}, CancellationToken)"/>.</summary>
    public static Task<Result<T>> TapErrorAsync<T>(
        this Task<Result<T>> task,
        Action<IReadOnlyList<Error>> action,
        CancellationToken cancellationToken = default) {
        return IfFailureAsync(task, action, cancellationToken);
    }

    /// <summary>Awaits <paramref name="task"/> and executes asynchronous failure action.</summary>
    public static async Task<Result<T>> IfFailureAsync<T>(
        this Task<Result<T>> task,
        Func<IReadOnlyList<Error>, CancellationToken, Task> action,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentNullException.ThrowIfNull(action);

        Result<T> result = await task.ConfigureAwait(false);
        if(result.IsFailure) {
            cancellationToken.ThrowIfCancellationRequested();
            await action(result.Errors, cancellationToken).ConfigureAwait(false);
        }
        return result;
    }

    /// <summary>Alias for <see cref="IfFailureAsync{T}(Task{Result{T}}, Func{IReadOnlyList{Error}, CancellationToken, Task}, CancellationToken)"/>.</summary>
    public static Task<Result<T>> TapErrorAsync<T>(
        this Task<Result<T>> task,
        Func<IReadOnlyList<Error>, CancellationToken, Task> action,
        CancellationToken cancellationToken = default) {
        return IfFailureAsync(task, action, cancellationToken);
    }

    /// <summary>From a synchronous result, executes asynchronous failure action.</summary>
    public static async Task<Result<T>> IfFailureAsync<T>(
        this Result<T> result,
        Func<IReadOnlyList<Error>, CancellationToken, Task> action,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(action);

        if(result.IsFailure) {
            cancellationToken.ThrowIfCancellationRequested();
            await action(result.Errors, cancellationToken).ConfigureAwait(false);
        }
        return result;
    }

    /// <summary>Alias for <see cref="IfFailureAsync{T}(Result{T}, Func{IReadOnlyList{Error}, CancellationToken, Task}, CancellationToken)"/>.</summary>
    public static Task<Result<T>> TapErrorAsync<T>(
        this Result<T> result,
        Func<IReadOnlyList<Error>, CancellationToken, Task> action,
        CancellationToken cancellationToken = default) {
        return IfFailureAsync(result, action, cancellationToken);
    }
}