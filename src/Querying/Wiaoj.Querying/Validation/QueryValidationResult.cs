namespace Wiaoj.Querying;

/// <summary>
/// Represents the outcome of validating a <see cref="QueryRequest"/> against a <see cref="QuerySchema{T}"/>.
/// </summary>
public readonly record struct QueryValidationResult {
    /// <summary>
    /// Gets a successful validation result with no errors.
    /// </summary>
    public static readonly QueryValidationResult Success = default;

    /// <summary>
    /// Gets the list of validation errors, if any. Never <see langword="null"/>.
    /// </summary>
    public IReadOnlyList<QueryValidationError> Errors {
        get => field ?? [];
        init => field = value ?? [];
    }

    /// <summary>
    /// Gets a value indicating whether the query request is valid (contains no validation errors).
    /// </summary>
    public bool IsValid => this.Errors.Count == 0;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryValidationResult"/> struct with a list of errors.
    /// </summary>
    /// <param name="errors">The collection of validation errors.</param>
    public QueryValidationResult(IReadOnlyList<QueryValidationError>? errors) {
        this.Errors = errors ?? [];
    }

    /// <summary>
    /// Converts the validation errors into a dictionary grouped by property name, compatible with RFC 7807 problem details.
    /// </summary>
    /// <returns>A dictionary mapping property names to their corresponding error messages.</returns>
    public Dictionary<string, string[]> ToDictionary() {
        if(this.IsValid) {
            return [];
        }

        var dict = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        for(int i = 0; i < this.Errors.Count; i++) {
            var error = this.Errors[i];
            string key = string.IsNullOrWhiteSpace(error.PropertyName) ? "$" : error.PropertyName;

            if(!dict.TryGetValue(key, out var list)) {
                list = [];
                dict[key] = list;
            }

            list.Add(error.Message);
        }

        var result = new Dictionary<string, string[]>(dict.Count, StringComparer.OrdinalIgnoreCase);
        foreach(var (key, list) in dict) {
            result[key] = [.. list];
        }

        return result;
    }
}