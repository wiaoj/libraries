namespace Wiaoj.Results;

public readonly partial record struct Error {
    /// <summary>
    /// Contains factory methods for creating NotFound errors.
    /// </summary>
    public static class NotFound {
        /// <summary>
        /// Creates a new 'NotFound' error for a specific resource.
        /// </summary>
        /// <param name="resourceName">The name of the resource (e.g., "User").</param>
        /// <param name="key">The key or identifier used to find the resource.</param>
        public static Error For(string resourceName, object key) =>
            new(
                code: $"{resourceName}.NotFound",
                description: $"The {resourceName} with key '{key}' was not found.",
                type: ErrorType.NotFound);

        /// <summary>
        /// Creates a new 'NotFound' error with a custom code and description.
        /// </summary>
        public static Error With(string code, string description) =>
            new(code, description, ErrorType.NotFound);
    }
}