using System.Buffers;

namespace Wiaoj.Primitives.Obfuscation;

/// <summary>
/// Represents a strategy for obfuscating and deobfuscating 128-bit identifiers.
/// </summary>
public interface IObfuscator {
    /// <summary>
    /// Encodes the given raw ID into the destination span as a string representation.
    /// </summary>
    bool TryEncode(Int128 value, Span<char> destination, out int charsWritten);

    /// <summary>
    /// Decodes the obfuscated string payload back into the raw 128-bit ID.
    /// </summary>
    bool TryDecode(ReadOnlySpan<char> payload, out Int128 result);

    /// <summary>
    /// Decodes the obfuscated UTF-8 payload back into the raw 128-bit ID.
    /// </summary>
    bool TryDecodeUtf8(ReadOnlySpan<byte> utf8Payload, out Int128 result);

    /// <summary>
    /// Encodes the ID directly into a buffer writer.
    /// </summary>
    void EncodeTo(Int128 value, IBufferWriter<char> writer);
}