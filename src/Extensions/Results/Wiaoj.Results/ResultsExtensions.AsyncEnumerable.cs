using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;

namespace Wiaoj.Results;

/// <summary>
/// Asynchronous stream extension methods for working with <see cref="IAsyncEnumerable{T}"/> of <see cref="Result{TValue}"/>.
/// </summary>
public static class ResultsAsyncEnumerableExtensions {

    // ── WhereSuccess / WhereFailure ───────────────────────────────────────────

    /// <summary>
    /// Filters the asynchronous stream, yielding only the unwrapped values from successful results.
    /// </summary>
    [Pure]
    public static async IAsyncEnumerable<T> WhereSuccess<T>(
        this IAsyncEnumerable<Result<T>> source,
        [EnumeratorCancellation] CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(source);

        await foreach(Result<T> result in source.WithCancellation(cancellationToken).ConfigureAwait(false)) {
            if(result.IsSuccess)
                yield return result.Value;
        }
    }

    /// <summary>
    /// Filters the asynchronous stream, yielding only the errors from failed results.
    /// </summary>
    [Pure]
    public static async IAsyncEnumerable<Error> WhereFailure<T>(
        this IAsyncEnumerable<Result<T>> source,
        [EnumeratorCancellation] CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(source);

        await foreach(Result<T> result in source.WithCancellation(cancellationToken).ConfigureAwait(false)) {
            if(result.IsFailure) {
                foreach(Error error in result.Errors)
                    yield return error;
            }
        }
    }

    // ── CombineAsync ──────────────────────────────────────────────────────────

    /// <summary>
    /// Consumes the entire asynchronous stream and returns a combined result containing all values on success,
    /// or all aggregated errors if any result failed.
    /// </summary>
    [Pure]
    public static async Task<Result<IReadOnlyList<T>>> CombineAsync<T>(
        this IAsyncEnumerable<Result<T>> source,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(source);

        List<T>? values = null;
        List<Error>? errors = null;

        await foreach(Result<T> result in source.WithCancellation(cancellationToken).ConfigureAwait(false)) {
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

    // ── PartitionAsync ────────────────────────────────────────────────────────

    /// <summary>
    /// Consumes the entire asynchronous stream in a single pass and partitions the results into successes and failures.
    /// </summary>
    [Pure]
    public static async Task<(IReadOnlyList<T> Successes, IReadOnlyList<Error> Failures)> PartitionAsync<T>(
        this IAsyncEnumerable<Result<T>> source,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(source);

        List<T> successes = [];
        List<Error> failures = [];

        await foreach(Result<T> result in source.WithCancellation(cancellationToken).ConfigureAwait(false)) {
            if(result.IsSuccess) {
                successes.Add(result.Value);
            }
            else {
                failures.AddRange(result.Errors);
            }
        }

        return (successes, failures);
    }

    // ── MapAsync / ThenAsync ──────────────────────────────────────────────────

    /// <summary>
    /// Applies a synchronous transformation to each successful result in the asynchronous stream.
    /// </summary>
    [Pure]
    public static async IAsyncEnumerable<Result<TNew>> MapAsync<T, TNew>(
        this IAsyncEnumerable<Result<T>> source,
        Func<T, TNew> mapper,
        [EnumeratorCancellation] CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(mapper);

        await foreach(Result<T> result in source.WithCancellation(cancellationToken).ConfigureAwait(false)) {
            yield return result.Map(mapper);
        }
    }

    /// <summary>
    /// Applies an asynchronous transformation to each successful result in the asynchronous stream.
    /// </summary>
    [Pure]
    public static async IAsyncEnumerable<Result<TNew>> MapAsync<T, TNew>(
        this IAsyncEnumerable<Result<T>> source,
        Func<T, Task<TNew>> mapper,
        [EnumeratorCancellation] CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(mapper);

        await foreach(Result<T> result in source.WithCancellation(cancellationToken).ConfigureAwait(false)) {
            if(result.IsFailure) {
                yield return result.ToFailure<TNew>();
            }
            else {
                TNew mapped = await mapper(result.Value).ConfigureAwait(false);
                yield return Result.Success(mapped);
            }
        }
    }

    /// <summary>
    /// Chains an asynchronous operation to each successful result in the asynchronous stream.
    /// </summary>
    [Pure]
    public static async IAsyncEnumerable<Result<TNext>> ThenAsync<T, TNext>(
        this IAsyncEnumerable<Result<T>> source,
        Func<T, Task<Result<TNext>>> next,
        [EnumeratorCancellation] CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(next);

        await foreach(Result<T> result in source.WithCancellation(cancellationToken).ConfigureAwait(false)) {
            if(result.IsFailure) {
                yield return result.ToFailure<TNext>();
            }
            else {
                yield return await next(result.Value).ConfigureAwait(false);
            }
        }
    }
}