namespace Wiaoj.Preconditions;

/// <summary>
/// Validation messages for text and string operations.
/// </summary>
internal static partial class PrecaMessages {
    internal static class Text {
        // String-specific messages (include null checks)
        public const string ValueCannotBeEmpty = "Value cannot be empty.";
        public const string ValueCannotBeWhiteSpace = "Value cannot be whitespace.";
        public const string ValueCannotBeNullOrEmpty = "Value cannot be null or empty.";
        public const string ValueCannotBeNullOrWhiteSpace = "Value cannot be null or whitespace.";

        /// <summary>
        /// Creates a dynamic string inequality validation message.
        /// </summary>
        /// <param name="actual">The actual string value received.</param>
        /// <param name="expected">The expected string value.</param>
        /// <returns>A formatted inequality validation message.</returns>
        public static string GetNotEqualMessage(string? actual, string expected) {
            return $"Value must be '{expected}', but was '{actual ?? "null"}'.";
        }
    }
}