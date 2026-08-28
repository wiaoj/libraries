using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;

namespace Wiaoj.Results;

/// <summary>
/// Represents a function that attempts an operation on an input and produces an output via an out parameter.
/// </summary>
/// <typeparam name="TIn">The type of the input argument.</typeparam>
/// <typeparam name="TOut">The type of the output value produced.</typeparam>
/// <param name="input">The input value to process.</param>
/// <param name="result">When this method returns, contains the produced value if the operation succeeded, or the default value if it failed.</param>
/// <returns><see langword="true"/> if the operation succeeded; otherwise, <see langword="false"/>.</returns>
public delegate bool TryFunc<TIn, TOut>(TIn input, [MaybeNullWhen(false)] out TOut result);

/// <summary>
/// Represents a parameterless function that attempts an operation and produces an output via an out parameter.
/// </summary>
/// <typeparam name="TOut">The type of the output value produced.</typeparam>
/// <param name="result">When this method returns, contains the produced value if the operation succeeded, or the default value if it failed.</param>
/// <returns><see langword="true"/> if the operation succeeded; otherwise, <see langword="false"/>.</returns>
public delegate bool TryFunc<TOut>([MaybeNullWhen(false)] out TOut result);

public static partial class Result {

    // ── Parse (IParsable<T>) ──────────────────────────────────────────────────

    /// <summary>
    /// Parses a string into <typeparamref name="T"/> using its <see cref="IParsable{TSelf}"/> implementation.
    /// </summary>
    /// <typeparam name="T">The target type that implements <see cref="IParsable{TSelf}"/>.</typeparam>
    /// <param name="input">The string representation to parse.</param>
    /// <param name="error">The error returned when parsing fails.</param>
    /// <param name="formatProvider">An optional format provider.</param>
    /// <returns>A successful <see cref="Result{TValue}"/> containing the parsed value, or <paramref name="error"/> on failure.</returns>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<T> Parse<T>(
        string? input,
        Error error,
        IFormatProvider? formatProvider = null) where T : IParsable<T> {
        return T.TryParse(input, formatProvider, out T? result)
            ? result!
            : error;
    }

    /// <summary>
    /// Parses a string into <typeparamref name="T"/> using its <see cref="IParsable{TSelf}"/> implementation,
    /// constructing the error lazily via <paramref name="errorFactory"/> on failure.
    /// </summary>
    /// <typeparam name="T">The target type that implements <see cref="IParsable{TSelf}"/>.</typeparam>
    /// <param name="input">The string representation to parse.</param>
    /// <param name="errorFactory">A factory invoked only on failure to create a contextual error.</param>
    /// <param name="formatProvider">An optional format provider.</param>
    /// <returns>A successful <see cref="Result{TValue}"/> containing the parsed value, or the lazily created error.</returns>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<T> Parse<T>(
        string? input,
        Func<string?, Error> errorFactory,
        IFormatProvider? formatProvider = null) where T : IParsable<T> {
        ArgumentNullException.ThrowIfNull(errorFactory);

        return T.TryParse(input, formatProvider, out T? result)
            ? result!
            : errorFactory(input);
    }

    /// <summary>
    /// Parses a character span into <typeparamref name="T"/> using its <see cref="ISpanParsable{TSelf}"/> implementation.
    /// </summary>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<T> Parse<T>(
        ReadOnlySpan<char> input,
        Error error,
        IFormatProvider? formatProvider = null) where T : ISpanParsable<T> {
        return T.TryParse(input, formatProvider, out T? result)
            ? result!
            : error;
    }

    // ── FromTry ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Executes a try-pattern function that takes an input and produces an output via an out parameter,
    /// returning a successful <see cref="Result{TValue}"/> on success or <paramref name="error"/> on failure.
    /// </summary>
    /// <typeparam name="TIn">The type of the input argument.</typeparam>
    /// <typeparam name="TOut">The type of the output value.</typeparam>
    /// <param name="input">The input value to evaluate.</param>
    /// <param name="tryOperation">The try-pattern delegate (e.g., custom TryDecode or TryGetValue methods).</param>
    /// <param name="error">The error returned when the operation fails.</param>
    /// <returns>A successful <see cref="Result{TValue}"/> or <paramref name="error"/>.</returns>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<TOut> FromTry<TIn, TOut>(
        TIn input,
        TryFunc<TIn, TOut> tryOperation,
        Error error) {
        ArgumentNullException.ThrowIfNull(tryOperation);

        return tryOperation(input, out TOut? value)
            ? value!
            : error;
    }

    /// <summary>
    /// Executes a try-pattern function that takes an input and produces an output via an out parameter,
    /// generating the error lazily via <paramref name="errorFactory"/> only when the operation fails.
    /// </summary>
    /// <typeparam name="TIn">The type of the input argument.</typeparam>
    /// <typeparam name="TOut">The type of the output value.</typeparam>
    /// <param name="input">The input value to evaluate.</param>
    /// <param name="tryOperation">The try-pattern delegate.</param>
    /// <param name="errorFactory">A factory invoked only on failure.</param>
    /// <returns>A successful <see cref="Result{TValue}"/> or the lazily constructed error.</returns>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<TOut> FromTry<TIn, TOut>(
        TIn input,
        TryFunc<TIn, TOut> tryOperation,
        Func<TIn, Error> errorFactory) {
        ArgumentNullException.ThrowIfNull(tryOperation);
        ArgumentNullException.ThrowIfNull(errorFactory);

        return tryOperation(input, out TOut? value)
            ? value!
            : errorFactory(input);
    }

    /// <summary>
    /// Executes a parameterless try-pattern function, returning a successful <see cref="Result{TValue}"/>
    /// on success or <paramref name="error"/> on failure.
    /// </summary>
    /// <typeparam name="TOut">The type of the output value.</typeparam>
    /// <param name="tryOperation">The parameterless try-pattern delegate.</param>
    /// <param name="error">The error returned when the operation fails.</param>
    /// <returns>A successful <see cref="Result{TValue}"/> or <paramref name="error"/>.</returns>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<TOut> FromTry<TOut>(
        TryFunc<TOut> tryOperation,
        Error error) {
        ArgumentNullException.ThrowIfNull(tryOperation);

        return tryOperation(out TOut? value)
            ? value!
            : error;
    }

    /// <summary>
    /// Executes a parameterless try-pattern function, generating the error lazily via
    /// <paramref name="errorFactory"/> only when the operation fails.
    /// </summary>
    /// <typeparam name="TOut">The type of the output value.</typeparam>
    /// <param name="tryOperation">The parameterless try-pattern delegate.</param>
    /// <param name="errorFactory">A factory invoked only on failure.</param>
    /// <returns>A successful <see cref="Result{TValue}"/> or the lazily constructed error.</returns>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<TOut> FromTry<TOut>(
        TryFunc<TOut> tryOperation,
        Func<Error> errorFactory) {
        ArgumentNullException.ThrowIfNull(tryOperation);
        ArgumentNullException.ThrowIfNull(errorFactory);

        return tryOperation(out TOut? value)
            ? value!
            : errorFactory();
    }

    // ── Try ───────────────────────────────────────────────────────────────────

    /// <summary>
    /// Executes <paramref name="operation"/> and returns a successful <see cref="Result{TValue}"/> containing its return value.
    /// If the operation throws, the exception is caught and converted to an <see cref="Error"/> via <paramref name="exceptionHandler"/>.
    /// </summary>
    /// <typeparam name="T">The value type produced by <paramref name="operation"/>.</typeparam>
    /// <param name="operation">A synchronous, potentially throwing function.</param>
    /// <param name="exceptionHandler">Optional handler to convert the caught exception to an <see cref="Error"/>.</param>
    /// <returns>A successful <see cref="Result{TValue}"/> on success, or a failed result containing the mapped error.</returns>
    [Pure]
    public static Result<T> Try<T>(
        Func<T> operation,
        Func<Exception, Error>? exceptionHandler = null) {
        ArgumentNullException.ThrowIfNull(operation);

        try {
            return operation();
        }
        catch(Exception ex) {
            return (exceptionHandler ?? Error.FromException)(ex);
        }
    }

    /// <summary>
    /// Executes a void <paramref name="operation"/> and returns a <see cref="Result{TValue}"/> of <see cref="Success"/>.
    /// If the operation throws, the exception is converted to an <see cref="Error"/>.
    /// </summary>
    /// <param name="operation">A synchronous, potentially throwing action.</param>
    /// <param name="exceptionHandler">Optional handler to convert the caught exception to an <see cref="Error"/>.</param>
    /// <returns>A successful <see cref="Result{TValue}"/> of <see cref="Success"/> on success, or a failed result containing the mapped error.</returns>
    public static Result<Success> Try(
        Action operation,
        Func<Exception, Error>? exceptionHandler = null) {
        ArgumentNullException.ThrowIfNull(operation);

        try {
            operation();
            return Wiaoj.Results.Success.Default;
        }
        catch(Exception ex) {
            return (exceptionHandler ?? Error.FromException)(ex);
        }
    }

    // ── TryAsync ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Executes an async <paramref name="operation"/> and returns a successful <see cref="Result{TValue}"/> containing its return value.
    /// </summary>
    public static async Task<Result<T>> TryAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        Func<Exception, Error>? exceptionHandler = null,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(operation);

        try {
            return await operation(cancellationToken).ConfigureAwait(false);
        }
        catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested) {
            throw;
        }
        catch(Exception ex) {
            return (exceptionHandler ?? Error.FromException)(ex);
        }
    }

    /// <summary>
    /// Executes an async <paramref name="operation"/> that returns no value and returns a <see cref="Result{TValue}"/> of <see cref="Success"/>.
    /// </summary>
    public static async Task<Result<Success>> TryAsync(
        Func<CancellationToken, Task> operation,
        Func<Exception, Error>? exceptionHandler = null,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(operation);

        try {
            await operation(cancellationToken).ConfigureAwait(false);
            return Wiaoj.Results.Success.Default;
        }
        catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested) {
            throw;
        }
        catch(Exception ex) {
            return (exceptionHandler ?? Error.FromException)(ex);
        }
    }

    /// <summary>
    /// Executes an async <paramref name="operation"/> without a cancellation token and returns a successful <see cref="Result{TValue}"/>.
    /// </summary>
    public static async Task<Result<T>> TryAsync<T>(
        Func<Task<T>> operation,
        Func<Exception, Error>? exceptionHandler = null) {
        ArgumentNullException.ThrowIfNull(operation);

        try {
            return await operation().ConfigureAwait(false);
        }
        catch(Exception ex) {
            return (exceptionHandler ?? Error.FromException)(ex);
        }
    }
}