using System.Buffers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Unicode;

namespace Wiaoj.Extensions;

/// <summary>
/// High-performance extension methods for UTF-8 encoding and byte conversions.
/// </summary>
public static class StringUtf8Extensions {
    /// <summary>
    /// Encodes the entire string into a new UTF-8 byte array.
    /// </summary>
    /// <param name="value">The source string to encode.</param>
    /// <returns>A UTF-8 encoded byte array.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte[] ToUtf8Bytes(this string value) {
        Preca.ThrowIfNull(value);
        return Encoding.UTF8.GetBytes(value);
    }

    /// <summary>
    /// Encodes a character span into a new UTF-8 byte array.
    /// </summary>
    /// <param name="chars">The source character span.</param>
    /// <returns>A UTF-8 encoded byte array.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte[] ToUtf8Bytes(this ReadOnlySpan<char> chars) {
        if(chars.IsEmpty) {
            return [];
        }

        byte[] bytes = new byte[Encoding.UTF8.GetByteCount(chars)];
        Encoding.UTF8.GetBytes(chars, bytes);
        return bytes;
    }

    /// <summary>
    /// Calculates the exact byte count required to encode the character span as UTF-8.
    /// </summary>
    /// <param name="chars">The source character span.</param>
    /// <returns>The number of bytes required.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetUtf8ByteCount(this ReadOnlySpan<char> chars) {
        return Encoding.UTF8.GetByteCount(chars);
    }

    /// <summary>
    /// Encodes the character span directly into the destination UTF-8 byte span without heap allocations.
    /// </summary>
    /// <param name="chars">The source character span.</param>
    /// <param name="destination">The destination byte buffer.</param>
    /// <param name="bytesWritten">When this method returns, contains the number of bytes written.</param>
    /// <returns><see langword="true"/> if the destination span was large enough; otherwise, <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryWriteUtf8Bytes(this ReadOnlySpan<char> chars, Span<byte> destination, out int bytesWritten) {
        return Utf8.FromUtf16(chars, destination, out _, out bytesWritten) == OperationStatus.Done;
    }
}