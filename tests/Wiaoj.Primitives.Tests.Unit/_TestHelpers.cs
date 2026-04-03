using System.Globalization;
using System.Text;

namespace Wiaoj.Primitives.Tests.Unit;

/// <summary>
/// Provides helper methods and mock implementations for testing UnixTimestamp.
/// </summary>
internal static class TestHelpers {

    // --- Parsing TestHelpers ---
    public static T ParseHelper<T>(string s) where T : IParsable<T>
        => T.Parse(s, CultureInfo.InvariantCulture);

    public static T ParseSpanHelper<T>(string s) where T : ISpanParsable<T>
        => T.Parse(s.AsSpan(), CultureInfo.InvariantCulture);

    public static T ParseUtf8Helper<T>(string s) where T : IUtf8SpanParsable<T>
        => T.Parse(Encoding.UTF8.GetBytes(s), CultureInfo.InvariantCulture);

    public static bool TryParseUtf8Helper<T>(byte[] b, out T res) where T : IUtf8SpanParsable<T>
        => T.TryParse(b, CultureInfo.InvariantCulture, out res!);

    // --- Fake Time Provider ---
    /// <summary>
    /// A simple TimeProvider implementation to mock time in unit tests without external dependencies (like Moq).
    /// </summary>
    public sealed class FakeTimeProvider : TimeProvider {
        public DateTimeOffset CurrentUtc { get; set; }

        public FakeTimeProvider(DateTimeOffset initialTime) {
            CurrentUtc = initialTime;
        }

        public override DateTimeOffset GetUtcNow() => CurrentUtc;
    }
}