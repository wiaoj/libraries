using System.Buffers;
using System.Buffers.Binary;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json.Serialization;
using Wiaoj.Primitives.Buffers;
using Wiaoj.Primitives.Hashing.Internal;
using Wiaoj.Primitives.JsonConverters;

namespace Wiaoj.Primitives.Hashing;

/// <summary>
/// Represents an immutable, fixed-size 8-byte (64-bit) XXHash3 hash.
/// </summary>
/// <remarks>
/// <para>
/// <b>Non-Cryptographic:</b> XXHash3 is an extremely fast, state-of-the-art non-cryptographic hash algorithm designed for 
/// data deduplication, in-memory hash tables, cache keys, and checksums.
/// </para>
/// <para>
/// <b>Zero Heap Allocation:</b> The hash is stored internally as a single 64-bit unsigned integer (<see cref="ulong"/>), 
/// requiring no unsafe pointer buffers or heap allocations.
/// </para>
/// </remarks>
[DebuggerDisplay("{ToString(),nq}")]
[StructLayout(LayoutKind.Sequential)]
[JsonConverter(typeof(XxHash3JsonConverter))]
[SkipLocalsInit]
public readonly struct XxHash3
    : IEquatable<XxHash3>,
      IComparable<XxHash3>,
      IComparable,
      IParsable<XxHash3>,
      ISpanParsable<XxHash3>,
      IUtf8SpanParsable<XxHash3>,
      ISpanFormattable,
      IUtf8SpanFormattable,
      IFormattable,
      IEqualityOperators<XxHash3, XxHash3, bool>,
      IComparisonOperators<XxHash3, XxHash3, bool> {

    /// <summary>The size of the XXHash3-64 hash in bytes (8 bytes / 64 bits).</summary>
    internal const int HashSizeInBytes = 8;

    private readonly ulong _value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal XxHash3(ulong value) {
        this._value = value;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="XxHash3"/> struct from an 8-byte span.
    /// </summary>
    /// <param name="source">A span containing exactly 8 bytes of hash data.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="source"/> is not exactly 8 bytes long.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal XxHash3(ReadOnlySpan<byte> source) {
        if(source.Length != HashSizeInBytes) {
            throw new ArgumentException($"Source span must be exactly {HashSizeInBytes} bytes long.", nameof(source));
        }
        this._value = MemoryMarshal.Read<ulong>(source);
    }

    #region Factory Methods

    /// <summary>
    /// Represents an empty (zero-filled) 8-byte <see cref="XxHash3"/> hash.
    /// </summary>
    public static readonly XxHash3 Empty = default;

    /// <summary>
    /// Gets the raw 64-bit unsigned integer hash value.
    /// </summary>
    public ulong Value {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => this._value;
    }

    /// <summary>
    /// Creates an <see cref="XxHash3"/> instance from an 8-byte read-only span.
    /// </summary>
    /// <param name="source">A span containing exactly 8 bytes of hash data.</param>
    /// <returns>A valid <see cref="XxHash3"/> instance.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="source"/> is not exactly 8 bytes long.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static XxHash3 FromBytes(ReadOnlySpan<byte> source) {
        return new(source);
    }

    /// <summary>
    /// Creates an <see cref="XxHash3"/> instance from a hexadecimal string representation.
    /// </summary>
    /// <param name="hex">The hex-encoded string representing the 8-byte hash (16 hex characters).</param>
    /// <returns>A new <see cref="XxHash3"/> instance.</returns>
    /// <exception cref="FormatException">Thrown when <paramref name="hex"/> does not decode to exactly 8 bytes.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static XxHash3 From(HexString hex) {
        if(hex.GetDecodedLength() != HashSizeInBytes) {
            throw new FormatException($"Source HexString must represent exactly {HashSizeInBytes} bytes (16 hex characters).");
        }
        Span<byte> buffer = stackalloc byte[HashSizeInBytes];
        hex.TryDecode(buffer, out _);
        return new(buffer);
    }

    /// <summary>
    /// Creates an <see cref="XxHash3"/> instance from a Base64-encoded string.
    /// </summary>
    /// <param name="base64">The Base64-encoded string representing the 8-byte hash.</param>
    /// <returns>A new <see cref="XxHash3"/> instance.</returns>
    /// <exception cref="FormatException">Thrown when <paramref name="base64"/> does not decode to exactly 8 bytes.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static XxHash3 From(Base64String base64) {
        if(base64.GetDecodedLength() != HashSizeInBytes) {
            throw new FormatException($"Source Base64String must represent exactly {HashSizeInBytes} bytes.");
        }
        Span<byte> buffer = stackalloc byte[HashSizeInBytes];
        if(!base64.TryDecode(buffer, out int written) || written != HashSizeInBytes) {
            throw new FormatException("Failed to decode Base64 into Hash.");
        }
        return new(buffer);
    }

    /// <summary>
    /// Creates an <see cref="XxHash3"/> instance from a Base32-encoded string.
    /// </summary>
    /// <param name="base32">The Base32-encoded string representing the 8-byte hash.</param>
    /// <returns>A new <see cref="XxHash3"/> instance.</returns>
    /// <exception cref="FormatException">Thrown when <paramref name="base32"/> does not decode to exactly 8 bytes.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static XxHash3 From(Base32String base32) {
        Span<byte> buffer = stackalloc byte[HashSizeInBytes];
        if(base32.TryDecode(buffer, out int written) && written == HashSizeInBytes) {
            return new(buffer);
        }
        throw new FormatException($"Base32 string length mismatch for {HashSizeInBytes}-byte hash.");
    }

    /// <summary>
    /// Creates an <see cref="XxHash3"/> instance from a Base62-encoded string.
    /// </summary>
    /// <param name="base62">The Base62-encoded string representing the 8-byte hash.</param>
    /// <returns>A new <see cref="XxHash3"/> instance.</returns>
    /// <exception cref="FormatException">Thrown when <paramref name="base62"/> represents a value exceeding 8 bytes.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static XxHash3 From(Base62String base62) {
        byte[] bytes = base62.ToBytes();
        if(bytes.Length > HashSizeInBytes) {
            for(int i = 0; i < bytes.Length - HashSizeInBytes; i++) {
                if(bytes[i] != 0) {
                    throw new FormatException("Base62 string represents a value too large for this hash.");
                }
            }
            return new(bytes.AsSpan(bytes.Length - HashSizeInBytes));
        }
        Span<byte> buffer = stackalloc byte[HashSizeInBytes];
        buffer.Clear();
        bytes.CopyTo(buffer[(HashSizeInBytes - bytes.Length)..]);
        return new(buffer);
    }

    /// <summary>
    /// Parses a 16-character hexadecimal string into an <see cref="XxHash3"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static XxHash3 Parse(string s) {
        Preca.ThrowIfNull(s);
        if(!TryParse(s.AsSpan(), out XxHash3 result)) {
            throw new FormatException($"Input string must represent exactly {HashSizeInBytes} bytes (16 hex characters).");
        }
        return result;
    }

    /// <summary>
    /// Parses a 16-character hexadecimal span into an <see cref="XxHash3"/> without heap allocations.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static XxHash3 Parse(ReadOnlySpan<char> s) {
        if(!TryParse(s, out XxHash3 result)) {
            throw new FormatException($"Input span must represent exactly {HashSizeInBytes} bytes (16 hex characters).");
        }
        return result;
    }

    /// <summary>
    /// Parses a UTF-8 encoded hexadecimal byte span into an <see cref="XxHash3"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static XxHash3 Parse(ReadOnlySpan<byte> utf8Text) {
        if(!TryParse(utf8Text, out XxHash3 result)) {
            throw new FormatException("Invalid UTF-8 hexadecimal sequence for XxHash3.");
        }
        return result;
    }

    /// <summary>
    /// Attempts to parse a hexadecimal string into an <see cref="XxHash3"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryParse([NotNullWhen(true)] string? s, out XxHash3 result) {
        if(s is null) { result = default; return false; }
        return TryParse(s.AsSpan(), out result);
    }

    /// <summary>
    /// Attempts to parse a hexadecimal span into an <see cref="XxHash3"/> without heap allocations.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryParse(ReadOnlySpan<char> s, out XxHash3 result) {
        if(HexString.TryParse(s, out HexString hex)) return TryParse(hex, out result);
        result = default;
        return false;
    }

    /// <summary>
    /// Attempts to parse a UTF-8 encoded byte span into an <see cref="XxHash3"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryParse(ReadOnlySpan<byte> utf8Text, out XxHash3 result) {
        if(utf8Text.Length == HashSizeInBytes * 2) {
            Span<char> chars = stackalloc char[HashSizeInBytes * 2];
            if(Encoding.UTF8.GetChars(utf8Text, chars) == HashSizeInBytes * 2) {
                return TryParse(chars, out result);
            }
        }
        result = default;
        return false;
    }

    /// <summary>
    /// Attempts to parse a <see cref="HexString"/> into an <see cref="XxHash3"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryParse(HexString hex, out XxHash3 result) {
        if(hex.GetDecodedLength() != HashSizeInBytes) { result = default; return false; }
        Span<byte> buffer = stackalloc byte[HashSizeInBytes];
        hex.TryDecode(buffer, out _);
        result = new(buffer);
        return true;
    }

    #endregion

    #region Explicit Interface Implementations (IParsable, ISpanParsable, IUtf8SpanParsable)

    static XxHash3 IParsable<XxHash3>.Parse(string s, IFormatProvider? provider) => Parse(s);
    static bool IParsable<XxHash3>.TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out XxHash3 result) => TryParse(s, out result);
    static XxHash3 ISpanParsable<XxHash3>.Parse(ReadOnlySpan<char> s, IFormatProvider? provider) => Parse(s);
    static bool ISpanParsable<XxHash3>.TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out XxHash3 result) => TryParse(s, out result);
    static XxHash3 IUtf8SpanParsable<XxHash3>.Parse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider) => Parse(utf8Text);
    static bool IUtf8SpanParsable<XxHash3>.TryParse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider, out XxHash3 result) => TryParse(utf8Text, out result);

    #endregion

    #region High-Performance Computation

    /// <summary>
    /// Computes the XXHash3-64 hash for the contents of a secure <see cref="Secret{Byte}"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static XxHash3 Compute(Secret<byte> secret) {
        Preca.ThrowIfNull(secret);
        return secret.Expose(span => Compute(span));
    }

    /// <summary>
    /// Computes the XXHash3-64 hash of a byte span using SIMD hardware acceleration without heap allocations.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [SkipLocalsInit]
    public static XxHash3 Compute(ReadOnlySpan<byte> data) {
        return new(XxHash3Core.HashToUInt64(data));
    }

    /// <summary>
    /// Computes the XXHash3-64 hash for the contents of a secure <see cref="Secret{Char}"/> using the specified encoding.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static XxHash3 Compute(Secret<char> secret, Encoding encoding) {
        Preca.ThrowIfNull(secret);
        Preca.ThrowIfNull(encoding);
        return secret.Expose(chars => {
            int maxByteCount = encoding.GetMaxByteCount(chars.Length);
            using ValueBuffer<byte> buffer = new(maxByteCount, stackalloc byte[1024]);
            int bytesWritten = encoding.GetBytes(chars, buffer.Span);
            return Compute(buffer.Span[..bytesWritten]);
        });
    }

    /// <summary>
    /// Computes the XXHash3-64 hash for the contents of a secure <see cref="Secret{Char}"/> using UTF-8 encoding.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static XxHash3 Compute(Secret<char> secret) {
        return Compute(secret, Encoding.UTF8);
    }

    /// <summary>
    /// Computes the XXHash3-64 hash of a string using the specified encoding.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [SkipLocalsInit]
    public static XxHash3 Compute(string text, Encoding encoding) {
        Preca.ThrowIfNull(text);
        Preca.ThrowIfNull(encoding);
        int maxByteCount = encoding.GetMaxByteCount(text.Length);
        using ValueBuffer<byte> buffer = new(maxByteCount, stackalloc byte[1024]);
        int bytesWritten = encoding.GetBytes(text, buffer.Span);
        return Compute(buffer.Span[..bytesWritten]);
    }

    /// <summary>
    /// Computes the XXHash3-64 hash of a string using UTF-8 encoding.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static XxHash3 Compute(string text) {
        return Compute(text, Encoding.UTF8);
    }

    #endregion

    #region Data Access & Conversion

    /// <summary>
    /// Returns a <see cref="ReadOnlySpan{Byte}"/> view of the 8 hash bytes without heap allocations.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly ReadOnlySpan<byte> AsSpan() {
        return MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref Unsafe.AsRef(in this._value), 1));
    }

    /// <summary>
    /// Provides safe, scoped access to the hash bytes as a <see cref="ReadOnlySpan{Byte}"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly void Expose(Action<ReadOnlySpan<byte>> action) {
        action(AsSpan());
    }

    /// <summary>
    /// Provides safe, scoped access to the hash bytes and returns a result.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly TResult Expose<TResult>(Func<ReadOnlySpan<byte>, TResult> func) {
        return func(AsSpan());
    }

    /// <summary>
    /// Copies the hash bytes into a destination span.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CopyTo(Span<byte> destination) {
        if(destination.Length < HashSizeInBytes) {
            throw new ArgumentException($"Destination span must be at least {HashSizeInBytes} bytes long.", nameof(destination));
        }
        AsSpan().CopyTo(destination);
    }

    /// <summary>
    /// Attempts to copy the hash bytes into the specified destination span.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryCopyTo(Span<byte> destination) {
        if(destination.Length < HashSizeInBytes) return false;
        AsSpan().CopyTo(destination);
        return true;
    }

    /// <summary>
    /// Encodes the hash bytes into an uppercase <see cref="HexString"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public HexString ToHexString() {
        return HexString.FromBytes(AsSpan());
    }

    /// <summary>
    /// Encodes the hash bytes into a lowercase <see cref="HexString"/> without string allocations.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public HexString ToHexStringLower() {
        return HexString.FromBytesLower(AsSpan());
    }

    /// <summary>
    /// Encodes the hash bytes into a type-safe <see cref="Base64String"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Base64String ToBase64String() {
        return Base64String.FromBytes(AsSpan());
    }

    /// <summary>
    /// Encodes the hash bytes into a type-safe, URL-safe <see cref="Base64UrlString"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Base64UrlString ToBase64UrlString() {
        return Base64UrlString.FromBytes(AsSpan());
    }

    /// <summary>
    /// Encodes the hash bytes into a type-safe <see cref="Base32String"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Base32String ToBase32String() {
        return Base32String.FromBytes(AsSpan());
    }

    /// <summary>
    /// Encodes the hash bytes into a type-safe <see cref="Base62String"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Base62String ToBase62String() {
        return Base62String.FromBytes(AsSpan());
    }

    /// <summary>
    /// Returns the uppercase hexadecimal string representation of the hash.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override string ToString() => Convert.ToHexString(AsSpan());

    /// <summary>
    /// Returns the string representation of the hash using the specified format.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public string ToString(string? format) => ToString(format, null);

    /// <summary>
    /// Returns the string representation of the hash using the specified format and provider.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public string ToString(string? format, IFormatProvider? formatProvider) {
        return format is "x" ? Convert.ToHexStringLower(AsSpan()) : Convert.ToHexString(AsSpan());
    }

    /// <summary>
    /// Attempts to format the hash as an uppercase hexadecimal string into the destination character span.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryFormat(Span<char> destination, out int charsWritten) => TryFormat(destination, out charsWritten, default, null);

    /// <summary>
    /// Attempts to format the hash into the destination character span using the specified format.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format) => TryFormat(destination, out charsWritten, format, null);

    /// <summary>
    /// Attempts to format the hash into the destination character span using the specified format and provider.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider) {
        int required = HashSizeInBytes * 2;
        if(destination.Length < required) { charsWritten = 0; return false; }
        return format.Equals("x", StringComparison.Ordinal)
            ? Convert.TryToHexStringLower(AsSpan(), destination, out charsWritten)
            : Convert.TryToHexString(AsSpan(), destination, out charsWritten);
    }

    /// <summary>
    /// Attempts to format the hash as an uppercase UTF-8 hexadecimal byte span.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryFormat(Span<byte> utf8Destination, out int bytesWritten) => TryFormat(utf8Destination, out bytesWritten, default, null);

    /// <summary>
    /// Attempts to format the hash into the destination UTF-8 byte span using the specified format.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryFormat(Span<byte> utf8Destination, out int bytesWritten, ReadOnlySpan<char> format) => TryFormat(utf8Destination, out bytesWritten, format, null);

    /// <summary>
    /// Attempts to format the hash into the destination UTF-8 byte span using the specified format and provider.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryFormat(Span<byte> utf8Destination, out int bytesWritten, ReadOnlySpan<char> format, IFormatProvider? provider) {
        int required = HashSizeInBytes * 2;
        if(utf8Destination.Length < required) { bytesWritten = 0; return false; }
        Span<char> charBuf = stackalloc char[required];
        bool ok = format.Equals("x", StringComparison.Ordinal)
            ? Convert.TryToHexStringLower(AsSpan(), charBuf, out _)
            : Convert.TryToHexString(AsSpan(), charBuf, out _);
        if(!ok) { bytesWritten = 0; return false; }
        bytesWritten = Encoding.UTF8.GetBytes(charBuf, utf8Destination);
        return true;
    }

    #endregion

    #region Equality & Comparison

    /// <summary>
    /// Compares two <see cref="XxHash3"/> hashes for equality using a fast 64-bit integer comparison.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool Equals(XxHash3 other) {
        return this._value == other._value;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override readonly bool Equals(object? obj) {
        return obj is XxHash3 other && Equals(other);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override readonly int GetHashCode() {
        return (int)(this._value ^ (this._value >> 32));
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CompareTo(XxHash3 other) => this._value.CompareTo(other._value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CompareTo(object? obj) {
        if(obj is null) return 1;
        if(obj is XxHash3 other) return CompareTo(other);
        throw new ArgumentException($"Object must be of type {nameof(XxHash3)}.", nameof(obj));
    }

    /// <inheritdoc cref="IComparisonOperators{TSelf, TOther, TResult}.op_GreaterThan(TSelf, TOther)" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >(XxHash3 left, XxHash3 right) => left.CompareTo(right) > 0;

    /// <inheritdoc cref="IComparisonOperators{TSelf, TOther, TResult}.op_LessThan(TSelf, TOther)" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <(XxHash3 left, XxHash3 right) => left.CompareTo(right) < 0;

    /// <inheritdoc cref="IComparisonOperators{TSelf, TOther, TResult}.op_GreaterThanOrEqual(TSelf, TOther)" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >=(XxHash3 left, XxHash3 right) => left.CompareTo(right) >= 0;

    /// <inheritdoc cref="IComparisonOperators{TSelf, TOther, TResult}.op_LessThanOrEqual(TSelf, TOther)" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <=(XxHash3 left, XxHash3 right) => left.CompareTo(right) <= 0;

    /// <inheritdoc cref="IEqualityOperators{TSelf,TOther,TResult}.op_Equality"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(XxHash3 left, XxHash3 right) {
        return left.Equals(right);
    }

    /// <inheritdoc cref="IEqualityOperators{TSelf,TOther,TResult}.op_Inequality"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(XxHash3 left, XxHash3 right) {
        return !left.Equals(right);
    }

    #endregion

    #region Alternate Comparers (.NET 10 Alternate Lookup)

    /// <summary>
    /// Gets an equality comparer that performs ordinal comparisons on <see cref="XxHash3"/>
    /// and supports zero-allocation alternate lookups using <see cref="ReadOnlySpan{Char}"/>.
    /// </summary>
    public static IEqualityComparer<XxHash3> OrdinalComparer => XxHash3OrdinalComparer.Instance;

    /// <summary>
    /// Gets an equality comparer that performs case-insensitive ordinal comparisons on <see cref="XxHash3"/>
    /// and supports zero-allocation alternate lookups using <see cref="ReadOnlySpan{Char}"/>.
    /// </summary>
    public static IEqualityComparer<XxHash3> OrdinalIgnoreCaseComparer => XxHash3OrdinalIgnoreCaseComparer.Instance;

    private sealed class XxHash3OrdinalComparer : IEqualityComparer<XxHash3>, IAlternateEqualityComparer<ReadOnlySpan<char>, XxHash3> {
        public static XxHash3OrdinalComparer Instance { get; } = new();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(XxHash3 x, XxHash3 y) => x.Equals(y);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetHashCode(XxHash3 obj) => obj.GetHashCode();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(ReadOnlySpan<char> alternate, XxHash3 other) {
            if(XxHash3.TryParse(alternate, out XxHash3 parsed)) {
                return parsed.Equals(other);
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetHashCode(ReadOnlySpan<char> alternate) {
            if(XxHash3.TryParse(alternate, out XxHash3 parsed)) {
                return parsed.GetHashCode();
            }
            return 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public XxHash3 Create(ReadOnlySpan<char> alternate) => XxHash3.Parse(alternate);
    }

    private sealed class XxHash3OrdinalIgnoreCaseComparer : IEqualityComparer<XxHash3>, IAlternateEqualityComparer<ReadOnlySpan<char>, XxHash3> {
        public static XxHash3OrdinalIgnoreCaseComparer Instance { get; } = new();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(XxHash3 x, XxHash3 y) => x.Equals(y);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetHashCode(XxHash3 obj) => obj.GetHashCode();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(ReadOnlySpan<char> alternate, XxHash3 other) {
            if(XxHash3.TryParse(alternate, out XxHash3 parsed)) {
                return parsed.Equals(other);
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetHashCode(ReadOnlySpan<char> alternate) {
            if(XxHash3.TryParse(alternate, out XxHash3 parsed)) {
                return parsed.GetHashCode();
            }
            return 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public XxHash3 Create(ReadOnlySpan<char> alternate) => XxHash3.Parse(alternate);
    }

    #endregion
}

/// <summary>
/// Extension methods for <see cref="XxHash3"/>.
/// </summary>
public static partial class XxHash3Extensions {
    extension(XxHash3) {
        /// <summary>
        /// Asynchronously computes the <see cref="XxHash3"/> hash of a stream using SIMD hardware streaming.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValueTask<XxHash3> ComputeAsync(Stream stream) => ComputeAsync(stream, CancellationToken.None);

        /// <summary>
        /// Asynchronously computes the <see cref="XxHash3"/> hash of a stream using SIMD hardware streaming.
        /// </summary>
        public static async ValueTask<XxHash3> ComputeAsync(
            Stream stream, CancellationToken cancellationToken) {
            Preca.ThrowIfNull(stream);
            if(stream.CanSeek) stream.Position = 0;

            XxHash3Core hasher = new();
            byte[] rented = ArrayPool<byte>.Shared.Rent(81_920); // 80 KB chunks

            try {
                int bytesRead;
                while((bytesRead = await stream.ReadAsync(rented, cancellationToken)
                    .ConfigureAwait(false)) > 0) {
                    hasher.Append(rented.AsSpan(0, bytesRead));
                }

                return new(hasher.GetCurrentHashAsUInt64());
            }
            finally {
                ArrayPool<byte>.Shared.Return(rented);
                if(stream.CanSeek) stream.Position = 0;
            }
        }
    }
}