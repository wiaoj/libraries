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
/// Represents an immutable, fixed-size 16-byte (128-bit) MD5 hash.
/// </summary>
/// <remarks>
/// <para>
/// <b>Zero Heap Allocation:</b> Utilizes an inline fixed-size byte buffer to store the hash directly within the struct, 
/// minimizing Garbage Collector (GC) pressure in high-throughput paths.
/// </para>
/// <para>
/// <b>Side-Channel Resistance:</b> Equality comparisons (<see cref="Equals(Md5Hash)"/> and operator <c>==</c>) 
/// are implemented using <see cref="CryptographicOperations.FixedTimeEquals"/> to mitigate timing attacks.
/// </para>
/// </remarks>
[DebuggerDisplay("{ToString(),nq}")]
[StructLayout(LayoutKind.Sequential)]
[JsonConverter(typeof(Md5HashJsonConverter))]
public unsafe struct Md5Hash
    : IFixedBinaryValue<Md5Hash>,
    IEquatable<Md5Hash>,
    IComparable<Md5Hash>,
    IComparable,
    IParsable<Md5Hash>,
    ISpanParsable<Md5Hash>,
    IUtf8SpanParsable<Md5Hash>,
    ISpanFormattable,
    IUtf8SpanFormattable,
    IFormattable,
    IEqualityOperators<Md5Hash, Md5Hash, bool>,
    IComparisonOperators<Md5Hash, Md5Hash, bool> {

    /// <summary>The size of the MD5 hash in bytes (16 bytes / 128 bits).</summary>
    internal const int HashSizeInBytes = 16;

    /// <inheritdoc/>
    public static int SizeInBytes => HashSizeInBytes;


    private fixed byte _bytes[HashSizeInBytes];

    /// <summary>
    /// Initializes a new instance of the <see cref="Md5Hash"/> struct from a 16-byte span.
    /// </summary>
    /// <param name="source">A span containing exactly 16 bytes of hash data.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="source"/> length is not exactly 16 bytes.</exception>
    internal Md5Hash(ReadOnlySpan<byte> source) {
        if(source.Length != HashSizeInBytes) {
            throw new ArgumentException($"Source span must be exactly {HashSizeInBytes} bytes long.", nameof(source));
        }

        fixed(byte* p = this._bytes) {
            source.CopyTo(new Span<byte>(p, HashSizeInBytes));
        }
    }

    #region Factory Methods

    /// <summary>
    /// Represents an empty (zero-filled) 16-byte MD5 hash.
    /// </summary>
    public static readonly Md5Hash Empty = default;

    /// <summary>
    /// Creates a <see cref="Md5Hash"/> instance from a 16-byte read-only span.
    /// </summary>
    /// <param name="source">A span containing exactly 16 bytes of hash data.</param>
    /// <returns>A valid <see cref="Md5Hash"/> instance.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="source"/> is not exactly 16 bytes long.</exception>
    public static Md5Hash FromBytes(ReadOnlySpan<byte> source) {
        return new Md5Hash(source);
    }

    /// <summary>
    /// Creates a <see cref="Md5Hash"/> instance from a valid <see cref="HexString"/>.
    /// </summary>
    /// <param name="hex">The hex-encoded string representing the 16-byte hash (32 hex characters).</param>
    /// <returns>A new <see cref="Md5Hash"/> instance.</returns>
    /// <exception cref="FormatException">Thrown when <paramref name="hex"/> does not decode to exactly 16 bytes.</exception>
    public static Md5Hash From(HexString hex) {
        if(hex.GetDecodedLength() != HashSizeInBytes) {
            throw new FormatException($"Source HexString must represent exactly {HashSizeInBytes} bytes (32 hex characters).");
        }

        Span<byte> buffer = stackalloc byte[HashSizeInBytes];
        hex.TryDecode(buffer, out _);
        return new Md5Hash(buffer);
    }

    /// <summary>
    /// Creates a <see cref="Md5Hash"/> instance from a valid <see cref="Base64String"/>.
    /// </summary>
    /// <param name="base64">The Base64-encoded string representing the 16-byte hash.</param>
    /// <returns>A new <see cref="Md5Hash"/> instance.</returns>
    /// <exception cref="FormatException">Thrown when <paramref name="base64"/> does not decode to exactly 16 bytes.</exception>
    public static Md5Hash From(Base64String base64) {
        if(base64.GetDecodedLength() != HashSizeInBytes) {
            throw new FormatException($"Source Base64String must represent exactly {HashSizeInBytes} bytes.");
        }

        Span<byte> buffer = stackalloc byte[HashSizeInBytes];
        if(!base64.TryDecode(buffer, out int written) || written != HashSizeInBytes) {
            throw new FormatException("Failed to decode Base64 into MD5 hash.");
        }
        return new Md5Hash(buffer);
    }

    /// <summary>
    /// Creates a <see cref="Md5Hash"/> instance from a valid <see cref="Base32String"/>.
    /// </summary>
    /// <param name="base32">The Base32-encoded string representing the 16-byte hash.</param>
    /// <returns>A new <see cref="Md5Hash"/> instance.</returns>
    /// <exception cref="FormatException">Thrown when <paramref name="base32"/> does not decode to exactly 16 bytes.</exception>
    public static Md5Hash From(Base32String base32) {
        Span<byte> buffer = stackalloc byte[HashSizeInBytes];
        if(base32.TryDecode(buffer, out int written) && written == HashSizeInBytes) {
            return new(buffer);
        }
        throw new FormatException($"Base32 string length mismatch for {HashSizeInBytes}-byte hash.");
    }

    /// <summary>
    /// Creates a <see cref="Md5Hash"/> instance from a valid <see cref="Base62String"/>.
    /// </summary>
    /// <param name="base62">The Base62-encoded string representing the 16-byte hash.</param>
    /// <returns>A new <see cref="Md5Hash"/> instance.</returns>
    /// <exception cref="FormatException">Thrown when <paramref name="base62"/> represents a value exceeding 16 bytes.</exception>
    public static Md5Hash From(Base62String base62) {
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
    /// Parses a 32-character hexadecimal string into an <see cref="Md5Hash"/>.
    /// </summary>
    /// <param name="s">The hexadecimal string to parse.</param>
    /// <returns>The parsed <see cref="Md5Hash"/> instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="s"/> is null.</exception>
    /// <exception cref="FormatException">Thrown when <paramref name="s"/> is not a valid 32-character hexadecimal string.</exception>
    public static Md5Hash Parse(string s) {
        ArgumentNullException.ThrowIfNull(s);
        if(!TryParse(s.AsSpan(), out Md5Hash result)) {
            throw new FormatException($"Input string must represent exactly {HashSizeInBytes} bytes (32 hex characters).");
        }
        return result;
    }

    /// <summary>
    /// Parses a 32-character hexadecimal span into an <see cref="Md5Hash"/> without heap allocations.
    /// </summary>
    /// <param name="s">The span of characters to parse.</param>
    /// <returns>The parsed <see cref="Md5Hash"/> instance.</returns>
    /// <exception cref="FormatException">Thrown when <paramref name="s"/> is not a valid 32-character hexadecimal sequence.</exception>
    public static Md5Hash Parse(ReadOnlySpan<char> s) {
        if(!TryParse(s, out Md5Hash result)) {
            throw new FormatException($"Input span must represent exactly {HashSizeInBytes} bytes (32 hex characters).");
        }
        return result;
    }

    /// <summary>
    /// Parses a UTF-8 encoded hexadecimal byte span into an <see cref="Md5Hash"/>.
    /// </summary>
    public static Md5Hash Parse(ReadOnlySpan<byte> utf8Text) {
        if(!TryParse(utf8Text, out Md5Hash result)) {
            throw new FormatException("Invalid UTF-8 hexadecimal sequence for Md5Hash.");
        }
        return result;
    }

    /// <summary>
    /// Attempts to parse a hexadecimal string into an <see cref="Md5Hash"/>.
    /// </summary>
    public static bool TryParse([NotNullWhen(true)] string? s, out Md5Hash result) {
        if(s is null) { result = default; return false; }
        return TryParse(s.AsSpan(), out result);
    }

    /// <summary>
    /// Attempts to parse a hexadecimal span into an <see cref="Md5Hash"/> without heap allocations.
    /// </summary>
    public static bool TryParse(ReadOnlySpan<char> s, out Md5Hash result) {
        if(HexString.TryParse(s, out HexString hex)) {
            return TryParse(hex, out result);
        }
        result = default;
        return false;
    }

    /// <summary>
    /// Attempts to parse a UTF-8 encoded byte span into an <see cref="Md5Hash"/>.
    /// </summary>
    public static bool TryParse(ReadOnlySpan<byte> utf8Text, out Md5Hash result) {
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
    /// Attempts to parse a <see cref="HexString"/> into an <see cref="Md5Hash"/>.
    /// </summary>
    /// <param name="hex">The hex-encoded string to parse.</param>
    /// <param name="result">When this method returns, contains the parsed hash if successful; otherwise, default.</param>
    /// <returns><see langword="true"/> if <paramref name="hex"/> represents exactly 16 bytes; otherwise, <see langword="false"/>.</returns>
    public static bool TryParse(HexString hex, out Md5Hash result) {
        if(hex.GetDecodedLength() != HashSizeInBytes) {
            result = default;
            return false;
        }

        Span<byte> buffer = stackalloc byte[HashSizeInBytes];
        hex.TryDecode(buffer, out _);
        result = new Md5Hash(buffer);
        return true;
    }

    #endregion

    #region Explicit Interface Implementations (IParsable, ISpanParsable, IUtf8SpanParsable)

    static Md5Hash IParsable<Md5Hash>.Parse(string s, IFormatProvider? provider) {
        return Parse(s);
    }

    static bool IParsable<Md5Hash>.TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out Md5Hash result) {
        return TryParse(s, out result);
    }

    static Md5Hash ISpanParsable<Md5Hash>.Parse(ReadOnlySpan<char> s, IFormatProvider? provider) {
        return Parse(s);
    }

    static bool ISpanParsable<Md5Hash>.TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out Md5Hash result) {
        return TryParse(s, out result);
    }

    static Md5Hash IUtf8SpanParsable<Md5Hash>.Parse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider) {
        return Parse(utf8Text);
    }

    static bool IUtf8SpanParsable<Md5Hash>.TryParse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider, out Md5Hash result) {
        return TryParse(utf8Text, out result);
    }

    #endregion

    #region High-Performance Computation

    /// <summary>
    /// Computes the MD5 hash for the contents of a secure <see cref="Secret{Byte}"/>.
    /// </summary>
    /// <param name="secret">The secret byte data to hash.</param>
    /// <returns>A new <see cref="Md5Hash"/> containing the digest.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="secret"/> is null.</exception>
    public static Md5Hash Compute(Secret<byte> secret) {
        Preca.ThrowIfNull(secret);
        return secret.Expose(span => Compute(span));
    }

    /// <summary>
    /// Computes the MD5 hash of a byte span without heap allocations.
    /// </summary>
    /// <param name="data">The byte span to hash.</param>
    /// <returns>A new <see cref="Md5Hash"/> containing the digest.</returns>
    [SkipLocalsInit]
    public static Md5Hash Compute(ReadOnlySpan<byte> data) {
        Span<byte> hashBuffer = stackalloc byte[HashSizeInBytes];
        MD5.HashData(data, hashBuffer);
        return new Md5Hash(hashBuffer);
    }

    /// <summary>
    /// Computes the MD5 hash of a character span using UTF-8 encoding.
    /// </summary>
    /// <param name="data">The character span to hash.</param>
    /// <returns>A new <see cref="Md5Hash"/> containing the digest.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Md5Hash Compute(ReadOnlySpan<char> data) {
        return Compute(data, Encoding.UTF8);
    }

    /// <summary>
    /// Computes the MD5 hash of a character span using the specified encoding.
    /// This method is allocation-free for inputs up to 1024 bytes after encoding.
    /// </summary>
    /// <param name="data">The character span to hash.</param>
    /// <param name="encoding">The character encoding to use when converting the characters to bytes.</param>
    /// <returns>A new <see cref="Md5Hash"/> containing the digest.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [SkipLocalsInit]
    public static Md5Hash Compute(ReadOnlySpan<char> data, Encoding encoding) {
        Preca.ThrowIfNull(encoding);
        int maxByteCount = encoding.GetMaxByteCount(data.Length);
        using ValueBuffer<byte> buffer = new(maxByteCount, stackalloc byte[1024]);
        int bytesWritten = encoding.GetBytes(data, buffer.Span);
        return Compute(buffer.Span[..bytesWritten]);
    }

    /// <summary>
    /// Computes the MD5 hash for the contents of a secure <see cref="Secret{Char}"/> using the specified encoding.
    /// </summary>
    /// <param name="secret">The secret character data to hash.</param>
    /// <param name="encoding">The character encoding used to convert the secret characters to bytes.</param>
    /// <returns>A new <see cref="Md5Hash"/> containing the digest.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="secret"/> or <paramref name="encoding"/> is null.</exception>
    public static Md5Hash Compute(Secret<char> secret, Encoding encoding) {
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
    /// Computes the MD5 hash for the contents of a <see cref="Secret{T}"/> of <see cref="char"/> using UTF-8 encoding.
    /// </summary>
    /// <param name="secret">The secret containing the character data to hash.</param>
    /// <returns>A new <see cref="Md5Hash"/> containing the digest.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="secret"/> is null.</exception>
    public static Md5Hash Compute(Secret<char> secret) {
        return Compute(secret, Encoding.UTF8);
    }

    /// <summary>
    /// Computes the MD5 hash of a string using UTF-8 encoding.
    /// </summary>
    /// <param name="text">The string to hash.</param>
    /// <returns>A new <see cref="Md5Hash"/> containing the digest.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> is null.</exception>
    public static Md5Hash Compute(string text) {
        return Compute(text, Encoding.UTF8);
    }

    /// <summary>
    /// Computes the MD5 hash of a string using the specified character encoding.
    /// </summary>
    /// <param name="text">The string to hash.</param>
    /// <param name="encoding">The character encoding to use.</param>
    /// <returns>A new <see cref="Md5Hash"/> containing the digest.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> or <paramref name="encoding"/> is null.</exception>
    public static Md5Hash Compute(string text, Encoding encoding) {
        Preca.ThrowIfNull(text);
        return Compute(text.AsSpan(), encoding);
    }

    #endregion

    #region Data Access & Conversion

    /// <summary>
    /// Provides safe, scoped access to the hash bytes as a <see cref="ReadOnlySpan{Byte}"/>.
    /// </summary>
    /// <param name="action">The delegate receiving the read-only span.</param>
    public void Expose(Action<ReadOnlySpan<byte>> action) {
        fixed(byte* p = this._bytes) {
            action(new ReadOnlySpan<byte>(p, HashSizeInBytes));
        }
    }

    /// <summary>
    /// Provides safe, scoped access to the hash bytes and returns a result.
    /// </summary>
    /// <typeparam name="TResult">The type of the result returned by the delegate.</typeparam>
    /// <param name="func">The delegate receiving the read-only span and returning a result.</param>
    /// <returns>The result computed by <paramref name="func"/>.</returns>
    public TResult Expose<TResult>(Func<ReadOnlySpan<byte>, TResult> func) {
        fixed(byte* p = this._bytes) {
            return func(new ReadOnlySpan<byte>(p, HashSizeInBytes));
        }
    }

    /// <summary>
    /// Copies the hash bytes into a destination span.
    /// </summary>
    /// <param name="destination">The destination span. Must be at least 16 bytes long.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="destination"/> is shorter than 16 bytes.</exception>
    public void CopyTo(Span<byte> destination) {
        if(destination.Length < HashSizeInBytes) {
            throw new ArgumentException($"Destination span must be at least {HashSizeInBytes} bytes long.", nameof(destination));
        }
        AsSpan().CopyTo(destination);
    }

    /// <summary>
    /// Attempts to copy the hash bytes into the specified destination span.
    /// </summary>
    /// <param name="destination">The span to copy the bytes into.</param>
    /// <returns><see langword="true"/> if the copy was successful; otherwise, <see langword="false"/>.</returns>
    public bool TryCopyTo(Span<byte> destination) {
        if(destination.Length < HashSizeInBytes) return false;
        AsSpan().CopyTo(destination);
        return true;
    }

    /// <summary>
    /// Returns a direct <see cref="ReadOnlySpan{Byte}"/> view over the inline hash bytes.
    /// </summary>
    /// <returns>A 16-byte <see cref="ReadOnlySpan{Byte}"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlySpan<byte> AsSpan() {
        return new ReadOnlySpan<byte>(Unsafe.AsPointer(ref Unsafe.AsRef(in this._bytes[0])), HashSizeInBytes);
    }

    /// <summary>
    /// Encodes the hash bytes into an uppercase <see cref="HexString"/>.
    /// </summary>
    /// <returns>An uppercase <see cref="HexString"/> representation of the hash.</returns>
    public HexString ToHexString() {
        return HexString.FromBytes(AsSpan());
    }

    /// <summary>
    /// Encodes the hash bytes into a lowercase <see cref="HexString"/> without string allocations.
    /// </summary>
    /// <returns>A lowercase <see cref="HexString"/> representation of the hash.</returns>
    public HexString ToHexStringLower() {
        return HexString.FromBytesLower(AsSpan());
    }

    /// <summary>
    /// Encodes the hash bytes into a type-safe <see cref="Base64String"/>.
    /// </summary>
    /// <returns>A <see cref="Base64String"/> representation of the hash.</returns>
    public Base64String ToBase64String() {
        return Base64String.FromBytes(AsSpan());
    }

    /// <summary>
    /// Encodes the hash bytes into a type-safe, URL-safe <see cref="Base64UrlString"/>.
    /// </summary>
    /// <returns>A <see cref="Base64UrlString"/> representation of the hash.</returns>
    public Base64UrlString ToBase64UrlString() {
        return Base64UrlString.FromBytes(AsSpan());
    }

    /// <summary>
    /// Encodes the hash bytes into a type-safe <see cref="Base32String"/>.
    /// </summary>
    /// <returns>A <see cref="Base32String"/> representation of the hash.</returns>
    public Base32String ToBase32String() {
        return Base32String.FromBytes(AsSpan());
    }

    /// <summary>
    /// Encodes the hash bytes into a type-safe <see cref="Base62String"/>.
    /// </summary>
    /// <returns>A <see cref="Base62String"/> representation of the hash.</returns>
    public Base62String ToBase62String() {
        return Base62String.FromBytes(AsSpan());
    }

    /// <summary>
    /// Returns the uppercase hexadecimal string representation of the hash.
    /// </summary>
    /// <returns>An uppercase 32-character hexadecimal string.</returns>
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

    #region Equality & Comparison

    /// <summary>
    /// Determines whether two <see cref="Md5Hash"/> instances are equal using a constant-time algorithm.
    /// </summary>
    /// <param name="other">The other hash to compare against.</param>
    /// <returns><see langword="true"/> if both hashes contain identical byte sequences; otherwise, <see langword="false"/>.</returns>
    public bool Equals(Md5Hash other) {
        return FixedBinaryValueOps.Equals(this, other);
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) {
        return obj is Md5Hash other && Equals(other);
    }

    /// <inheritdoc/>
    public override int GetHashCode() {
        return FixedBinaryValueOps.GetHashCode(this);
    }

    /// <inheritdoc/>
    public int CompareTo(Md5Hash other) {
        return FixedBinaryValueOps.CompareTo(this, other);
    }

    /// <inheritdoc/>
    public int CompareTo(object? obj) {
        return FixedBinaryValueOps.CompareToObject(this, obj);
    }

    /// <inheritdoc cref="IComparisonOperators{TSelf, TOther, TResult}.op_GreaterThan(TSelf, TOther)" />
    public static bool operator >(Md5Hash left, Md5Hash right) {
        return left.CompareTo(right) > 0;
    }

    /// <inheritdoc cref="IComparisonOperators{TSelf, TOther, TResult}.op_LessThan(TSelf, TOther)" />
    public static bool operator <(Md5Hash left, Md5Hash right) {
        return left.CompareTo(right) < 0;
    }

    /// <inheritdoc cref="IComparisonOperators{TSelf, TOther, TResult}.op_GreaterThanOrEqual(TSelf, TOther)" />
    public static bool operator >=(Md5Hash left, Md5Hash right) {
        return left.CompareTo(right) >= 0;
    }

    /// <inheritdoc cref="IComparisonOperators{TSelf, TOther, TResult}.op_LessThanOrEqual(TSelf, TOther)" />
    public static bool operator <=(Md5Hash left, Md5Hash right) {
        return left.CompareTo(right) <= 0;
    }

    /// <inheritdoc cref="IEqualityOperators{TSelf, TOther, TResult}.op_Equality(TSelf, TOther)" />
    public static bool operator ==(Md5Hash left, Md5Hash right) {
        return left.Equals(right);
    }

    /// <inheritdoc cref="IEqualityOperators{TSelf, TOther, TResult}.op_Inequality(TSelf, TOther)" />
    public static bool operator !=(Md5Hash left, Md5Hash right) {
        return !left.Equals(right);
    }

    #endregion

    #region Alternate Comparers (.NET 10 Alternate Lookup)

    /// <summary>
    /// Gets an equality comparer that performs ordinal comparisons on <see cref="Md5Hash"/>
    /// and supports zero-allocation alternate lookups using <see cref="ReadOnlySpan{Char}"/>.
    /// </summary>
    public static IEqualityComparer<Md5Hash> OrdinalComparer => Md5HashOrdinalComparer.Instance;

    /// <summary>
    /// Gets an equality comparer that performs case-insensitive ordinal comparisons on <see cref="Md5Hash"/>
    /// and supports zero-allocation alternate lookups using <see cref="ReadOnlySpan{Char}"/>.
    /// </summary>
    public static IEqualityComparer<Md5Hash> OrdinalIgnoreCaseComparer => Md5HashOrdinalIgnoreCaseComparer.Instance;

    private sealed class Md5HashOrdinalComparer : IEqualityComparer<Md5Hash>, IAlternateEqualityComparer<ReadOnlySpan<char>, Md5Hash> {
        public static Md5HashOrdinalComparer Instance { get; } = new();

        public bool Equals(Md5Hash x, Md5Hash y) {
            return x.Equals(y);
        }

        public int GetHashCode(Md5Hash obj) {
            return obj.GetHashCode();
        }

        public bool Equals(ReadOnlySpan<char> alternate, Md5Hash other) {
            if(Md5Hash.TryParse(alternate, out Md5Hash parsed)) {
                return parsed.Equals(other);
            }
            return false;
        }

        public int GetHashCode(ReadOnlySpan<char> alternate) {
            if(Md5Hash.TryParse(alternate, out Md5Hash parsed)) {
                return parsed.GetHashCode();
            }
            return 0;
        }

        public Md5Hash Create(ReadOnlySpan<char> alternate) {
            return Md5Hash.Parse(alternate);
        }
    }

    private sealed class Md5HashOrdinalIgnoreCaseComparer : IEqualityComparer<Md5Hash>, IAlternateEqualityComparer<ReadOnlySpan<char>, Md5Hash> {
        public static Md5HashOrdinalIgnoreCaseComparer Instance { get; } = new();

        public bool Equals(Md5Hash x, Md5Hash y) {
            return x.Equals(y);
        }

        public int GetHashCode(Md5Hash obj) {
            return obj.GetHashCode();
        }

        public bool Equals(ReadOnlySpan<char> alternate, Md5Hash other) {
            if(Md5Hash.TryParse(alternate, out Md5Hash parsed)) {
                return parsed.Equals(other);
            }
            return false;
        }

        public int GetHashCode(ReadOnlySpan<char> alternate) {
            if(Md5Hash.TryParse(alternate, out Md5Hash parsed)) {
                return parsed.GetHashCode();
            }
            return 0;
        }

        public Md5Hash Create(ReadOnlySpan<char> alternate) {
            return Md5Hash.Parse(alternate);
        }
    }

    #endregion
}

/// <summary>
/// Extension methods for <see cref="Md5Hash"/>.
/// </summary>
public static partial class Md5HashExtensions {
    extension(Md5Hash) {
        /// <summary>
        /// Asynchronously computes the MD5 hash of a stream.
        /// Resets the stream position before and after computation if seekable.
        /// </summary>
        /// <param name="stream">The source stream to hash.</param>
        /// <returns>A task containing the computed <see cref="Md5Hash"/>.</returns>
        public static ValueTask<Md5Hash> ComputeAsync(Stream stream) {
            return ComputeAsync(stream, CancellationToken.None);
        }

        /// <summary>
        /// Asynchronously computes the MD5 hash of a stream.
        /// Resets the stream position before and after computation if seekable.
        /// </summary>
        /// <param name="stream">The source stream to hash.</param>
        /// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
        /// <returns>A task containing the computed <see cref="Md5Hash"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="stream"/> is null.</exception>
        public static async ValueTask<Md5Hash> ComputeAsync(Stream stream, CancellationToken cancellationToken) {
            Preca.ThrowIfNull(stream);

            if(stream.CanSeek) stream.Position = 0;

            byte[] buffer = ArrayPool<byte>.Shared.Rent(Md5Hash.HashSizeInBytes);

            try {
                await MD5.HashDataAsync(stream, buffer.AsMemory(0, Md5Hash.HashSizeInBytes), cancellationToken);

                if(stream.CanSeek) stream.Position = 0;

                return new Md5Hash(buffer.AsSpan(0, Md5Hash.HashSizeInBytes));
            }
            finally {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
    }
}