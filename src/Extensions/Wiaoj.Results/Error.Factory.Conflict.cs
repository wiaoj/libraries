namespace Wiaoj.Results;

public readonly partial record struct Error {
    /// <summary>
    /// Contains factory methods for creating Conflict errors.
    /// </summary>
    public static class Conflict {
        /// <summary>
        /// Creates a new 'Conflict' error indicating a duplicate resource.
        /// </summary>
        /// <param name="resourceName">The name of the resource (e.g., "User").</param>
        /// <param name="fieldName">The name of the conflicting field (e.g., "Email").</param>
        /// <param name="fieldValue">The value of the conflicting field.</param>
        public static Error Duplicate(string resourceName, string fieldName, object fieldValue) =>
            new(
                code: $"{resourceName}.Conflict.Duplicate{fieldName}",
                description: $"A {resourceName} with the {fieldName} '{fieldValue}' already exists.",
                type: ErrorType.Conflict);

        /// <summary>
        /// Creates a new 'Conflict' error with a custom code and description.
        /// </summary>
        public static Error With(string code, string description,IReadOnlyDictionary<string, object>? metadata = null) =>
            new(code, description, ErrorType.Conflict, metadata);
    }
}