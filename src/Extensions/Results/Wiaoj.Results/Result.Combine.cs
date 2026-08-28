using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;

namespace Wiaoj.Results;

public static partial class Result {

    // ── All ───────────────────────────────────────────────────────────────────

    /// <summary>
    /// Evaluates all results and returns a successful <see cref="Result{TValue}"/> of <see cref="Success"/>
    /// only when every result is successful. If any result fails, all errors are aggregated.
    /// </summary>
    /// <param name="results">The span of results to evaluate.</param>
    /// <returns>A successful <see cref="Success"/> result, or an aggregated failure.</returns>
    [Pure]
    public static Result<Success> All(params ReadOnlySpan<Result<Success>> results) {
        if(results.IsEmpty)
            return Wiaoj.Results.Success.Default;

        List<Error>? errors = null;
        Result<Success> singleFailure = default;
        int failureCount = 0;

        foreach(Result<Success> result in results) {
            if(result.IsFailure) {
                failureCount++;
                if(failureCount == 1) {
                    singleFailure = result;
                }
                else if(failureCount == 2) {
                    errors = [.. singleFailure.Errors, .. result.Errors];
                }
                else {
                    errors!.AddRange(result.Errors);
                }
            }
        }

        return failureCount switch {
            0 => Wiaoj.Results.Success.Default,
            1 => singleFailure,
            _ => errors!
        };
    }

    /// <inheritdoc cref="All(ReadOnlySpan{Result{Success}})"/>
    [Pure]
    public static Result<Success> All(IEnumerable<Result<Success>> results) {
        ArgumentNullException.ThrowIfNull(results);

        List<Error>? errors = null;
        Result<Success> singleFailure = default;
        int failureCount = 0;

        foreach(Result<Success> result in results) {
            if(result.IsFailure) {
                failureCount++;
                if(failureCount == 1) {
                    singleFailure = result;
                }
                else if(failureCount == 2) {
                    errors = [.. singleFailure.Errors, .. result.Errors];
                }
                else {
                    errors!.AddRange(result.Errors);
                }
            }
        }

        return failureCount switch {
            0 => Wiaoj.Results.Success.Default,
            1 => singleFailure,
            _ => errors!
        };
    }

    // ── Combine (Tuples) ──────────────────────────────────────────────────────

    /// <summary>
    /// Combines two results into a single result containing a 2-tuple.
    /// Allocates error collection only when multiple results fail simultaneously.
    /// </summary>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<(T1, T2)> Combine<T1, T2>(Result<T1> r1, Result<T2> r2) {

        if(r1.IsSuccess && r2.IsSuccess)
            return (r1.Value, r2.Value);

        if(r1.IsFailure && r2.IsSuccess)
            return r1.ToFailure<(T1, T2)>();

        if(r1.IsSuccess && r2.IsFailure)
            return r2.ToFailure<(T1, T2)>();

        List<Error> errors = [.. r1.Errors, .. r2.Errors];
        return errors;
    }

    /// <summary>
    /// Combines three results into a single result containing a 3-tuple.
    /// </summary>
    [Pure]
    public static Result<(T1, T2, T3)> Combine<T1, T2, T3>(
        Result<T1> r1, Result<T2> r2, Result<T3> r3) {

        if(r1.IsSuccess && r2.IsSuccess && r3.IsSuccess)
            return (r1.Value, r2.Value, r3.Value);

        if(r1.IsFailure && r2.IsSuccess && r3.IsSuccess) return r1.ToFailure<(T1, T2, T3)>();
        if(r1.IsSuccess && r2.IsFailure && r3.IsSuccess) return r2.ToFailure<(T1, T2, T3)>();
        if(r1.IsSuccess && r2.IsSuccess && r3.IsFailure) return r3.ToFailure<(T1, T2, T3)>();

        List<Error> errors = [];
        if(r1.IsFailure) errors.AddRange(r1.Errors);
        if(r2.IsFailure) errors.AddRange(r2.Errors);
        if(r3.IsFailure) errors.AddRange(r3.Errors);
        return errors;
    }

    /// <summary>
    /// Combines four results into a single result containing a 4-tuple.
    /// </summary>
    [Pure]
    public static Result<(T1, T2, T3, T4)> Combine<T1, T2, T3, T4>(
        Result<T1> r1, Result<T2> r2, Result<T3> r3, Result<T4> r4) {

        if(r1.IsSuccess && r2.IsSuccess && r3.IsSuccess && r4.IsSuccess)
            return (r1.Value, r2.Value, r3.Value, r4.Value);

        if(r1.IsFailure && r2.IsSuccess && r3.IsSuccess && r4.IsSuccess) return r1.ToFailure<(T1, T2, T3, T4)>();
        if(r1.IsSuccess && r2.IsFailure && r3.IsSuccess && r4.IsSuccess) return r2.ToFailure<(T1, T2, T3, T4)>();
        if(r1.IsSuccess && r2.IsSuccess && r3.IsFailure && r4.IsSuccess) return r3.ToFailure<(T1, T2, T3, T4)>();
        if(r1.IsSuccess && r2.IsSuccess && r3.IsSuccess && r4.IsFailure) return r4.ToFailure<(T1, T2, T3, T4)>();

        List<Error> errors = [];
        if(r1.IsFailure) errors.AddRange(r1.Errors);
        if(r2.IsFailure) errors.AddRange(r2.Errors);
        if(r3.IsFailure) errors.AddRange(r3.Errors);
        if(r4.IsFailure) errors.AddRange(r4.Errors);
        return errors;
    }
}