using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using Wiaoj.Primitives.Buffers;
using Wiaoj.Primitives.JsonConverters;

namespace Wiaoj.Primitives.Cryptography.Hashing;
/// <summary>
/// Represents a fixed-size, 64-byte HMAC-SHA512 hash.
/// </summary>
/// <remarks>
/// This struct utilizes a fixed-size buffer to ensure that the hash is stored inline, 
/// minimizing heap allocations and pressure on the Garbage Collector.
/// Equality comparisons are implemented using <see cref="CryptographicOperations.FixedTimeEquals"/> 
/// to prevent timing-based side-channel attacks.
/// </remarks>
[DebuggerDisplay("{ToString(),nq}")]
[StructLayout(LayoutKind.Sequential)]
[JsonConverter(typeof(HmacSha512HashJsonConverter))]
public unsafe struct HmacSha512Hash
    : IFixedBinaryValue<HmacSha512Hash>,
    IEquatable<HmacSha512Hash>,
    IComparable<HmacSha512Hash>,
    IComparable,
    IParsable<HmacSha512Hash>,
    ISpanParsable<HmacSha512Hash>,
    IUtf8SpanParsable<HmacSha512Hash>,
    ISpanFormattable,
    IUtf8SpanFormattable,
    IFormattable,
    IEqualityOperators<HmacSha512Hash, HmacSha512Hash, bool>,
    IComparisonOperators<HmacSha512Hash, HmacSha512Hash, bool> {
    /// <summary>The size of the HMAC-SHA512 hash in bytes (64 bytes).</summary>
    internal const int HashSizeInBytes = 64;

    /// <inheritdoc/>
    public static int SizeInBytes => HashSizeInBytes;


    private fixed byte _bytes[HashSizeInBytes];

    /// <summary>
    /// Initializes a new instance of the <see cref="HmacSha512Hash"/> struct from a span of bytes.
    /// </summary>
    /// <param name="source">A span containing exactly 64 bytes of hash data.</param>
    /// <exception cref="ArgumentException">Thrown when the source span length is not 64.</exception>
    internal HmacSha512Hash(ReadOnlySpan<byte> source) {
        Preca.ThrowIf<(int Size, string Name), ArgumentException>(
            condition: source.Length != HashSizeInBytes,
            exceptionFactory: static (state) => new ArgumentException($"Source span must be exactly {state.Size} bytes long.", state.Name),
            state: (HashSizeInBytes, nameof(source))
        );

        fixed(byte* pDest = this._bytes) {
            fixed(byte* pSrc = source) {
                Unsafe.CopyBlock(pDest, pSrc, HashSizeInBytes);
            }
        }
    }

    /// <summary>Represents an empty (zero-filled) HMAC-SHA512 hash.</summary>
    public static readonly HmacSha512Hash Empty = default;

    #region Computation

    /// <summary>
    /// Computes the HMAC-SHA512 hash of the specified data using a secure secret key.
    /// </summary>
    /// <param name="key">The secret key stored in unmanaged memory.</param>
    /// <param name="data">The data to be hashed.</param>
    /// <returns>A <see cref="HmacSha512Hash"/> instance representing the result.</returns>
    [SkipLocalsInit]
    public static HmacSha512Hash Compute(Secret<byte> key, ReadOnlySpan<byte> data) {
        return key.Expose(data, static (dataState, keySpan) => Compute(keySpan, dataState));
    }

    /// <summary>
    /// Computes the HMAC-SHA512 hash where both the key and the data are sensitive secrets.
    /// </summary>
    /// <param name="key">The secret key.</param>
    /// <param name="data">The secret data.</param>
    /// <returns>A <see cref="HmacSha512Hash"/> instance.</returns>
    public static HmacSha512Hash Compute(Secret<byte> key, Secret<byte> data) {
        return data.Expose(key, static (keySecret, dataSpan) => Compute(keySecret, dataSpan));
    }

    /// <summary>
    /// Computes the HMAC-SHA256 hash of a string using a secure secret key and the specified encoding.
    /// </summary>
    public static HmacSha512Hash Compute(Secret<byte> key, string data, Encoding encoding) {
        Preca.ThrowIfNull(data);
        return Compute(key, data.AsSpan(), encoding);
    }

    /// <summary>
    /// Computes the HMAC-SHA256 hash of a string using a secure secret key and UTF-8 encoding.
    /// </summary>
    public static HmacSha512Hash Compute(Secret<byte> key, string data) {
        return Compute(key, data, Encoding.UTF8);
    }

    /// <summary>
    /// Computes the HMAC-SHA512 hash using raw byte spans for both key and data.
    /// </summary>
    /// <param name="key">The cryptographic key.</param>
    /// <param name="data">The data to hash.</param>
    /// <returns>A <see cref="HmacSha512Hash"/> instance.</returns>
    [SkipLocalsInit]
    public static HmacSha512Hash Compute(ReadOnlySpan<byte> key, ReadOnlySpan<byte> data) {
        Span<byte> hashBuffer = stackalloc byte[HashSizeInBytes];
        HMACSHA512.HashData(key, data, hashBuffer);
        return new HmacSha512Hash(hashBuffer);
    }

    /// <summary>
    /// Computes the HMAC-SHA512 hash of a string using the specified key and encoding.
    /// </summary>
    /// <param name="key">The cryptographic key.</param>
    /// <param name="data">The string data to hash.</param>
    /// <param name="encoding">The character encoding used to convert the string to bytes.</param>
    /// <returns>A <see cref="HmacSha512Hash"/> instance.</returns>
    public static HmacSha512Hash Compute(ReadOnlySpan<byte> key, string data, Encoding encoding) {
        Preca.ThrowIfNull(data);
        return Compute(key, data.AsSpan(), encoding);
    }

    /// <summary>
    /// Computes the HMAC-SHA512 hash of a string using the specified key and UTF-8 encoding.
    /// </summary>
    /// <param name="key">The cryptographic key.</param>
    /// <param name="data">The string data to hash.</param>
    /// <returns>A <see cref="HmacSha512Hash"/> instance.</returns>
    public static HmacSha512Hash Compute(ReadOnlySpan<byte> key, string data) {
        return Compute(key, data, Encoding.UTF8);
    }

    /// <summary>
    /// Computes the HMAC-SHA512 hash of a character span using the specified key and UTF-8 encoding.
    /// </summary>
    /// <param name="key">The cryptographic key.</param>
    /// <param name="data">The character span to hash.</param>
    /// <returns>A <see cref="HmacSha512Hash"/> instance.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static HmacSha512Hash Compute(ReadOnlySpan<byte> key, ReadOnlySpan<char> data) {
        return Compute(key, data, Encoding.UTF8);
    }

    /// <summary>
    /// Computes the HMAC-SHA512 hash of a character span using the specified key and encoding.
    /// This method is allocation-free for inputs up to 1024 bytes after encoding.
    /// </summary>
    /// <param name="key">The cryptographic key.</param>
    /// <param name="data">The character span to hash.</param>
    /// <param name="encoding">The character encoding used to convert the characters to bytes.</param>
    /// <returns>A <see cref="HmacSha512Hash"/> instance.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [SkipLocalsInit]
    public static HmacSha512Hash Compute(ReadOnlySpan<byte> key, ReadOnlySpan<char> data, Encoding encoding) {
        Preca.ThrowIfNull(encoding);
        int maxByteCount = encoding.GetMaxByteCount(data.Length);
        using ValueBuffer<byte> buffer = new(maxByteCount, stackalloc byte[1024]);
        int bytesWritten = encoding.GetBytes(data, buffer.Span);
        return Compute(key, buffer.Span[..bytesWritten]);
    }

    /// <summary>
    /// Computes the HMAC-SHA512 hash of a character span using a secure secret key and UTF-8 encoding.
    /// </summary>
    /// <param name="key">The secret key stored in unmanaged memory.</param>
    /// <param name="data">The character span to hash.</param>
    /// <returns>A <see cref="HmacSha512Hash"/> instance.</returns>
    public static HmacSha512Hash Compute(Secret<byte> key, ReadOnlySpan<char> data) {
        return Compute(key, data, Encoding.UTF8);
    }

    /// <summary>
    /// Computes the HMAC-SHA512 hash of a character span using a secure secret key and the specified encoding.
    /// </summary>
    /// <param name="key">The secret key stored in unmanaged memory.</param>
    /// <param name="data">The character span to hash.</param>
    /// <param name="encoding">The character encoding used to convert the characters to bytes.</param>
    /// <returns>A <see cref="HmacSha512Hash"/> instance.</returns>
    [SkipLocalsInit]
    public static HmacSha512Hash Compute(Secret<byte> key, ReadOnlySpan<char> data, Encoding encoding) {
        Preca.ThrowIfNull(encoding);
        int maxByteCount = encoding.GetMaxByteCount(data.Length);
        using ValueBuffer<byte> buffer = new(maxByteCount, stackalloc byte[1024]);
        int bytesWritten = encoding.GetBytes(data, buffer.Span);
        return Compute(key, buffer.Span[..bytesWritten]);
    }

    #endregion

    #region Data Access & Conversion

    /// <summary>
    /// Returns a read-only span over the 64 bytes of the hash.
    /// </summary>
    /// <returns>A <see cref="ReadOnlySpan{Byte}"/> containing the hash data.</returns>
    public ReadOnlySpan<byte> AsSpan() {
        return MemoryMarshal.CreateReadOnlySpan(ref Unsafe.AsRef<byte>(Unsafe.AsPointer(ref this._bytes[0])), HashSizeInBytes);
    }

    /// <summary>
    /// Provides safe, scoped access to the hash bytes as a <see cref="ReadOnlySpan{Byte}"/>.
    /// </summary>
    /// <param name="action">The delegate to invoke with the hash bytes.</param>
    public void Expose(Action<ReadOnlySpan<byte>> action) {
        fixed(byte* p = this._bytes) {
            action(new ReadOnlySpan<byte>(p, HashSizeInBytes));
        }
    }

    /// <summary>
    /// Provides safe, scoped access to the hash bytes and returns a result.
    /// </summary>
    /// <typeparam name="TResult">The type of the value returned by <paramref name="func"/>.</typeparam>
    /// <param name="func">The delegate to invoke with the hash bytes.</param>
    /// <returns>The value returned by <paramref name="func"/>.</returns>
    public TResult Expose<TResult>(Func<ReadOnlySpan<byte>, TResult> func) {
        fixed(byte* p = this._bytes) {
            return func(new ReadOnlySpan<byte>(p, HashSizeInBytes));
        }
    }

    /// <summary>Converts the hash to its <see cref="HexString"/> representation.</summary>
    public HexString ToHexString() {
        return HexString.FromBytes(AsSpan());
    }

    /// <summary>
    /// Encodes the hash bytes into a lowercase <see cref="HexString"/>.
    /// This avoids the extra allocation caused by calling <c>ToHexString().ToLower()</c>.
    /// </summary>
    /// <returns>A lowercase <see cref="HexString"/> representation of the HMAC-SHA512 hash.</returns>
    public HexString ToHexStringLower() {
        return HexString.FromBytesLower(AsSpan());
    }

    /// <summary>Converts the hash to its <see cref="Base64String"/> representation.</summary>
    public Base64String ToBase64String() {
        return Base64String.FromBytes(AsSpan());
    }

    /// <summary>Converts the hash to its <see cref="Base64UrlString"/> representation.</summary>
    public Base64UrlString ToBase64UrlString() {
        return Base64UrlString.FromBytes(AsSpan());
    }

    /// <summary>Encodes the hash bytes into a type-safe <see cref="Base32String"/>.</summary>
    public Base32String ToBase32String() {
        return Base32String.FromBytes(AsSpan());
    }

    /// <summary>Encodes the hash bytes into a type-safe <see cref="Base62String"/>.</summary>
    public Base62String ToBase62String() {
        return Base62String.FromBytes(AsSpan());
    }

    /// <summary>Returns the hexadecimal string representation of the hash.</summary>
    /// <returns>An uppercase hexadecimal string.</returns>
    public override string ToString() {
        return Convert.ToHexString(AsSpan());
    }

    /// <summary>
    /// Returns the string representation of the hash using the specified format.
    /// </summary>
    public string ToString(string? format) {
        return ToString(format, null);
    }

    /// <summary>
    /// Returns the string representation of the hash using the specified format and provider.
    /// </summary>
    public string ToString(string? format, IFormatProvider? formatProvider) {
        return format is "x" ? Convert.ToHexStringLower(AsSpan()) : Convert.ToHexString(AsSpan());
    }

    /// <summary>
    /// Attempts to format the hash as an uppercase hexadecimal string into the destination character span.
    /// </summary>
    public bool TryFormat(Span<char> destination, out int charsWritten) {
        return TryFormat(destination, out charsWritten, default, null);
    }

    /// <summary>
    /// Attempts to format the hash into the destination character span using the specified format.
    /// </summary>
    public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format) {
        return TryFormat(destination, out charsWritten, format, null);
    }

    /// <summary>
    /// Attempts to format the hash into the destination character span using the specified format and provider.
    /// </summary>
    public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider) {
        int required = HashSizeInBytes * 2;
        if(destination.Length < required) { charsWritten = 0; return false; }
        bool lower = format.Equals("x", StringComparison.Ordinal);
        return lower
            ? Convert.TryToHexStringLower(AsSpan(), destination, out charsWritten)
            : Convert.TryToHexString(AsSpan(), destination, out charsWritten);
    }

    /// <summary>
    /// Attempts to format the hash as an uppercase UTF-8 hexadecimal byte span.
    /// </summary>
    public bool TryFormat(Span<byte> utf8Destination, out int bytesWritten) {
        return TryFormat(utf8Destination, out bytesWritten, default, null);
    }

    /// <summary>
    /// Attempts to format the hash into the destination UTF-8 byte span using the specified format.
    /// </summary>
    public bool TryFormat(Span<byte> utf8Destination, out int bytesWritten, ReadOnlySpan<char> format) {
        return TryFormat(utf8Destination, out bytesWritten, format, null);
    }

    /// <summary>
    /// Attempts to format the hash into the destination UTF-8 byte span using the specified format and provider.
    /// </summary>
    public bool TryFormat(Span<byte> utf8Destination, out int bytesWritten, ReadOnlySpan<char> format, IFormatProvider? provider) {
        int required = HashSizeInBytes * 2;
        if(utf8Destination.Length < required) { bytesWritten = 0; return false; }
        Span<char> charBuf = stackalloc char[required];
        bool lower = format.Equals("x", StringComparison.Ordinal);
        bool ok = lower
            ? Convert.TryToHexStringLower(AsSpan(), charBuf, out _)
            : Convert.TryToHexString(AsSpan(), charBuf, out _);
        if(!ok) { bytesWritten = 0; return false; }
        bytesWritten = Encoding.UTF8.GetBytes(charBuf, utf8Destination);
        return true;
    }

    #endregion

    #region Parsing

    /// <summary>
    /// Creates a <see cref="HmacSha512Hash"/> from a valid <see cref="HexString"/>.
    /// </summary>
    /// <param name="hex">The hex-encoded string containing the hash.</param>
    /// <returns>A new <see cref="HmacSha512Hash"/> instance.</returns>
    /// <exception cref="FormatException">Thrown when the hex string does not represent 64 bytes.</exception>
    public static HmacSha512Hash From(HexString hex) {
        Span<byte> buffer = stackalloc byte[HashSizeInBytes];
        if(hex.TryDecode(buffer, out int written) && written == HashSizeInBytes) {
            return new HmacSha512Hash(buffer);
        }
        throw new FormatException("Hex string length mismatch for HMAC-SHA512.");
    }

    /// <summary>
    /// Creates a <see cref="HmacSha512Hash"/> from a valid <see cref="Base64String"/>.
    /// </summary>
    /// <param name="base64">The base64-encoded string containing the hash.</param>
    /// <returns>A new <see cref="HmacSha512Hash"/> instance.</returns>
    /// <exception cref="FormatException">Thrown when the decoded base64 data is not 64 bytes.</exception>
    public static HmacSha512Hash From(Base64String base64) {
        Span<byte> buffer = stackalloc byte[HashSizeInBytes];
        if(base64.TryDecode(buffer, out int written) && written == HashSizeInBytes) {
            return new HmacSha512Hash(buffer);
        }
        throw new FormatException("Base64 string is not a valid 64-byte hash.");
    }

    /// <summary>
    /// Creates a hash instance from a valid <see cref="Base32String"/>.
    /// </summary>
    public static HmacSha512Hash From(Base32String base32) {
        Span<byte> buffer = stackalloc byte[HashSizeInBytes];
        if(base32.TryDecode(buffer, out int written) && written == HashSizeInBytes) {
            return new(buffer);
        }
        throw new FormatException($"Base32 string length mismatch for {HashSizeInBytes}-byte hash.");
    }

    /// <summary>
    /// Creates a hash instance from a valid <see cref="Base62String"/>.
    /// </summary>
    public static HmacSha512Hash From(Base62String base62) {
        byte[] bytes = base62.ToBytes();

        if(bytes.Length > HashSizeInBytes) {
            for(int i = 0; i < bytes.Length - HashSizeInBytes; i++) {
                if(bytes[i] != 0) throw new FormatException("Base62 string represents a value too large for this hash.");
            }
            return new(bytes.AsSpan(bytes.Length - HashSizeInBytes));
        }

        Span<byte> buffer = stackalloc byte[HashSizeInBytes];
        buffer.Clear();
        bytes.CopyTo(buffer[(HashSizeInBytes - bytes.Length)..]);
        return new(buffer);
    }

    /// <summary>
    /// Parses a hexadecimal string into a HmacSha512Hash.
    /// </summary>
    public static HmacSha512Hash Parse(string s) {
        ArgumentNullException.ThrowIfNull(s);
        if(!TryParse(s.AsSpan(), out HmacSha512Hash result)) {
            throw new FormatException($"Input string must represent exactly {HashSizeInBytes} bytes (128 hex characters).");
        }
        return result;
    }

    /// <summary>
    /// Parses a span of characters into a HmacSha512Hash. (Zero-allocation)
    /// </summary>
    public static HmacSha512Hash Parse(ReadOnlySpan<char> s) {
        if(!TryParse(s, out HmacSha512Hash result)) {
            throw new FormatException($"Input span must represent exactly {HashSizeInBytes} bytes (128 hex characters).");
        }
        return result;
    }

    /// <summary>
    /// Parses a UTF-8 encoded hexadecimal byte span into a <see cref="HmacSha512Hash"/>.
    /// </summary>
    public static HmacSha512Hash Parse(ReadOnlySpan<byte> utf8Text) {
        if(!TryParse(utf8Text, out HmacSha512Hash result)) {
            throw new FormatException("Invalid UTF-8 hexadecimal sequence for HmacSha512Hash.");
        }
        return result;
    }

    /// <summary>
    /// Tries to parse a hexadecimal string into a HmacSha512Hash.
    /// </summary>
    public static bool TryParse([NotNullWhen(true)] string? s, out HmacSha512Hash result) {
        if(s is null) { result = default; return false; }
        return TryParse(s.AsSpan(), out result);
    }

    /// <summary>
    /// Tries to parse a span of characters into a HmacSha512Hash.
    /// </summary>
    public static bool TryParse(ReadOnlySpan<char> s, out HmacSha512Hash result) {
        if(HexString.TryParse(s, out HexString hex)) {
            return TryParse(hex, out result);
        }
        result = default;
        return false;
    }

    /// <summary>
    /// Tries to parse a UTF-8 encoded byte span into a <see cref="HmacSha512Hash"/>.
    /// </summary>
    public static bool TryParse(ReadOnlySpan<byte> utf8Text, out HmacSha512Hash result) {
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
    /// Tries to create a <see cref="HmacSha512Hash"/> from a <see cref="HexString"/>.
    /// </summary>
    /// <param name="hex">The hex-encoded string to parse.</param>
    /// <param name="result">
    ///     When this method returns <see langword="true"/>, contains the parsed hash;
    ///     otherwise, contains the default value.
    /// </param>
    /// <returns>
    ///     <see langword="true"/> if <paramref name="hex"/> represents exactly 64 bytes;
    ///     otherwise, <see langword="false"/>.
    /// </returns>
    public static bool TryParse(HexString hex, out HmacSha512Hash result) {
        if(hex.GetDecodedLength() != HashSizeInBytes) {
            result = default;
            return false;
        }
        Span<byte> buffer = stackalloc byte[HashSizeInBytes];
        hex.TryDecode(buffer, out _);
        result = new HmacSha512Hash(buffer);
        return true;
    }

    #endregion

    #region Explicit Interface Implementations (IParsable, ISpanParsable, IUtf8SpanParsable)

    static HmacSha512Hash IParsable<HmacSha512Hash>.Parse(string s, IFormatProvider? provider) {
        return Parse(s);
    }

    static bool IParsable<HmacSha512Hash>.TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out HmacSha512Hash result) {
        return TryParse(s, out result);
    }

    static HmacSha512Hash ISpanParsable<HmacSha512Hash>.Parse(ReadOnlySpan<char> s, IFormatProvider? provider) {
        return Parse(s);
    }

    static bool ISpanParsable<HmacSha512Hash>.TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out HmacSha512Hash result) {
        return TryParse(s, out result);
    }

    static HmacSha512Hash IUtf8SpanParsable<HmacSha512Hash>.Parse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider) {
        return Parse(utf8Text);
    }

    static bool IUtf8SpanParsable<HmacSha512Hash>.TryParse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider, out HmacSha512Hash result) {
        return TryParse(utf8Text, out result);
    }

    #endregion

    /// <summary>
    /// Creates a <see cref="HmacSha512Hash"/> from a raw byte array.
    /// </summary>
    /// <param name="bytes">A byte array of exactly 64 bytes.</param>
    /// <returns>A new <see cref="HmacSha512Hash"/> instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="bytes"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the array length is not 64.</exception>
    public static HmacSha512Hash FromBytes(byte[] bytes) {
        Preca.ThrowIfNull(bytes);
        return new HmacSha512Hash(bytes.AsSpan());
    }

    /// <summary>
    /// Creates a <see cref="HmacSha512Hash"/> from a read-only span of bytes.
    /// </summary>
    /// <param name="source">A span of exactly 64 bytes.</param>
    /// <returns>A new <see cref="HmacSha512Hash"/> instance.</returns>
    /// <exception cref="ArgumentException">Thrown when the span length is not 64.</exception>
    public static HmacSha512Hash FromBytes(ReadOnlySpan<byte> source) {
        return new HmacSha512Hash(source);
    }

    /// <summary>
    /// Attempts to create a <see cref="HmacSha512Hash"/> from a read-only span of bytes.
    /// </summary>
    /// <param name="source">A span of bytes.</param>
    /// <param name="result">
    ///     When this method returns <see langword="true"/>, contains the parsed hash;
    ///     otherwise, contains the default value.
    /// </param>
    /// <returns>
    ///     <see langword="true"/> if <paramref name="source"/> contains exactly 64 bytes;
    ///     otherwise, <see langword="false"/>.
    /// </returns>
    public static bool TryFromBytes(ReadOnlySpan<byte> source, out HmacSha512Hash result) {
        if(source.Length != HashSizeInBytes) {
            result = default;
            return false;
        }
        result = new HmacSha512Hash(source);
        return true;
    }

    /// <summary>
    /// Copies the hash bytes to a destination span.
    /// </summary>
    /// <param name="destination">The span to copy the bytes into. Must be at least 64 bytes.</param>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="destination"/> is shorter than 64 bytes.
    /// </exception>
    public void CopyTo(Span<byte> destination) {
        if(destination.Length < HashSizeInBytes)
            throw new ArgumentException("Destination span must be at least 64 bytes long.", nameof(destination));
        fixed(byte* p = this._bytes) {
            new ReadOnlySpan<byte>(p, HashSizeInBytes).CopyTo(destination);
        }
    }

    /// <summary>
    /// Attempts to copy the hash bytes to the specified destination span.
    /// </summary>
    /// <param name="destination">The span to copy the bytes into.</param>
    /// <returns><see langword="true"/> if the copy was successful; otherwise, <see langword="false"/>.</returns>
    public bool TryCopyTo(Span<byte> destination) {
        if(destination.Length < HashSizeInBytes) return false;
        fixed(byte* p = this._bytes) {
            new ReadOnlySpan<byte>(p, HashSizeInBytes).CopyTo(destination);
        }
        return true;
    }

    /// <summary>
    /// Implicitly converts a <see cref="HmacSha512Hash"/> to a <see cref="ReadOnlySpan{Byte}"/>.
    /// </summary>
    public static implicit operator ReadOnlySpan<byte>(HmacSha512Hash hash) {
        return hash.AsSpan();
    }

    #region Equality & Comparison

    /// <summary>
    /// Determines whether two <see cref="HmacSha512Hash"/> instances are equal using a constant-time algorithm.
    /// </summary>
    public bool Equals(HmacSha512Hash other) {
        return FixedBinaryValueOps.Equals(this, other);
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) {
        return obj is HmacSha512Hash other && Equals(other);
    }

    /// <inheritdoc/>
    public override int GetHashCode() {
        return FixedBinaryValueOps.GetHashCode(this);
    }

    /// <inheritdoc/>
    public int CompareTo(HmacSha512Hash other) {
        return FixedBinaryValueOps.CompareTo(this, other);
    }

    /// <inheritdoc/>
    public int CompareTo(object? obj) {
        return FixedBinaryValueOps.CompareToObject(this, obj);
    }

    /// <inheritdoc cref="IComparisonOperators{TSelf, TOther, TResult}.op_GreaterThan(TSelf, TOther)" />
    public static bool operator >(HmacSha512Hash left, HmacSha512Hash right) {
        return left.CompareTo(right) > 0;
    }

    /// <inheritdoc cref="IComparisonOperators{TSelf, TOther, TResult}.op_LessThan(TSelf, TOther)" />
    public static bool operator <(HmacSha512Hash left, HmacSha512Hash right) {
        return left.CompareTo(right) < 0;
    }

    /// <inheritdoc cref="IComparisonOperators{TSelf, TOther, TResult}.op_GreaterThanOrEqual(TSelf, TOther)" />
    public static bool operator >=(HmacSha512Hash left, HmacSha512Hash right) {
        return left.CompareTo(right) >= 0;
    }

    /// <inheritdoc cref="IComparisonOperators{TSelf, TOther, TResult}.op_LessThanOrEqual(TSelf, TOther)" />
    public static bool operator <=(HmacSha512Hash left, HmacSha512Hash right) {
        return left.CompareTo(right) <= 0;
    }

    /// <inheritdoc cref="IEqualityOperators{TSelf, TOther, TResult}.op_Equality(TSelf, TOther)" />
    public static bool operator ==(HmacSha512Hash left, HmacSha512Hash right) {
        return left.Equals(right);
    }

    /// <inheritdoc cref="IEqualityOperators{TSelf, TOther, TResult}.op_Inequality(TSelf, TOther)" />
    public static bool operator !=(HmacSha512Hash left, HmacSha512Hash right) {
        return !left.Equals(right);
    }

    #endregion

    #region Alternate Comparers (.NET 10 Alternate Lookup)

    /// <summary>
    /// Gets an equality comparer that performs ordinal comparisons on <see cref="HmacSha512Hash"/>
    /// and supports zero-allocation alternate lookups using <see cref="ReadOnlySpan{Char}"/>.
    /// </summary>
    public static IEqualityComparer<HmacSha512Hash> OrdinalComparer => HmacSha512HashOrdinalComparer.Instance;

    /// <summary>
    /// Gets an equality comparer that performs case-insensitive ordinal comparisons on <see cref="HmacSha512Hash"/>
    /// and supports zero-allocation alternate lookups using <see cref="ReadOnlySpan{Char}"/>.
    /// </summary>
    public static IEqualityComparer<HmacSha512Hash> OrdinalIgnoreCaseComparer => HmacSha512HashOrdinalIgnoreCaseComparer.Instance;

    private sealed class HmacSha512HashOrdinalComparer : IEqualityComparer<HmacSha512Hash>, IAlternateEqualityComparer<ReadOnlySpan<char>, HmacSha512Hash> {
        public static HmacSha512HashOrdinalComparer Instance { get; } = new();

        public bool Equals(HmacSha512Hash x, HmacSha512Hash y) {
            return x.Equals(y);
        }

        public int GetHashCode(HmacSha512Hash obj) {
            return obj.GetHashCode();
        }

        public bool Equals(ReadOnlySpan<char> alternate, HmacSha512Hash other) {
            if(HmacSha512Hash.TryParse(alternate, out HmacSha512Hash parsed)) {
                return parsed.Equals(other);
            }
            return false;
        }

        public int GetHashCode(ReadOnlySpan<char> alternate) {
            if(HmacSha512Hash.TryParse(alternate, out HmacSha512Hash parsed)) {
                return parsed.GetHashCode();
            }
            return 0;
        }

        public HmacSha512Hash Create(ReadOnlySpan<char> alternate) {
            return HmacSha512Hash.Parse(alternate);
        }
    }

    private sealed class HmacSha512HashOrdinalIgnoreCaseComparer : IEqualityComparer<HmacSha512Hash>, IAlternateEqualityComparer<ReadOnlySpan<char>, HmacSha512Hash> {
        public static HmacSha512HashOrdinalIgnoreCaseComparer Instance { get; } = new();

        public bool Equals(HmacSha512Hash x, HmacSha512Hash y) {
            return x.Equals(y);
        }

        public int GetHashCode(HmacSha512Hash obj) {
            return obj.GetHashCode();
        }

        public bool Equals(ReadOnlySpan<char> alternate, HmacSha512Hash other) {
            if(HmacSha512Hash.TryParse(alternate, out HmacSha512Hash parsed)) {
                return parsed.Equals(other);
            }
            return false;
        }

        public int GetHashCode(ReadOnlySpan<char> alternate) {
            if(HmacSha512Hash.TryParse(alternate, out HmacSha512Hash parsed)) {
                return parsed.GetHashCode();
            }
            return 0;
        }

        public HmacSha512Hash Create(ReadOnlySpan<char> alternate) {
            return HmacSha512Hash.Parse(alternate);
        }
    }

    #endregion
}

/// <summary>
/// Extension methods for <see cref="HmacSha512Hash"/>.
/// </summary>
public static partial class HmacSha512HashExtensions {
    extension(HmacSha512Hash) {
        /// <summary>
        /// Asynchronously computes the HMAC-SHA512 hash of a stream.
        /// Ensures the stream is reset before and after computation, and manages memory securely.
        /// </summary>
        public static ValueTask<HmacSha512Hash> ComputeAsync(Stream stream, Secret<byte> key) {
            return ComputeAsync(stream, key, CancellationToken.None);
        }

        /// <summary>
        /// Asynchronously computes the HMAC-SHA512 hash of a stream.
        /// Ensures the stream is reset before and after computation, and manages memory securely.
        /// </summary>
        public static async ValueTask<HmacSha512Hash> ComputeAsync(
            Stream stream,
            Secret<byte> key,
            CancellationToken cancellationToken) {
            Preca.ThrowIfNull(stream);
            Preca.ThrowIfNull(key);

            if(stream.CanSeek) stream.Position = 0;

            int keyLength = key.Expose(k => k.Length);
            byte[] keyBuffer = ArrayPool<byte>.Shared.Rent(keyLength);
            byte[] hashBuffer = ArrayPool<byte>.Shared.Rent(HmacSha512Hash.HashSizeInBytes);

            try {
                key.Expose(k => k.CopyTo(keyBuffer));

                await HMACSHA512.HashDataAsync(
                    new ReadOnlyMemory<byte>(keyBuffer, 0, keyLength),
                    stream,
                    hashBuffer.AsMemory(0, HmacSha512Hash.HashSizeInBytes),
                    cancellationToken);

                return new HmacSha512Hash(hashBuffer.AsSpan(0, HmacSha512Hash.HashSizeInBytes));
            }
            finally {
                CryptographicOperations.ZeroMemory(keyBuffer.AsSpan(0, keyLength));
                ArrayPool<byte>.Shared.Return(keyBuffer);
                ArrayPool<byte>.Shared.Return(hashBuffer);

                if(stream.CanSeek)
                    stream.Position = 0;
            }
        }
    }
}