using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;

namespace Wiaoj.Results;

/// <summary>
/// Extension methods for working with collections of <see cref="Result{TValue}"/>
/// and for converting nullable values to <see cref="Result{TValue}"/>.
/// </summary>
public static class ResultsCollectionExtensions {

    // ── Partition ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Evaluates the sequence in a single pass and splits the results into two separate collections:
    /// one containing all successful unwrapped values, and another containing all flattened errors.
    /// </summary>
    /// <typeparam name="T">The value type of each result.</typeparam>
    /// <param name="source">The sequence of results to partition.</param>
    /// <returns>
    /// A tuple containing:
    /// <list type="bullet">
    ///   <item><description><c>Successes</c>: A read-only list containing all unwrapped values from successful results.</description></item>
    ///   <item><description><c>Failures</c>: A read-only list containing all errors from failing results.</description></item>
    /// </list>
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> is <see langword="null"/>.</exception>
    /// <example>
    /// <code>
    /// var (users, errors) = fetchResults.Partition();
    /// </code>
    /// </example>
    [Pure]
    public static (IReadOnlyList<T> Successes, IReadOnlyList<Error> Failures) Partition<T>(
        this IEnumerable<Result<T>> source) {
        ArgumentNullException.ThrowIfNull(source);

        List<T> successes = [];
        List<Error> failures = [];

        foreach(Result<T> result in source) {
            if(result.IsSuccess) {
                successes.Add(result.Value);
            }
            else {
                failures.AddRange(result.Errors);
            }
        }

        return (successes, failures);
    }

    // ── IEnumerable<Result<T>> ────────────────────────────────────────────────

    /// <summary>
    /// Evaluates every result in <paramref name="source"/> and returns a successful
    /// <see cref="Result{TValue}"/> containing a read-only list of all values when
    /// every result succeeds. If any result fails, all errors from all failing
    /// results are collected and returned together.
    /// </summary>
    [Pure]
    public static Result<IReadOnlyList<T>> Combine<T>(
        this IEnumerable<Result<T>> source) {
        ArgumentNullException.ThrowIfNull(source);

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
    /// </summary>
    [Pure]
    public static IEnumerable<T> WhereSuccess<T>(
        this IEnumerable<Result<T>> source) {
        ArgumentNullException.ThrowIfNull(source);

        foreach(Result<T> result in source) {
            if(result.IsSuccess)
                yield return result.Value;
        }
    }

    /// <summary>
    /// Returns only the errors from the failing results in <paramref name="source"/>,
    /// silently discarding any successes.
    /// </summary>
    [Pure]
    public static IEnumerable<Error> WhereFailure<T>(
        this IEnumerable<Result<T>> source) {
        ArgumentNullException.ThrowIfNull(source);

        foreach(Result<T> result in source) {
            if(result.IsFailure) {
                foreach(Error error in result.Errors)
                    yield return error;
            }
        }
    }

    // ── Nullable → Result ─────────────────────────────────────────────────────

    /// <summary>
    /// Converts a nullable reference type to a <see cref="Result{TValue}"/>.
    /// Returns <paramref name="error"/> when <paramref name="value"/> is <see langword="null"/>.
    /// </summary>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<T> ToResult<T>(
        this T? value,
        Error error) where T : class {
        return value is null ? error : value;
    }

    /// <summary>
    /// Converts a nullable value type to a <see cref="Result{TValue}"/>.
    /// Returns <paramref name="error"/> when <paramref name="value"/> has no value.
    /// </summary>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<T> ToResult<T>(
        this T? value,
        Error error) where T : struct {
        return value.HasValue ? value.Value : error;
    }

    /// <summary>
    /// Converts a nullable reference type to a <see cref="Result{TValue}"/>,
    /// producing the error lazily via <paramref name="errorFactory"/>.
    /// </summary>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<T> ToResult<T>(
        this T? value,
        Func<Error> errorFactory) where T : class {
        ArgumentNullException.ThrowIfNull(errorFactory);
        return value is null ? errorFactory() : value;
    }

    /// <summary>
    /// Converts a nullable value type to a <see cref="Result{TValue}"/>,
    /// producing the error lazily via <paramref name="errorFactory"/>.
    /// </summary>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<T> ToResult<T>(
        this T? value,
        Func<Error> errorFactory) where T : struct {
        ArgumentNullException.ThrowIfNull(errorFactory);
        return value.HasValue ? value.Value : errorFactory();
    }

    // ── ValueTask<T> → Result<T> ──────────────────────────────────────────────

    /// <summary>
    /// Awaits a <see cref="ValueTask{TResult}"/> of a plain value and wraps the
    /// result in a successful <see cref="Result{TValue}"/>.
    /// </summary>
    public static async ValueTask<Result<T>> AsResult<T>(
        this ValueTask<T> valueTask) {
        T value = await valueTask.ConfigureAwait(false);
        return Result.Success(value);
    }

    /// <summary>
    /// Awaits a <see cref="ValueTask{TResult}"/> of a nullable reference type and
    /// converts the result using <see cref="ToResult{T}(T?, Error)"/>.
    /// </summary>
    public static async ValueTask<Result<T>> AsResult<T>(
        this ValueTask<T?> valueTask,
        Error error) where T : class {
        T? value = await valueTask.ConfigureAwait(false);
        return value.ToResult(error);
    }

    // ── IEnumerable<Task<Result<T>>> (Async Task Collections) ─────────────────

    /// <summary>
    /// Awaits all asynchronous tasks in parallel and combines their results into a single list.
    /// </summary>
    [Pure]
    public static async Task<Result<IReadOnlyList<T>>> CombineAsync<T>(
        this IEnumerable<Task<Result<T>>> tasks) {
        ArgumentNullException.ThrowIfNull(tasks);

        Result<T>[] results = await Task.WhenAll(tasks).ConfigureAwait(false);
        return results.Combine();
    }

    /// <summary>
    /// Awaits all asynchronous tasks in parallel and partitions their results into successes and failures.
    /// </summary>
    [Pure]
    public static async Task<(IReadOnlyList<T> Successes, IReadOnlyList<Error> Failures)> PartitionAsync<T>(
        this IEnumerable<Task<Result<T>>> tasks) {
        ArgumentNullException.ThrowIfNull(tasks);

        Result<T>[] results = await Task.WhenAll(tasks).ConfigureAwait(false);
        return results.Partition();
    }
}