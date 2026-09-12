namespace Wiaoj.Querying;

/// <summary>
/// Thrown when a query is applied strictly and does not satisfy the schema it is applied against.
/// </summary>
/// <remarks>
/// Lives beside <see cref="QueryValidationResult"/> rather than in the engine so a caller on the other side
/// of a service boundary — which references only the abstractions — can catch it and read the errors.
/// </remarks>
public sealed class QueryValidationException : Exception {

    /// <summary>Initializes a new instance from a failed validation.</summary>
    /// <param name="result">The validation result carrying the errors.</param>
    public QueryValidationException(QueryValidationResult result)
        : base(BuildMessage(result)) {
        this.Result = result;
    }

    /// <summary>Gets the validation result that caused the exception.</summary>
    public QueryValidationResult Result { get; }

    /// <summary>Gets the individual validation errors.</summary>
    public IReadOnlyList<QueryValidationError> Errors => this.Result.Errors;

    private static string BuildMessage(QueryValidationResult result) {
        return result.Errors.Count switch {
            0 => "The query is not valid for the schema.",
            1 => $"The query is not valid for the schema: {result.Errors[0].Message}",
            _ => $"The query is not valid for the schema ({result.Errors.Count} errors): " +
                 string.Join(" ", result.Errors.Select(error => error.Message))
        };
    }
}
