namespace Wiaoj.Results; 
public static partial class Result { 
    // ── Try ───────────────────────────────────────────────────────────────────

    /// <summary>
    /// Executes <paramref name="operation"/> and returns a successful
    /// <see cref="Result{TValue}"/> containing its return value.
    /// If the operation throws, the exception is caught and converted to an
    /// <see cref="Error"/> via <paramref name="exceptionHandler"/> (or
    /// <see cref="Error.FromException"/> by default).
    /// </summary>
    /// <typeparam name="T">The value type produced by <paramref name="operation"/>.</typeparam>
    /// <param name="operation">A synchronous, potentially throwing function.</param>
    /// <param name="exceptionHandler">
    /// Optional. Converts the caught exception to an <see cref="Error"/>.
    /// Defaults to <see cref="Error.FromException"/>.
    /// </param>
    /// <returns>
    /// A successful <see cref="Result{TValue}"/> on success, or a failed one
    /// containing the mapped error.
    /// </returns>
    /// <example>
    /// <code>
    /// Result&lt;int&gt; result = Result.Try(() => int.Parse(input));
    ///
    /// Result&lt;Guid&gt; id = Result.Try(
    ///     () => Guid.Parse(raw),
    ///     ex  => Error.Validation("Id.Invalid", $"'{raw}' is not a valid GUID."));
    /// </code>
    /// </example>
    public static Result<T> Try<T>(
        Func<T> operation,
        Func<Exception, Error>? exceptionHandler = null) {
        try {
            return operation();
        }
        catch(Exception ex) {
            return (exceptionHandler ?? Error.FromException)(ex);
        }
    }

    /// <summary>
    /// Executes a void <paramref name="operation"/> and returns
    /// <see cref="Result{TValue}"/> of <see cref="Success"/>.
    /// If the operation throws, the exception is converted to an <see cref="Error"/>.
    /// </summary>
    /// <param name="operation">A synchronous, potentially throwing action.</param>
    /// <param name="exceptionHandler">
    /// Optional. Converts the caught exception to an <see cref="Error"/>.
    /// Defaults to <see cref="Error.FromException"/>.
    /// </param>
    /// <returns>
    /// A successful <see cref="Result{TValue}"/> of <see cref="Success"/> on success,
    /// or a failed one containing the mapped error.
    /// </returns>
    public static Result<Success> Try(
        Action operation,
        Func<Exception, Error>? exceptionHandler = null) {
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
    /// Executes an async <paramref name="operation"/> and returns a successful
    /// <see cref="Result{TValue}"/> containing its return value.
    /// If the operation throws, the exception is caught and converted to an
    /// <see cref="Error"/>.
    /// </summary>
    /// <typeparam name="T">The value type produced by <paramref name="operation"/>.</typeparam>
    /// <param name="operation">An async, potentially throwing function.</param>
    /// <param name="exceptionHandler">
    /// Optional. Converts the caught exception to an <see cref="Error"/>.
    /// Defaults to <see cref="Error.FromException"/>.
    /// </param>
    /// <param name="cancellationToken">
    /// Forwarded to <paramref name="operation"/>.
    /// <see cref="OperationCanceledException"/> is re-thrown regardless of
    /// <paramref name="exceptionHandler"/> when this token is cancelled.
    /// </param>
    /// <returns>
    /// A successful <see cref="Result{TValue}"/> on success, or a failed one
    /// containing the mapped error.
    /// </returns>
    /// <example>
    /// <code>
    /// Result&lt;string&gt; result = await Result.TryAsync(
    ///     ct => httpClient.GetStringAsync(url, ct));
    /// </code>
    /// </example>
    public static async Task<Result<T>> TryAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        Func<Exception, Error>? exceptionHandler = null,
        CancellationToken cancellationToken = default) {
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
    /// Executes an async <paramref name="operation"/> that returns no value and
    /// returns <see cref="Result{TValue}"/> of <see cref="Success"/>.
    /// </summary>
    /// <param name="operation">An async, potentially throwing action.</param>
    /// <param name="exceptionHandler">
    /// Optional. Converts the caught exception to an <see cref="Error"/>.
    /// Defaults to <see cref="Error.FromException"/>.
    /// </param>
    /// <param name="cancellationToken">
    /// Forwarded to <paramref name="operation"/>.
    /// <see cref="OperationCanceledException"/> is re-thrown when this token is cancelled.
    /// </param>
    /// <returns>
    /// A successful <see cref="Result{TValue}"/> of <see cref="Success"/> on success,
    /// or a failed one containing the mapped error.
    /// </returns>
    public static async Task<Result<Success>> TryAsync(
        Func<CancellationToken, Task> operation,
        Func<Exception, Error>? exceptionHandler = null,
        CancellationToken cancellationToken = default) {
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
    /// Executes an async <paramref name="operation"/> (no cancellation token)
    /// and returns a successful <see cref="Result{TValue}"/>.
    /// Prefer the overload with <see cref="CancellationToken"/> for new code.
    /// </summary>
    public static async Task<Result<T>> TryAsync<T>(
        Func<Task<T>> operation,
        Func<Exception, Error>? exceptionHandler = null) {
        try {
            return await operation().ConfigureAwait(false);
        }
        catch(Exception ex) {
            return (exceptionHandler ?? Error.FromException)(ex);
        }
    }
}