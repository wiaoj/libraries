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
/// Represents an immutable, fixed-size 16-byte (128-bit) XXHash3-128 hash.
/// </summary>
/// <remarks>
/// <para>
/// <b>Non-Cryptographic:</b> XXHash128 provides ultra-high collision resistance with a birthday bound 
/// of ~1.84 × 10¹⁹ elements, making it ideal for distributed deduplication, large file verification, 
/// cache keys, and GUID-sized identifiers at extreme speeds.
/// </para>
/// <para>
/// <b>Zero Heap Allocation:</b> The hash is stored internally as a single 128-bit unsigned integer (<see cref="UInt128"/>), 
/// requiring no heap allocations.
/// </para>
/// </remarks>
[DebuggerDisplay("{ToString(),nq}")]
[StructLayout(LayoutKind.Sequential)]
[JsonConverter(typeof(XxHash128JsonConverter))]
[SkipLocalsInit]
public readonly struct XxHash128
    : IEquatable<XxHash128>,
      IComparable<XxHash128>,
      IComparable,
      IParsable<XxHash128>,
      ISpanParsable<XxHash128>,
      IUtf8SpanParsable<XxHash128>,
      ISpanFormattable,
      IUtf8SpanFormattable,
      IFormattable,
      IEqualityOperators<XxHash128, XxHash128, bool>,
      IComparisonOperators<XxHash128, XxHash128, bool> {

    /// <summary>The size of the XXHash3-128 hash in bytes (16 bytes / 128 bits).</summary>
    public const int HashSizeInBytes = 16;

    private readonly UInt128 _value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal XxHash128(UInt128 value) {
        this._value = value;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="XxHash128"/> struct from a 16-byte span.
    /// </summary>
    /// <param name="source">A span containing exactly 16 bytes of hash data.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="source"/> is not exactly 16 bytes long.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal XxHash128(ReadOnlySpan<byte> source) {
        if(source.Length != HashSizeInBytes) {
            throw new ArgumentException($"Source span must be exactly {HashSizeInBytes} bytes long.", nameof(source));
        }
        this._value = MemoryMarshal.Read<UInt128>(source);
    }

    #region Factory Methods

    /// <summary>
    /// Represents an empty (zero-filled) 16-byte <see cref="XxHash128"/> hash.
    /// </summary>
    public static readonly XxHash128 Empty = default;

    /// <summary>
    /// Gets the raw 128-bit unsigned integer hash value.
    /// </summary>
    public UInt128 Value {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => this._value;
    }

    /// <summary>
    /// Creates an <see cref="XxHash128"/> instance from a 16-byte read-only span.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static XxHash128 FromBytes(ReadOnlySpan<byte> source) {
        return new(source);
    }

    /// <summary>
    /// Creates an <see cref="XxHash128"/> instance from a hexadecimal string representation.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static XxHash128 From(HexString hex) {
        if(hex.GetDecodedLength() != HashSizeInBytes) {
            throw new FormatException($"Source HexString must represent exactly {HashSizeInBytes} bytes (32 hex characters).");
        }
        Span<byte> buffer = stackalloc byte[HashSizeInBytes];
        hex.TryDecode(buffer, out _);
        return new(buffer);
    }

    /// <summary>
    /// Creates an <see cref="XxHash128"/> instance from a Base64-encoded string.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static XxHash128 From(Base64String base64) {
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
    /// Creates an <see cref="XxHash128"/> instance from a Base32-encoded string.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static XxHash128 From(Base32String base32) {
        Span<byte> buffer = stackalloc byte[HashSizeInBytes];
        if(base32.TryDecode(buffer, out int written) && written == HashSizeInBytes) {
            return new(buffer);
        }
        throw new FormatException($"Base32 string length mismatch for {HashSizeInBytes}-byte hash.");
    }

    /// <summary>
    /// Creates an <see cref="XxHash128"/> instance from a Base62-encoded string.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static XxHash128 From(Base62String base62) {
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
    /// Parses a 32-character hexadecimal string into an <see cref="XxHash128"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static XxHash128 Parse(string s) {
        Preca.ThrowIfNull(s);
        if(!TryParse(s.AsSpan(), out XxHash128 result)) {
            throw new FormatException($"Input string must represent exactly {HashSizeInBytes} bytes (32 hex characters).");
        }
        return result;
    }

    /// <summary>
    /// Parses a 32-character hexadecimal span into an <see cref="XxHash128"/> without heap allocations.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static XxHash128 Parse(ReadOnlySpan<char> s) {
        if(!TryParse(s, out XxHash128 result)) {
            throw new FormatException($"Input span must represent exactly {HashSizeInBytes} bytes (32 hex characters).");
        }
        return result;
    }

    /// <summary>
    /// Parses a UTF-8 encoded hexadecimal byte span into an <see cref="XxHash128"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static XxHash128 Parse(ReadOnlySpan<byte> utf8Text) {
        if(!TryParse(utf8Text, out XxHash128 result)) {
            throw new FormatException("Invalid UTF-8 hexadecimal sequence for XxHash128.");
        }
        return result;
    }

    /// <summary>
    /// Attempts to parse a hexadecimal string into an <see cref="XxHash128"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryParse([NotNullWhen(true)] string? s, out XxHash128 result) {
        if(s is null) { result = default; return false; }
        return TryParse(s.AsSpan(), out result);
    }

    /// <summary>
    /// Attempts to parse a hexadecimal span into an <see cref="XxHash128"/> without heap allocations.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryParse(ReadOnlySpan<char> s, out XxHash128 result) {
        if(HexString.TryParse(s, out HexString hex)) return TryParse(hex, out result);
        result = default;
        return false;
    }

    /// <summary>
    /// Attempts to parse a UTF-8 encoded byte span into an <see cref="XxHash128"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryParse(ReadOnlySpan<byte> utf8Text, out XxHash128 result) {
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
    /// Attempts to parse a <see cref="HexString"/> into an <see cref="XxHash128"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryParse(HexString hex, out XxHash128 result) {
        if(hex.GetDecodedLength() != HashSizeInBytes) { result = default; return false; }
        Span<byte> buffer = stackalloc byte[HashSizeInBytes];
        hex.TryDecode(buffer, out _);
        result = new(buffer);
        return true;
    }

    #endregion

    #region Explicit Interface Implementations (IParsable, ISpanParsable, IUtf8SpanParsable)

    static XxHash128 IParsable<XxHash128>.Parse(string s, IFormatProvider? provider) => Parse(s);
    static bool IParsable<XxHash128>.TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out XxHash128 result) => TryParse(s, out result);
    static XxHash128 ISpanParsable<XxHash128>.Parse(ReadOnlySpan<char> s, IFormatProvider? provider) => Parse(s);
    static bool ISpanParsable<XxHash128>.TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out XxHash128 result) => TryParse(s, out result);
    static XxHash128 IUtf8SpanParsable<XxHash128>.Parse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider) => Parse(utf8Text);
    static bool IUtf8SpanParsable<XxHash128>.TryParse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider, out XxHash128 result) => TryParse(utf8Text, out result);

    #endregion

    #region High-Performance Computation

    /// <summary>
    /// Computes the XXHash3-128 hash for the contents of a secure <see cref="Secret{Byte}"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static XxHash128 Compute(Secret<byte> secret) {
        Preca.ThrowIfNull(secret);
        return secret.Expose(span => Compute(span));
    }

    /// <summary>
    /// Computes the XXHash3-128 hash of a byte span using SIMD hardware acceleration without heap allocations.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [SkipLocalsInit]
    public static XxHash128 Compute(ReadOnlySpan<byte> data) {
        return new(XxHash128Core.HashToUInt128(data));
    }

    /// <summary>
    /// Computes the XXHash3-128 hash of a character span using UTF-8 encoding.
    /// </summary>
    /// <param name="chars">The character span to hash.</param>
    /// <returns>A new <see cref="XxHash128"/> instance containing the 128-bit digest.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static XxHash128 Compute(ReadOnlySpan<char> chars) {
        return Compute(chars, Encoding.UTF8);
    }

    /// <summary>
    /// Computes the XXHash3-128 hash of a character span using the specified encoding.
    /// </summary>
    /// <param name="chars">The character span to hash.</param>
    /// <param name="encoding">The encoding used to convert the characters to bytes before hashing.</param>
    /// <returns>A new <see cref="XxHash128"/> instance containing the 128-bit digest.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [SkipLocalsInit]
    public static XxHash128 Compute(ReadOnlySpan<char> chars, Encoding encoding) {
        Preca.ThrowIfNull(encoding);
        int maxByteCount = encoding.GetMaxByteCount(chars.Length);
        using ValueBuffer<byte> buffer = new(maxByteCount, stackalloc byte[1024]);
        int bytesWritten = encoding.GetBytes(chars, buffer.Span);
        return Compute(buffer.Span[..bytesWritten]);
    }

    /// <summary>
    /// Computes the XXHash3-128 hash for the contents of a secure <see cref="Secret{Char}"/> using the specified encoding.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static XxHash128 Compute(Secret<char> secret, Encoding encoding) {
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
    /// Computes the XXHash3-128 hash for the contents of a secure <see cref="Secret{Char}"/> using UTF-8 encoding.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static XxHash128 Compute(Secret<char> secret) {
        return Compute(secret, Encoding.UTF8);
    }

    /// <summary>
    /// Computes the XXHash3-128 hash of a string using the specified encoding.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [SkipLocalsInit]
    public static XxHash128 Compute(string text, Encoding encoding) {
        Preca.ThrowIfNull(text);
        Preca.ThrowIfNull(encoding);
        int maxByteCount = encoding.GetMaxByteCount(text.Length);
        using ValueBuffer<byte> buffer = new(maxByteCount, stackalloc byte[1024]);
        int bytesWritten = encoding.GetBytes(text, buffer.Span);
        return Compute(buffer.Span[..bytesWritten]);
    }

    /// <summary>
    /// Computes the XXHash3-128 hash of a string using UTF-8 encoding.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static XxHash128 Compute(string text) {
        return Compute(text, Encoding.UTF8);
    }

    #endregion

    #region Data Access & Conversion

    /// <summary>
    /// Returns a <see cref="ReadOnlySpan{Byte}"/> view of the 16 hash bytes without heap allocations.
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
    /// Compares two <see cref="XxHash128"/> hashes for equality using a fast 128-bit integer comparison.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool Equals(XxHash128 other) {
        return this._value == other._value;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override readonly bool Equals(object? obj) {
        return obj is XxHash128 other && Equals(other);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override readonly int GetHashCode() {
        return this._value.GetHashCode();
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CompareTo(XxHash128 other) => this._value.CompareTo(other._value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CompareTo(object? obj) {
        if(obj is null) return 1;
        if(obj is XxHash128 other) return CompareTo(other);
        throw new ArgumentException($"Object must be of type {nameof(XxHash128)}.", nameof(obj));
    }

    /// <inheritdoc cref="IComparisonOperators{TSelf, TOther, TResult}.op_GreaterThan(TSelf, TOther)" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >(XxHash128 left, XxHash128 right) => left.CompareTo(right) > 0;

    /// <inheritdoc cref="IComparisonOperators{TSelf, TOther, TResult}.op_LessThan(TSelf, TOther)" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <(XxHash128 left, XxHash128 right) => left.CompareTo(right) < 0;

    /// <inheritdoc cref="IComparisonOperators{TSelf, TOther, TResult}.op_GreaterThanOrEqual(TSelf, TOther)" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >=(XxHash128 left, XxHash128 right) => left.CompareTo(right) >= 0;

    /// <inheritdoc cref="IComparisonOperators{TSelf, TOther, TResult}.op_LessThanOrEqual(TSelf, TOther)" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <=(XxHash128 left, XxHash128 right) => left.CompareTo(right) <= 0;

    /// <inheritdoc cref="IEqualityOperators{TSelf,TOther,TResult}.op_Equality"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(XxHash128 left, XxHash128 right) {
        return left.Equals(right);
    }

    /// <inheritdoc cref="IEqualityOperators{TSelf,TOther,TResult}.op_Inequality"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(XxHash128 left, XxHash128 right) {
        return !left.Equals(right);
    }

    #endregion

    #region Alternate Comparers (.NET 10 Alternate Lookup)

    /// <summary>
    /// Gets an equality comparer that performs ordinal comparisons on <see cref="XxHash128"/>
    /// and supports zero-allocation alternate lookups using <see cref="ReadOnlySpan{Char}"/>.
    /// </summary>
    public static IEqualityComparer<XxHash128> OrdinalComparer => XxHash128OrdinalComparer.Instance;

    /// <summary>
    /// Gets an equality comparer that performs case-insensitive ordinal comparisons on <see cref="XxHash128"/>
    /// and supports zero-allocation alternate lookups using <see cref="ReadOnlySpan{Char}"/>.
    /// </summary>
    public static IEqualityComparer<XxHash128> OrdinalIgnoreCaseComparer => XxHash128OrdinalIgnoreCaseComparer.Instance;

    private sealed class XxHash128OrdinalComparer : IEqualityComparer<XxHash128>, IAlternateEqualityComparer<ReadOnlySpan<char>, XxHash128> {
        public static XxHash128OrdinalComparer Instance { get; } = new();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(XxHash128 x, XxHash128 y) => x.Equals(y);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetHashCode(XxHash128 obj) => obj.GetHashCode();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(ReadOnlySpan<char> alternate, XxHash128 other) {
            if(XxHash128.TryParse(alternate, out XxHash128 parsed)) {
                return parsed.Equals(other);
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetHashCode(ReadOnlySpan<char> alternate) {
            if(XxHash128.TryParse(alternate, out XxHash128 parsed)) {
                return parsed.GetHashCode();
            }
            return 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public XxHash128 Create(ReadOnlySpan<char> alternate) => XxHash128.Parse(alternate);
    }

    private sealed class XxHash128OrdinalIgnoreCaseComparer : IEqualityComparer<XxHash128>, IAlternateEqualityComparer<ReadOnlySpan<char>, XxHash128> {
        public static XxHash128OrdinalIgnoreCaseComparer Instance { get; } = new();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(XxHash128 x, XxHash128 y) => x.Equals(y);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetHashCode(XxHash128 obj) => obj.GetHashCode();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(ReadOnlySpan<char> alternate, XxHash128 other) {
            if(XxHash128.TryParse(alternate, out XxHash128 parsed)) {
                return parsed.Equals(other);
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetHashCode(ReadOnlySpan<char> alternate) {
            if(XxHash128.TryParse(alternate, out XxHash128 parsed)) {
                return parsed.GetHashCode();
            }
            return 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public XxHash128 Create(ReadOnlySpan<char> alternate) => XxHash128.Parse(alternate);
    }

    #endregion
}

/// <summary>
/// Extension methods for <see cref="XxHash128"/>.
/// </summary>
public static partial class XxHash128Extensions {
    extension(XxHash128) {
        /// <summary>
        /// Asynchronously computes the <see cref="XxHash128"/> hash of a stream using SIMD hardware streaming.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValueTask<XxHash128> ComputeAsync(Stream stream) => ComputeAsync(stream, CancellationToken.None);

        /// <summary>
        /// Asynchronously computes the <see cref="XxHash128"/> hash of a stream using SIMD hardware streaming.
        /// </summary>
        public static async ValueTask<XxHash128> ComputeAsync(
            Stream stream, CancellationToken cancellationToken) {
            Preca.ThrowIfNull(stream);
            if(stream.CanSeek) stream.Position = 0;

            XxHash128Core hasher = new();
            byte[] rented = ArrayPool<byte>.Shared.Rent(81_920); // 80 KB chunks

            try {
                int bytesRead;
                while((bytesRead = await stream.ReadAsync(rented, cancellationToken)
                    .ConfigureAwait(false)) > 0) {
                    hasher.Append(rented.AsSpan(0, bytesRead));
                }

                return new(hasher.GetCurrentHashAsUInt128());
            }
            finally {
                ArrayPool<byte>.Shared.Return(rented);
                if(stream.CanSeek) stream.Position = 0;
            }
        }
    }
}
