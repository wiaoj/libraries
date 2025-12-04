namespace Wiaoj.Results;

public readonly partial record struct Error {
    /// <summary>
    /// Contains factory methods for creating Validation errors.
    /// </summary>
    public static class Validation {
        public static Error IsRequired(string fieldName) {
            return new($"Validation.{fieldName}.IsRequired", $"The '{fieldName}' field is required.", ErrorType.Validation);
        }

        public static Error NotEmpty(string fieldName) {
            return new($"Validation.{fieldName}.NotEmpty", $"The '{fieldName}' field cannot be empty.", ErrorType.Validation);
        }
         

        public static Error InvalidFormat(string fieldName, string expectedFormat) {
            return new($"Validation.{fieldName}.InvalidFormat", $"The '{fieldName}' field must be in the format '{expectedFormat}'.", ErrorType.Validation);
        }
    }
}