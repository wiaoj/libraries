namespace Wiaoj.Preconditions;
/// <summary>
/// Validation messages for buffer types (Span, Memory, etc.).
/// </summary>
internal static partial class PrecaMessages {
    internal static class Buffer {
        // Generic buffer messages (no null checks - these types are stack-allocated or value types)
        public const string SpanCannotBeEmpty = "Span cannot be empty.";
        public const string ReadOnlySpanCannotBeEmpty = "ReadOnlySpan cannot be empty.";
        public const string MemoryCannotBeEmpty = "Memory cannot be empty.";
        public const string ReadOnlyMemoryCannotBeEmpty = "ReadOnlyMemory cannot be empty.";
        public const string ArraySegmentCannotBeEmpty = "ArraySegment cannot be empty.";

        public const string ReadOnlySpanCannotBeEmptyOrWhiteSpace = "ReadOnlySpan<char> cannot be empty or consist only of whitespace characters.";
    }
}