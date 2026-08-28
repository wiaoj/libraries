using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;

namespace Wiaoj.Results;

public static partial class ResultsExtensions {

    // ── Ensure (Synchronous) ──────────────────────────────────────────────────

    /// <summary>
    /// Validates a condition against the value. Returns <paramref name="error"/> when false.
    /// </summary>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<T> Ensure<T>(
        this Result<T> result,
        Func<T, bool> predicate,
        Error error) {
        ArgumentNullException.ThrowIfNull(predicate);

        if(result.IsFailure) return result;
        if(!predicate(result.Value)) return error;
        return result;
    }

    /// <summary>
    /// Validates a condition against the value, constructing the error lazily on failure.
    /// </summary>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<T> Ensure<T>(
        this Result<T> result,
        Func<T, bool> predicate,
        Func<T, Error> errorFactory) {
        ArgumentNullException.ThrowIfNull(predicate);
        ArgumentNullException.ThrowIfNull(errorFactory);

        if(result.IsFailure) return result;
        if(!predicate(result.Value)) return errorFactory(result.Value);
        return result;
    }

    /// <summary>
    /// Validates a value-independent condition. Returns <paramref name="error"/> when false.
    /// </summary>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<T> Ensure<T>(
        this Result<T> result,
        Func<bool> predicate,
        Error error) {
        ArgumentNullException.ThrowIfNull(predicate);

        if(result.IsFailure) return result;
        if(!predicate()) return error;
        return result;
    }

    /// <summary>
    /// Validates a value-independent condition, constructing the error lazily on failure.
    /// </summary>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<T> Ensure<T>(
        this Result<T> result,
        Func<bool> predicate,
        Func<Error> errorFactory) {
        ArgumentNullException.ThrowIfNull(predicate);
        ArgumentNullException.ThrowIfNull(errorFactory);

        if(result.IsFailure) return result;
        if(!predicate()) return errorFactory();
        return result;
    }

    // ── EnsureAsync ───────────────────────────────────────────────────────────

    /// <summary>
    /// Awaits <paramref name="task"/> and validates the value with a synchronous predicate.
    /// </summary>
    [Pure]
    public static async Task<Result<T>> EnsureAsync<T>(
        this Task<Result<T>> task,
        Func<T, bool> predicate,
        Error error) {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentNullException.ThrowIfNull(predicate);

        Result<T> result = await task.ConfigureAwait(false);
        if(result.IsFailure) return result;
        if(!predicate(result.Value)) return error;
        return result;
    }

    /// <summary>
    /// Awaits <paramref name="task"/> and validates the value with an asynchronous predicate.
    /// </summary>
    [Pure]
    public static async Task<Result<T>> EnsureAsync<T>(
        this Task<Result<T>> task,
        Func<T, Task<bool>> predicate,
        Error error) {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentNullException.ThrowIfNull(predicate);

        Result<T> result = await task.ConfigureAwait(false);
        if(result.IsFailure) return result;
        if(!await predicate(result.Value).ConfigureAwait(false)) return error;
        return result;
    }

    /// <summary>
    /// From a synchronous result, validates the value with an asynchronous predicate.
    /// </summary>
    [Pure]
    public static async Task<Result<T>> EnsureAsync<T>(
        this Result<T> result,
        Func<T, Task<bool>> predicate,
        Error error) {
        ArgumentNullException.ThrowIfNull(predicate);

        if(result.IsFailure) return result;
        if(!await predicate(result.Value).ConfigureAwait(false)) return error;
        return result;
    }

    /// <summary>
    /// Awaits <paramref name="task"/> and validates the value using an asynchronous predicate and lazy error factory.
    /// </summary>
    [Pure]
    public static async Task<Result<T>> EnsureAsync<T>(
        this Task<Result<T>> task,
        Func<T, Task<bool>> predicate,
        Func<T, Task<Error>> errorFactory) {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentNullException.ThrowIfNull(predicate);
        ArgumentNullException.ThrowIfNull(errorFactory);

        Result<T> result = await task.ConfigureAwait(false);
        if(result.IsFailure) return result;

        if(!await predicate(result.Value).ConfigureAwait(false))
            return await errorFactory(result.Value).ConfigureAwait(false);

        return result;
    }

    /// <summary>
    /// From a synchronous result, validates the value using an asynchronous predicate and lazy error factory.
    /// </summary>
    [Pure]
    public static async Task<Result<T>> EnsureAsync<T>(
        this Result<T> result,
        Func<T, Task<bool>> predicate,
        Func<T, Task<Error>> errorFactory) {
        ArgumentNullException.ThrowIfNull(predicate);
        ArgumentNullException.ThrowIfNull(errorFactory);

        if(result.IsFailure) return result;

        if(!await predicate(result.Value).ConfigureAwait(false))
            return await errorFactory(result.Value).ConfigureAwait(false);

        return result;
    }
}