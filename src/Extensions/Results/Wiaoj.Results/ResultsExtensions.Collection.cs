namespace Wiaoj.Results;
/// <summary>
/// Extension methods for working with collections of <see cref="Result{TValue}"/>
/// and for converting nullable values to <see cref="Result{TValue}"/>.
/// </summary>
public static class ResultsCollectionExtensions {

    // ── IEnumerable<Result<T>> ────────────────────────────────────────────────

    /// <summary>
    /// Evaluates every result in <paramref name="source"/> and returns a successful
    /// <see cref="Result{TValue}"/> containing a read-only list of all values when
    /// every result succeeds. If any result fails, all errors from all failing
    /// results are collected and returned together.
    /// </summary>
    /// <typeparam name="T">The value type of each result.</typeparam>
    /// <param name="source">The sequence of results to evaluate.</param>
    /// <returns>
    /// A successful <see cref="Result{TValue}"/> containing all values;
    /// a failed <see cref="Result{TValue}"/> containing every error from every
    /// failing result otherwise.
    /// </returns>
    /// <example>
    /// <code>
    /// IEnumerable&lt;Result&lt;User&gt;&gt; results = ids.Select(id => FindUser(id));
    /// Result&lt;IReadOnlyList&lt;User&gt;&gt; all = results.Combine();
    /// </code>
    /// </example>
    public static Result<IReadOnlyList<T>> Combine<T>(
        this IEnumerable<Result<T>> source) {

        List<T>? values = null;
        List<Error>? errors = null;

        foreach(Result<T> result in source) {
            if(result.IsSuccess) {
                values ??= [];
                values.Add(result.Value);
            }
            else {
                errors ??= [];
                errors.AddRange(result.Errors);
            }
        }

        if(errors is not null)
            return errors;

        return values ?? [];
    }

    /// <summary>
    /// Returns only the values from the successful results in <paramref name="source"/>,
    /// silently discarding any failures.
    /// <para>
    /// Use this when partial success is acceptable — e.g., enriching a list where
    /// some enrichments may fail without invalidating the whole operation.
    /// </para>
    /// </summary>
    /// <typeparam name="T">The value type of each result.</typeparam>
    /// <param name="source">The sequence of results to filter.</param>
    /// <returns>
    /// A sequence containing only the unwrapped values from successful results.
    /// </returns>
    public static IEnumerable<T> WhereSuccess<T>(
        this IEnumerable<Result<T>> source) {

        foreach(Result<T> result in source) {
            if(result.IsSuccess)
                yield return result.Value;
        }
    }

    /// <summary>
    /// Returns only the errors from the failing results in <paramref name="source"/>,
    /// silently discarding any successes.
    /// <para>
    /// Useful for collecting validation errors from a batch operation.
    /// </para>
    /// </summary>
    /// <typeparam name="T">The value type of each result.</typeparam>
    /// <param name="source">The sequence of results to filter.</param>
    /// <returns>
    /// A flattened sequence of every <see cref="Error"/> from every failing result.
    /// </returns>
    public static IEnumerable<Error> WhereFailure<T>(
        this IEnumerable<Result<T>> source) {

        foreach(Result<T> result in source) {
            if(result.IsError) {
                foreach(Error error in result.Errors)
                    yield return error;
            }
        }
    }

    // ── Nullable → Result ─────────────────────────────────────────────────────

    /// <summary>
    /// Converts a nullable reference type to a <see cref="Result{TValue}"/>.
    /// Returns <paramref name="error"/> when <paramref name="value"/> is
    /// <see langword="null"/>.
    /// </summary>
    /// <typeparam name="T">A reference type.</typeparam>
    /// <param name="value">The nullable value to convert.</param>
    /// <param name="error">The error to use when <paramref name="value"/> is null.</param>
    /// <returns>
    /// A successful <see cref="Result{TValue}"/> containing the non-null value;
    /// a failed <see cref="Result{TValue}"/> containing <paramref name="error"/>
    /// when the value is null.
    /// </returns>
    /// <example>
    /// <code>
    /// Result&lt;User&gt; result = await _db.Users.FindAsync(id).ToResult(
    ///     Error.NotFound("User", id));
    /// </code>
    /// </example>
    public static Result<T> ToResult<T>(
        this T? value,
        Error error) where T : class {
        return value is null ? error : value;
    }

    /// <summary>
    /// Converts a nullable value type to a <see cref="Result{TValue}"/>.
    /// Returns <paramref name="error"/> when <paramref name="value"/> has no value.
    /// </summary>
    /// <typeparam name="T">A value type.</typeparam>
    /// <param name="value">The nullable struct to convert.</param>
    /// <param name="error">The error to use when <paramref name="value"/> is null.</param>
    /// <returns>
    /// A successful <see cref="Result{TValue}"/> containing the unwrapped value;
    /// a failed <see cref="Result{TValue}"/> containing <paramref name="error"/>
    /// when the value is null.
    /// </returns>
    /// <example>
    /// <code>
    /// Result&lt;int&gt; result = int.TryParse(input, out int v) ? v : (int?)null;
    /// // or more idiomatically:
    /// Result&lt;int&gt; result = parsed.ToResult(Error.Validation("Age.Invalid", "..."));
    /// </code>
    /// </example>
    public static Result<T> ToResult<T>(
        this T? value,
        Error error) where T : struct {
        return value.HasValue ? value.Value : error;
    }

    /// <summary>
    /// Converts a nullable reference type to a <see cref="Result{TValue}"/>,
    /// producing the error lazily via <paramref name="errorFactory"/>.
    /// Use this when constructing the error is expensive or requires context.
    /// </summary>
    /// <typeparam name="T">A reference type.</typeparam>
    /// <param name="value">The nullable value to convert.</param>
    /// <param name="errorFactory">
    /// A factory invoked only when <paramref name="value"/> is null.
    /// </param>
    /// <returns>
    /// A successful <see cref="Result{TValue}"/> or the lazily-produced error.
    /// </returns>
    public static Result<T> ToResult<T>(
        this T? value,
        Func<Error> errorFactory) where T : class {
        return value is null ? errorFactory() : value;
    }

    /// <summary>
    /// Converts a nullable value type to a <see cref="Result{TValue}"/>,
    /// producing the error lazily via <paramref name="errorFactory"/>.
    /// </summary>
    /// <typeparam name="T">A value type.</typeparam>
    /// <param name="value">The nullable struct to convert.</param>
    /// <param name="errorFactory">A factory invoked only when the value has no value.</param>
    public static Result<T> ToResult<T>(
        this T? value,
        Func<Error> errorFactory) where T : struct {
        return value.HasValue ? value.Value : errorFactory();
    }

    // ── ValueTask<T> → Result<T> ──────────────────────────────────────────────

    /// <summary>
    /// Awaits a <see cref="ValueTask{TResult}"/> of a plain value and wraps the
    /// result in a successful <see cref="Result{TValue}"/>.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="valueTask">A <see cref="ValueTask{TResult}"/> producing a plain value.</param>
    /// <returns>
    /// A <see cref="ValueTask{TResult}"/> that yields a successful
    /// <see cref="Result{TValue}"/> containing the awaited value.
    /// </returns>
    /// <example>
    /// <code>
    /// Result&lt;string&gt; result = await cache.GetAsync(key).AsResult();
    /// </code>
    /// </example>
    public static async ValueTask<Result<T>> AsResult<T>(
        this ValueTask<T> valueTask) {
        T value = await valueTask.ConfigureAwait(false);
        return Result.Success(value);
    }

    /// <summary>
    /// Awaits a <see cref="ValueTask{TResult}"/> of a nullable reference type and
    /// converts the result using <see cref="ToResult{T}(T?, Error)"/>.
    /// </summary>
    /// <typeparam name="T">A reference type.</typeparam>
    /// <param name="valueTask">A <see cref="ValueTask{TResult}"/> producing a nullable value.</param>
    /// <param name="error">The error to use when the awaited value is null.</param>
    /// <returns>
    /// A <see cref="ValueTask{TResult}"/> that yields a <see cref="Result{TValue}"/>.
    /// </returns>
    public static async ValueTask<Result<T>> AsResult<T>(
        this ValueTask<T?> valueTask,
        Error error) where T : class {
        T? value = await valueTask.ConfigureAwait(false);
        return value.ToResult(error);
    }
}