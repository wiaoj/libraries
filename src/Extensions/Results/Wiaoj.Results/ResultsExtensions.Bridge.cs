using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;

namespace Wiaoj.Results;

/// <summary>
/// Bridge and utility extensions: converting between plain values, tasks, and results;
/// null-safety helpers; and error mapping.
/// </summary>
public static partial class ResultsExtensions {

    // ── AsResult / AsTask ─────────────────────────────────────────────────────

    /// <summary>
    /// Wraps <paramref name="value"/> in a successful <see cref="Result{TValue}"/>.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="value">The value to wrap.</param>
    /// <returns>A successful <see cref="Result{TValue}"/> containing <paramref name="value"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<T> AsResult<T>(this T value) {
        return Result.Success(value);
    }

    /// <summary>
    /// Awaits <paramref name="task"/> and wraps the result in a successful
    /// <see cref="Result{TValue}"/>.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="task">A task producing a plain value.</param>
    /// <returns>
    /// A <see cref="Task{TResult}"/> that, when awaited, yields a successful
    /// <see cref="Result{TValue}"/> containing the awaited value.
    /// </returns>
    public static async Task<Result<T>> AsResult<T>(this Task<T> task) {
        T value = await task.ConfigureAwait(false);
        return Result.Success(value);
    }

    /// <summary>
    /// Wraps <paramref name="result"/> in a completed <see cref="Task{TResult}"/>.
    /// Useful when an interface requires a <see cref="Task{TResult}"/> but the
    /// value is already available synchronously.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="result">The result to wrap.</param>
    /// <returns>A completed <see cref="Task{TResult}"/> containing <paramref name="result"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task<Result<T>> AsTask<T>(this Result<T> result) {
        return Task.FromResult(result);
    }

    // ── Null safety ───────────────────────────────────────────────────────────

    /// <summary>
    /// Converts a <see cref="Result{TValue}"/> of a nullable reference type to a
    /// non-nullable <see cref="Result{TValue}"/> by ensuring the value is not
    /// <see langword="null"/>.
    /// </summary>
    /// <typeparam name="T">A reference type.</typeparam>
    /// <param name="result">A result whose value may be <see langword="null"/>.</param>
    /// <param name="error">
    /// The error to return when the value is <see langword="null"/>.
    /// </param>
    /// <returns>
    /// A non-nullable <see cref="Result{TValue}"/> on success;
    /// <paramref name="error"/> when the value is <see langword="null"/>;
    /// the original errors when the result was already a failure.
    /// </returns>
    [Pure]
    public static Result<T> EnsureNotNull<T>(
        this Result<T?> result,
        Error error) where T : class {

        if(result.IsFailure) return result.Errors.ToList();
        if(result.Value is null) return error;
        return Result.Success(result.Value);
    }

    /// <summary>
    /// Awaits <paramref name="task"/> and converts a nullable result to a
    /// non-nullable result.
    /// See <see cref="EnsureNotNull{T}(Result{T?}, Error)"/> for full semantics.
    /// </summary>
    /// <typeparam name="T">A reference type.</typeparam>
    /// <param name="task">A task producing a nullable result.</param>
    /// <param name="error">The error to return when the value is <see langword="null"/>.</param>
    /// <returns>
    /// A <see cref="Task{TResult}"/> that yields a non-nullable
    /// <see cref="Result{TValue}"/>.
    /// </returns>
    [Pure]
    public static async Task<Result<T>> EnsureNotNullAsync<T>(
        this Task<Result<T?>> task,
        Error error) where T : class {

        Result<T?> result = await task.ConfigureAwait(false);
        return result.EnsureNotNull(error);
    }

    // ── Error mapping ─────────────────────────────────────────────────────────

    /// <summary>
    /// Replaces all errors with a single <paramref name="error"/> when the result
    /// is a failure. Has no effect when the result is successful.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="result">The result to transform.</param>
    /// <param name="error">The replacement error.</param>
    /// <returns>
    /// The original successful result, or a new failure containing only
    /// <paramref name="error"/>.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Pure]
    public static Result<T> MapError<T>(this Result<T> result, Error error) {
        if(result.IsFailure) return error;
        return result;
    }

    /// <summary>
    /// Transforms the first error when the result is a failure.
    /// Has no effect when the result is successful.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="result">The result to transform.</param>
    /// <param name="errorMapper">
    /// A function that receives <see cref="Result{TValue}.FirstError"/> and returns
    /// a replacement <see cref="Error"/>.
    /// </param>
    /// <returns>
    /// The original successful result, or a new failure containing the mapped error.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Pure]
    public static Result<T> MapError<T>(
        this Result<T> result,
        Func<Error, Error> errorMapper) {

        if(result.IsFailure) return errorMapper(result.FirstError);
        return result;
    }

    // ── MapSuccess ────────────────────────────────────────────────────────────

    /// <summary>
    /// Discards the value and converts to <see cref="Result{TValue}"/> of
    /// <see cref="Success"/>.
    /// <para>
    /// Use this at the end of a chain when the caller only needs to know
    /// whether the overall operation succeeded, not what value it produced.
    /// </para>
    /// </summary>
    /// <typeparam name="T">The value type being discarded.</typeparam>
    /// <param name="result">The result whose value should be discarded.</param>
    /// <returns>
    /// <see cref="Result{TValue}"/> of <see cref="Success"/> on success,
    /// or the original errors on failure.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Pure]
    public static Result<Success> MapSuccess<T>(this Result<T> result) {
        return result.Map(_ => Success.Default);
    }

    /// <summary>
    /// LINQ Query Syntax desteği sağlar: var final = from x in result select x.Prop;
    /// </summary>
    public static Result<TResult> Select<T, TResult>(this Result<T> result, Func<T, TResult> selector) {
        return result.Map(selector);
    }

    /// <summary>
    /// LINQ Query Syntax desteği sağlar (Zincirleme sorgular için).
    /// </summary>
    public static Result<TResult> SelectMany<T, U, TResult>(
            this Result<T> result,
            Func<T, Result<U>> binder,
            Func<T, U, TResult> project) {
        return result.Then(t => binder(t).Map(u => project(t, u)));
    }
}