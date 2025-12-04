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
    }
}