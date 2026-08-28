using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;

namespace Wiaoj.Results;

/// <summary>
/// Bridge and utility extensions: converting between plain values, tasks, and results;
/// null-safety helpers; two-track transformations; and error mapping.
/// </summary>
public static partial class ResultsExtensions {

    // ── AsResult / AsTask ─────────────────────────────────────────────────────

    /// <summary>Wraps <paramref name="value"/> in a successful <see cref="Result{TValue}"/>.</summary>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<T> AsResult<T>(this T value) {
        return Result.Success(value);
    }

    /// <summary>Awaits <paramref name="task"/> and wraps the result in a successful <see cref="Result{TValue}"/>.</summary>
    [Pure]
    public static async Task<Result<T>> AsResult<T>(this Task<T> task) {
        ArgumentNullException.ThrowIfNull(task);
        T value = await task.ConfigureAwait(false);
        return Result.Success(value);
    }

    /// <summary>Wraps <paramref name="result"/> in a completed <see cref="Task{TResult}"/>.</summary>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task<Result<T>> AsTask<T>(this Result<T> result) {
        return Task.FromResult(result);
    }

    // ── Null Safety (Reference Types) ─────────────────────────────────────────

    /// <summary>
    /// Ensures that a nullable reference type value is not <see langword="null"/>.
    /// </summary>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<T> EnsureNotNull<T>(
        this Result<T?> result,
        Error error) where T : class {
        if(result.IsFailure) return result.ToFailure<T>();
        if(result.Value is null) return error;
        return Result.Success(result.Value);
    }

    /// <summary>
    /// Ensures that a nullable reference type value is not <see langword="null"/>, generating error lazily.
    /// </summary>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<T> EnsureNotNull<T>(
        this Result<T?> result,
        Func<Error> errorFactory) where T : class {
        ArgumentNullException.ThrowIfNull(errorFactory);
        if(result.IsFailure) return result.ToFailure<T>();
        if(result.Value is null) return errorFactory();
        return Result.Success(result.Value);
    }

    /// <summary>
    /// Awaits <paramref name="task"/> and ensures the reference type value is not <see langword="null"/>.
    /// </summary>
    [Pure]
    public static async Task<Result<T>> EnsureNotNullAsync<T>(
        this Task<Result<T?>> task,
        Error error) where T : class {
        ArgumentNullException.ThrowIfNull(task);
        Result<T?> result = await task.ConfigureAwait(false);
        return result.EnsureNotNull(error);
    }

    /// <summary>
    /// Awaits <paramref name="task"/> and ensures the reference type value is not <see langword="null"/>, generating error lazily.
    /// </summary>
    [Pure]
    public static async Task<Result<T>> EnsureNotNullAsync<T>(
        this Task<Result<T?>> task,
        Func<Error> errorFactory) where T : class {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentNullException.ThrowIfNull(errorFactory);
        Result<T?> result = await task.ConfigureAwait(false);
        return result.EnsureNotNull(errorFactory);
    }

    // ── Null Safety (Value Types) ─────────────────────────────────────────────

    /// <summary>
    /// Ensures that a nullable value type has a value.
    /// </summary>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<T> EnsureNotNull<T>(
        this Result<T?> result,
        Error error) where T : struct {
        if(result.IsFailure) return result.ToFailure<T>();
        if(!result.Value.HasValue) return error;
        return Result.Success(result.Value.Value);
    }

    /// <summary>
    /// Ensures that a nullable value type has a value, generating error lazily.
    /// </summary>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<T> EnsureNotNull<T>(
        this Result<T?> result,
        Func<Error> errorFactory) where T : struct {
        ArgumentNullException.ThrowIfNull(errorFactory);
        if(result.IsFailure) return result.ToFailure<T>();
        if(!result.Value.HasValue) return errorFactory();
        return Result.Success(result.Value.Value);
    }

    /// <summary>
    /// Awaits <paramref name="task"/> and ensures the value type has a value.
    /// </summary>
    [Pure]
    public static async Task<Result<T>> EnsureNotNullAsync<T>(
        this Task<Result<T?>> task,
        Error error) where T : struct {
        ArgumentNullException.ThrowIfNull(task);
        Result<T?> result = await task.ConfigureAwait(false);
        return result.EnsureNotNull(error);
    }

    /// <summary>
    /// Awaits <paramref name="task"/> and ensures the value type has a value, generating error lazily.
    /// </summary>
    [Pure]
    public static async Task<Result<T>> EnsureNotNullAsync<T>(
        this Task<Result<T?>> task,
        Func<Error> errorFactory) where T : struct {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentNullException.ThrowIfNull(errorFactory);
        Result<T?> result = await task.ConfigureAwait(false);
        return result.EnsureNotNull(errorFactory);
    }

    // ── BiMap (Two-Track Transformation) ───────────────────────────────────────

    /// <summary>
    /// Transforms the success value using <paramref name="mapSuccess"/> or the error list using <paramref name="mapError"/>.
    /// </summary>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<TNew> BiMap<T, TNew>(
        this Result<T> result,
        Func<T, TNew> mapSuccess,
        Func<IReadOnlyList<Error>, Error> mapError) {
        ArgumentNullException.ThrowIfNull(mapSuccess);
        ArgumentNullException.ThrowIfNull(mapError);

        return result.IsSuccess
            ? Result.Success(mapSuccess(result.Value))
            : Result.Failure<TNew>(mapError(result.Errors));
    }

    /// <summary>
    /// Awaits <paramref name="task"/> and applies two-track transformation.
    /// </summary>
    [Pure]
    public static async Task<Result<TNew>> BiMapAsync<T, TNew>(
        this Task<Result<T>> task,
        Func<T, TNew> mapSuccess,
        Func<IReadOnlyList<Error>, Error> mapError) {
        ArgumentNullException.ThrowIfNull(task);
        Result<T> result = await task.ConfigureAwait(false);
        return result.BiMap(mapSuccess, mapError);
    }

    // ── Error Mapping ─────────────────────────────────────────────────────────

    /// <summary>Replaces all errors with a single error when failed.</summary>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<T> MapError<T>(this Result<T> result, Error error) {
        if(result.IsFailure) return error;
        return result;
    }

    /// <summary>Transforms the first error when failed.</summary>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<T> MapError<T>(this Result<T> result, Func<Error, Error> errorMapper) {
        ArgumentNullException.ThrowIfNull(errorMapper);
        if(result.IsFailure) return errorMapper(result.FirstError);
        return result;
    }

    // ── MapSuccess ────────────────────────────────────────────────────────────

    /// <summary>Discards the value and converts to a successful <see cref="Success"/> result.</summary>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<Success> MapSuccess<T>(this Result<T> result) {
        if(result.IsFailure) return result.ToFailure<Success>();
        return Wiaoj.Results.Success.Default;
    }

    // ── LINQ Query Syntax ─────────────────────────────────────────────────────

    /// <summary>Provides LINQ query expression support (<c>select</c>).</summary>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<TResult> Select<T, TResult>(this Result<T> result, Func<T, TResult> selector) {
        return result.Map(selector);
    }

    /// <summary>Provides LINQ query expression support for multiple <c>from</c> clauses.</summary>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<TResult> SelectMany<T, U, TResult>(
        this Result<T> result,
        Func<T, Result<U>> binder,
        Func<T, U, TResult> project) {
        ArgumentNullException.ThrowIfNull(binder);
        ArgumentNullException.ThrowIfNull(project);

        return result.Then(t => binder(t).Map(u => project(t, u)));
    }
}