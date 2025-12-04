using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text;

namespace Wiaoj.Primitives;
/// <summary>
/// Provides static factory methods for creating and operating on <see cref="Secret{T}"/> instances.
/// Using these methods allows for type inference, enabling cleaner syntax (e.g., `Secret.From(bytes)`).
/// </summary>
public static class Secret {
    /// <inheritdoc cref="Secret{T}.From(ReadOnlySpan{T})"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Secret<T> From<T>(ReadOnlySpan<T> source) where T : unmanaged {
        return Secret<T>.From(source);
    }

    /// <inheritdoc cref="Secret{T}.From(Stream)"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Secret<byte> From(Stream stream) {
        return Secret<byte>.From(stream);
    }

    /// <inheritdoc cref="Secret{T}.From(Base64String)"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Secret<byte> From(Base64String base64) {
        return Secret<byte>.From(base64);
    }

    /// <inheritdoc cref="Secret{T}.From(HexString)"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Secret<byte> From(HexString hex) {
        return Secret<byte>.From(hex);
    }

    /// <inheritdoc cref="Secret{T}.From(string , Encoding)"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Secret<byte> From(string s, Encoding encoding) { 
        return Secret<byte>.From(encoding.GetBytes(s));
    }

    /// <inheritdoc cref="Secret{T}.From(string)"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Secret<byte> From(string s) {
        return Secret<byte>.From(s, Encoding.UTF8);
    }

    /// <inheritdoc cref="Secret{T}.Parse(string)"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Secret<char> Parse(string s) {
        return Secret<char>.Parse(s);
    }

    /// <inheritdoc cref="Secret{T}.Parse(ReadOnlySpan{char})"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Secret<char> Parse(ReadOnlySpan<char> s) {
        return Secret<char>.Parse(s);
    }

    /// <inheritdoc cref="Secret{T}.TryParse(ReadOnlySpan{char}, out Secret{T})"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryParse(ReadOnlySpan<char> s, [MaybeNullWhen(false)] out Secret<char> result) {
        return Secret<char>.TryParse(s, out result);
    }

    /// <inheritdoc cref="Secret{T}.Generate(int)"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Secret<byte> Generate(int byteCount) {
        return Secret<byte>.Generate(byteCount);
    }

    /// <inheritdoc cref="Secret{T}.Select(in Secret{T}, in Secret{T}, int)"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Secret<T> Select<T>(in Secret<T> ifOne, in Secret<T> ifZero, int condition) where T : unmanaged {
        return Secret<T>.Select(in ifOne, in ifZero, condition);
    }
}