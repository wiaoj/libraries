namespace Wiaoj.Results; 
public static partial class Result {

    // ── All — collect all errors, stop only when every result is checked ──────

    /// <summary>
    /// Evaluates all <paramref name="results"/> and returns a successful
    /// <see cref="Result{TValue}"/> of <see cref="Success"/> only when every
    /// result is successful. If any result fails, all errors from all failing
    /// results are collected and returned together.
    /// </summary>
    /// <param name="results">The results to evaluate.</param>
    /// <returns>
    /// <see cref="Success"/> when all results succeed;
    /// a failed <see cref="Result{TValue}"/> containing every error from every
    /// failing result otherwise.
    /// </returns>
    /// <example>
    /// <code>
    /// Result&lt;Success&gt; combined = Result.All(nameResult, emailResult, ageResult);
    /// // If nameResult and ageResult both fail, combined contains both sets of errors.
    /// </code>
    /// </example>
    public static Result<Success> All(params Result<Success>[] results) {
        List<Error>? errors = null;

        foreach(Result<Success> result in results) {
            if(result.IsFailure) {
                errors ??= [];
                errors.AddRange(result.Errors);
            }
        }

        return errors is null ? Wiaoj.Results.Success.Default : errors;
    }

    /// <inheritdoc cref="All(Result{Success}[])"/>
    public static Result<Success> All(IEnumerable<Result<Success>> results) {
        List<Error>? errors = null;

        foreach(Result<Success> result in results) {
            if(result.IsFailure) {
                errors ??= [];
                errors.AddRange(result.Errors);
            }
        }

        return errors is null ? Wiaoj.Results.Success.Default : errors;
    }

    // ── Combine — zip two results into a tuple ────────────────────────────────

    /// <summary>
    /// Combines two results into a single <see cref="Result{TValue}"/> containing
    /// a tuple of both values. All errors from both failing results are collected.
    /// </summary>
    /// <typeparam name="T1">Value type of the first result.</typeparam>
    /// <typeparam name="T2">Value type of the second result.</typeparam>
    /// <param name="r1">The first result.</param>
    /// <param name="r2">The second result.</param>
    /// <returns>
    /// A successful <see cref="Result{TValue}"/> of <c>(T1, T2)</c> when both succeed;
    /// a failed result containing all errors otherwise.
    /// </returns>
    public static Result<(T1, T2)> Combine<T1, T2>(
        Result<T1> r1, Result<T2> r2) {

        if(r1.IsSuccess && r2.IsSuccess)
            return (r1.Value, r2.Value);

        List<Error> errors = [];
        if(r1.IsFailure) errors.AddRange(r1.Errors);
        if(r2.IsFailure) errors.AddRange(r2.Errors);
        return errors;
    }

    /// <summary>
    /// Combines three results into a single <see cref="Result{TValue}"/> containing
    /// a tuple of all three values. All errors from any failing results are collected.
    /// </summary>
    public static Result<(T1, T2, T3)> Combine<T1, T2, T3>(
        Result<T1> r1, Result<T2> r2, Result<T3> r3) {

        if(r1.IsSuccess && r2.IsSuccess && r3.IsSuccess)
            return (r1.Value, r2.Value, r3.Value);

        List<Error> errors = [];
        if(r1.IsFailure) errors.AddRange(r1.Errors);
        if(r2.IsFailure) errors.AddRange(r2.Errors);
        if(r3.IsFailure) errors.AddRange(r3.Errors);
        return errors;
    }

    /// <summary>
    /// Combines four results into a single <see cref="Result{TValue}"/> containing
    /// a tuple of all four values.
    /// </summary>
    public static Result<(T1, T2, T3, T4)> Combine<T1, T2, T3, T4>(
        Result<T1> r1, Result<T2> r2, Result<T3> r3, Result<T4> r4) {

        if(r1.IsSuccess && r2.IsSuccess && r3.IsSuccess && r4.IsSuccess)
            return (r1.Value, r2.Value, r3.Value, r4.Value);

        List<Error> errors = [];
        if(r1.IsFailure) errors.AddRange(r1.Errors);
        if(r2.IsFailure) errors.AddRange(r2.Errors);
        if(r3.IsFailure) errors.AddRange(r3.Errors);
        if(r4.IsFailure) errors.AddRange(r4.Errors);
        return errors;
    }
}