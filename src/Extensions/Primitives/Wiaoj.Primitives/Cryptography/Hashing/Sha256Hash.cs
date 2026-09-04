using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using Wiaoj.Primitives;
using Wiaoj.Primitives.Buffers;

namespace Wiaoj.Primitives.Cryptography.Hashing;
/// <summary>
/// Represents a 32-byte SHA256 hash. This struct guarantees the correct size
/// and provides high-performance, allocation-free operations for computing and comparing hashes.
/// </summary>
[DebuggerDisplay("{ToString(),nq}")]
[StructLayout(LayoutKind.Sequential)]
[JsonConverter(typeof(JsonConverters.Sha256HashJsonConverter))]
public unsafe struct Sha256Hash
    : IFixedBinaryValue<Sha256Hash>,
    IEquatable<Sha256Hash>,
    IComparable<Sha256Hash>,
    IComparable,
    IParsable<Sha256Hash>,
    ISpanParsable<Sha256Hash>,
    IUtf8SpanParsable<Sha256Hash>,
    ISpanFormattable,
    IUtf8SpanFormattable,
    IFormattable,
    IEqualityOperators<Sha256Hash, Sha256Hash, bool>,
    IComparisonOperators<Sha256Hash, Sha256Hash, bool> {
    internal const int HashSizeInBytes = 32;
    private fixed byte _bytes[HashSizeInBytes];

    /// <inheritdoc/>
    public static int SizeInBytes => HashSizeInBytes;

    internal Sha256Hash(ReadOnlySpan<byte> source) {
        Preca.ThrowIf(
            source.Length != HashSizeInBytes,
            () => new ArgumentException("Source span must be exactly 32 bytes long.", nameof(source)));

        fixed(byte* p = this._bytes) {
            source.CopyTo(new Span<byte>(p, HashSizeInBytes));
        }
    }

    #region Factory Methods
    /// <summary>
    /// Represents a SHA256 hash consisting of all zero bytes.
    /// Equivalent to a 32-byte array filled with 0x00.
    /// </summary>
    public static readonly Sha256Hash Empty = default;

    /// <summary>
    /// Creates a Sha256Hash instance from a 32-byte span.
    /// This is the primary public entry point for creating a hash from existing bytes.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown if source is not exactly 32 bytes long.</exception>
    public static Sha256Hash FromBytes(ReadOnlySpan<byte> source) {
        return new Sha256Hash(source);
    }

    /// <summary>
    /// Creates a Sha256Hash instance from a hexadecimal string representation.
    /// </summary>
    /// <exception cref="FormatException">The input is not a valid 64-character hexadecimal string.</exception>
    public static Sha256Hash From(HexString hex) {
        if(hex.GetDecodedLength() != HashSizeInBytes) {
            throw new FormatException("Source HexString must represent exactly 32 bytes (64 hex characters).");
        }

        Span<byte> buffer = stackalloc byte[HashSizeInBytes];
        hex.TryDecode(buffer, out _);
        return new Sha256Hash(buffer);
    }

    /// <summary>
    /// Creates a Sha256Hash instance from a Base64String.
    /// </summary>
    public static Sha256Hash From(Base64String base64) {
        if(base64.GetDecodedLength() != HashSizeInBytes) {
            throw new FormatException("Source Base64String must represent exactly 32 bytes.");
        }

        Span<byte> buffer = stackalloc byte[HashSizeInBytes];
        if(!base64.TryDecode(buffer, out int written) || written != HashSizeInBytes) {
            throw new FormatException("Failed to decode Base64 into Hash.");
        }
        return new Sha256Hash(buffer);
    }

    /// <summary>
    /// Creates a hash instance from a valid <see cref="Base32String"/>.
    /// </summary>
    public static Sha256Hash From(Base32String base32) {
        Span<byte> buffer = stackalloc byte[HashSizeInBytes];
        if(base32.TryDecode(buffer, out int written) && written == HashSizeInBytes) {
            return new(buffer);
        }
        throw new FormatException($"Base32 string length mismatch for {HashSizeInBytes}-byte hash.");
    }

    /// <summary>
    /// Creates a hash instance from a valid <see cref="Base62String"/>.
    /// </summary>
    public static Sha256Hash From(Base62String base62) {
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
    /// Parses a hexadecimal string into a Sha256Hash.
    /// </summary>
    public static Sha256Hash Parse(string s) {
        ArgumentNullException.ThrowIfNull(s);
        if(!TryParse(s.AsSpan(), out Sha256Hash result)) {
            throw new FormatException($"Input string must represent exactly {HashSizeInBytes} bytes (64 hex characters).");
        }
        return result;
    }

    /// <summary>
    /// Parses a span of characters into a Sha256Hash. (Zero-allocation)
    /// </summary>
    public static Sha256Hash Parse(ReadOnlySpan<char> s) {
        if(!TryParse(s, out Sha256Hash result)) {
            throw new FormatException($"Input span must represent exactly {HashSizeInBytes} bytes (64 hex characters).");
        }
        return result;
    }

    /// <summary>
    /// Parses a UTF-8 encoded hexadecimal byte span into a <see cref="Sha256Hash"/>.
    /// </summary>
    public static Sha256Hash Parse(ReadOnlySpan<byte> utf8Text) {
        if(!TryParse(utf8Text, out Sha256Hash result)) {
            throw new FormatException("Invalid UTF-8 hexadecimal sequence for Sha256Hash.");
        }
        return result;
    }

    /// <summary>
    /// Tries to parse a hexadecimal string into a Sha256Hash.
    /// </summary>
    public static bool TryParse([NotNullWhen(true)] string? s, out Sha256Hash result) {
        if(s is null) { result = default; return false; }
        return TryParse(s.AsSpan(), out result);
    }

    /// <summary>
    /// Tries to parse a span of characters into a Sha256Hash.
    /// </summary>
    public static bool TryParse(ReadOnlySpan<char> s, out Sha256Hash result) {
        if(HexString.TryParse(s, out HexString hex)) {
            return TryParse(hex, out result);
        }
        result = default;
        return false;
    }

    /// <summary>
    /// Tries to parse a UTF-8 encoded byte span into a <see cref="Sha256Hash"/>.
    /// </summary>
    public static bool TryParse(ReadOnlySpan<byte> utf8Text, out Sha256Hash result) {
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
    /// Tries to create a Sha256Hash instance from a hexadecimal string representation.
    /// </summary>
    public static bool TryParse(HexString hex, out Sha256Hash result) {
        if(hex.GetDecodedLength() != HashSizeInBytes) {
            result = default;
            return false;
        }

        Span<byte> buffer = stackalloc byte[HashSizeInBytes];
        hex.TryDecode(buffer, out _);
        result = new Sha256Hash(buffer);
        return true;
    }

    #endregion

    #region Explicit Interface Implementations (IParsable, ISpanParsable, IUtf8SpanParsable)

    static Sha256Hash IParsable<Sha256Hash>.Parse(string s, IFormatProvider? provider) => Parse(s);
    static bool IParsable<Sha256Hash>.TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out Sha256Hash result) => TryParse(s, out result);
    static Sha256Hash ISpanParsable<Sha256Hash>.Parse(ReadOnlySpan<char> s, IFormatProvider? provider) => Parse(s);
    static bool ISpanParsable<Sha256Hash>.TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out Sha256Hash result) => TryParse(s, out result);
    static Sha256Hash IUtf8SpanParsable<Sha256Hash>.Parse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider) => Parse(utf8Text);
    static bool IUtf8SpanParsable<Sha256Hash>.TryParse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider, out Sha256Hash result) => TryParse(utf8Text, out result);

    #endregion

    #region High-Performance Computation
    /// <summary>
    /// Computes the SHA256 hash for the contents of a <see cref="Secret{Byte}"/>.
    /// Since the secret is already binary, no encoding is needed.
    /// </summary>
    public static Sha256Hash Compute(Secret<byte> secret) {
        Preca.ThrowIfNull(secret);
        return secret.Expose(span => Compute(span));
    }

    /// <summary>
    /// Computes the SHA256 hash of a span of bytes. This method is allocation-free.
    /// </summary>
    [SkipLocalsInit]
    public static Sha256Hash Compute(ReadOnlySpan<byte> data) {
        Span<byte> hashBuffer = stackalloc byte[HashSizeInBytes];
        SHA256.HashData(data, hashBuffer);
        return new Sha256Hash(hashBuffer);
    }

    /// <summary>
    /// Computes the SHA256 hash of a character span using UTF-8 encoding.
    /// </summary>
    /// <param name="data">The character span to hash.</param>
    /// <returns>The computed <see cref="Sha256Hash"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Sha256Hash Compute(ReadOnlySpan<char> data) {
        return Compute(data, Encoding.UTF8);
    }

    /// <summary>
    /// Computes the SHA256 hash of a character span using the specified encoding.
    /// This method is allocation-free for inputs up to 1024 bytes after encoding.
    /// </summary>
    /// <param name="data">The character span to hash.</param>
    /// <param name="encoding">The character encoding to use when converting the characters to bytes.</param>
    /// <returns>The computed <see cref="Sha256Hash"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [SkipLocalsInit]
    public static Sha256Hash Compute(ReadOnlySpan<char> data, Encoding encoding) {
        Preca.ThrowIfNull(encoding);
        int maxByteCount = encoding.GetMaxByteCount(data.Length);
        using ValueBuffer<byte> buffer = new(maxByteCount, stackalloc byte[1024]);
        int bytesWritten = encoding.GetBytes(data, buffer.Span);
        return Compute(buffer.Span[..bytesWritten]);
    }

    /// <summary>
    /// Computes the SHA256 hash for the contents of a <see cref="Secret{T}"/> of <see cref="char"/> using the specified encoding.
    /// This method avoids allocating the secret on the managed heap, performing the entire operation securely.
    /// </summary>
    /// <param name="secret">The secret containing the character data to hash.</param>
    /// <param name="encoding">The character encoding to use when converting the secret to bytes for hashing.</param>
    /// <returns>The computed <see cref="Sha256Hash"/>.</returns>
    public static Sha256Hash Compute(Secret<char> secret, Encoding encoding) {
        Preca.ThrowIfNull(secret);
        Preca.ThrowIfNull(encoding);

        // secret.Expose provides secure access to the underlying ReadOnlySpan<char>.
        return secret.Expose(chars => {
            int maxByteCount = encoding.GetMaxByteCount(chars.Length);
            using ValueBuffer<byte> buffer = new(maxByteCount, stackalloc byte[1024]);
            int bytesWritten = encoding.GetBytes(chars, buffer.Span);
            return Compute(buffer.Span[..bytesWritten]);
        });
    }

    /// <summary>
    /// Computes the SHA256 hash for the contents of a <see cref="Secret{T}"/> of <see cref="char"/> using the default UTF-8 encoding.
    /// </summary>
    /// <param name="secret">The secret containing the character data to hash.</param>
    /// <returns>The computed <see cref="Sha256Hash"/>.</returns>
    public static Sha256Hash Compute(Secret<char> secret) {
        return Compute(secret, Encoding.UTF8);
    }

    /// <summary>
    /// Computes the SHA256 hash of a string using the specified encoding.
    /// </summary>
    /// <param name="text">The string to hash.</param>
    /// <param name="encoding">The character encoding used to convert the string to bytes.</param>
    /// <returns>The computed <see cref="Sha256Hash"/>.</returns>
    public static Sha256Hash Compute(string text, Encoding encoding) {
        Preca.ThrowIfNull(text);
        return Compute(text.AsSpan(), encoding);
    }

    /// <summary>
    /// Computes the SHA256 hash of a string using UTF-8 encoding by default.
    /// </summary>
    /// <param name="text">The string to hash.</param>
    /// <returns>The computed <see cref="Sha256Hash"/>.</returns>
    public static Sha256Hash Compute(string text) {
        return Compute(text, Encoding.UTF8);
    }

    #endregion

    #region Data Access & Conversion

    /// <summary>
    /// Provides safe, scoped access to the hash bytes as a <see cref="ReadOnlySpan{Byte}"/>.
    /// </summary>
    public void Expose(Action<ReadOnlySpan<byte>> action) {
        fixed(byte* p = this._bytes) {
            action(new ReadOnlySpan<byte>(p, HashSizeInBytes));
        }
    }

    /// <summary>
    /// Provides safe, scoped access to the hash bytes and returns a result.
    /// </summary>
    public TResult Expose<TResult>(Func<ReadOnlySpan<byte>, TResult> func) {
        fixed(byte* p = this._bytes) {
            return func(new ReadOnlySpan<byte>(p, HashSizeInBytes));
        }
    }

    /// <summary>
    /// Copies the hash bytes to a destination span.
    /// </summary>
    public void CopyTo(Span<byte> destination) {
        if(destination.Length < HashSizeInBytes) {
            throw new ArgumentException("Destination span must be at least 32 bytes long.", nameof(destination));
        }
        AsSpan().CopyTo(destination);
    }

    /// <summary>
    /// Attempts to copy the hash bytes to the specified destination span.
    /// </summary>
    /// <param name="destination">The span to copy the bytes into.</param>
    /// <returns><see langword="true"/> if the copy was successful; otherwise, <see langword="false"/>.</returns>
    public bool TryCopyTo(Span<byte> destination) {
        if(destination.Length < HashSizeInBytes) return false;
        AsSpan().CopyTo(destination);
        return true;
    }

    /// <summary>
    /// Returns a <see cref="ReadOnlySpan{Byte}"/> view of the hash bytes.
    /// </summary>
    public ReadOnlySpan<byte> AsSpan() {
        // This is the most efficient way to get a span from a fixed buffer inside a struct.
        return new ReadOnlySpan<byte>(Unsafe.AsPointer(ref Unsafe.AsRef(in this._bytes[0])), HashSizeInBytes);
    }

    /// <summary>
    /// Encodes the hash bytes into a type-safe <see cref="HexString"/>.
    /// </summary>
    /// <returns>A <see cref="HexString"/> representation of the SHA256 hash.</returns>
    public HexString ToHexString() {
        return HexString.FromBytes(AsSpan());
    }

    /// <summary>
    /// Encodes the hash bytes into a lowercase <see cref="HexString"/>.
    /// This avoids the extra allocation caused by calling <c>ToHexString().ToLower()</c>.
    /// </summary>
    /// <returns>A lowercase <see cref="HexString"/> representation of the SHA256 hash.</returns>
    public HexString ToHexStringLower() {
        return HexString.FromBytesLower(AsSpan());
    }

    /// <summary>
    /// Encodes the hash bytes into a type-safe <see cref="Base64String"/>.
    /// </summary>
    /// <returns>A <see cref="Base64String"/> representation of the SHA256 hash.</returns>
    public Base64String ToBase64String() {
        return Base64String.FromBytes(AsSpan());
    }

    /// <summary>
    /// Encodes the hash bytes into a type-safe <see cref="Base64UrlString"/>.
    /// </summary>
    /// <returns>A <see cref="Base64UrlString"/> representation of the SHA256 hash.</returns>
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

    /// <summary>
    /// Returns the hash as an uppercase hexadecimal string.
    /// </summary>
    public override string ToString() => Convert.ToHexString(AsSpan());

    /// <summary>
    /// Returns the string representation of the hash using the specified format.
    /// </summary>
    public string ToString(string? format) => ToString(format, null);

    /// <summary>
    /// Returns the string representation of the hash using the specified format and provider.
    /// </summary>
    public string ToString(string? format, IFormatProvider? formatProvider) {
        return format is "x" ? Convert.ToHexStringLower(AsSpan()) : Convert.ToHexString(AsSpan());
    }

    /// <summary>
    /// Attempts to format the hash as an uppercase hexadecimal string into the destination character span.
    /// </summary>
    public bool TryFormat(Span<char> destination, out int charsWritten) => TryFormat(destination, out charsWritten, default, null);

    /// <summary>
    /// Attempts to format the hash into the destination character span using the specified format.
    /// </summary>
    public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format) => TryFormat(destination, out charsWritten, format, null);

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
    public bool TryFormat(Span<byte> utf8Destination, out int bytesWritten) => TryFormat(utf8Destination, out bytesWritten, default, null);

    /// <summary>
    /// Attempts to format the hash into the destination UTF-8 byte span using the specified format.
    /// </summary>
    public bool TryFormat(Span<byte> utf8Destination, out int bytesWritten, ReadOnlySpan<char> format) => TryFormat(utf8Destination, out bytesWritten, format, null);

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
    /// Compares two hashes for equality in a way that is resistant to timing attacks.
    /// </summary>
    public bool Equals(Sha256Hash other) => FixedBinaryValueOps.Equals(this, other);

    /// <inheritdoc/>
    public override bool Equals(object? obj) {
        return obj is Sha256Hash other && Equals(other);
    }

    /// <summary>
    /// Gets the hash code for this instance. Note: This is not a cryptographic hash.
    /// It is suitable for use in collections like dictionaries and hash sets.
    /// </summary>
    public override int GetHashCode() => FixedBinaryValueOps.GetHashCode(this);

    /// <inheritdoc/>
    public int CompareTo(Sha256Hash other) => FixedBinaryValueOps.CompareTo(this, other);

    /// <inheritdoc/>
    public int CompareTo(object? obj) => FixedBinaryValueOps.CompareToObject(this, obj);

    /// <inheritdoc cref="IComparisonOperators{TSelf, TOther, TResult}.op_GreaterThan(TSelf, TOther)" />
    public static bool operator >(Sha256Hash left, Sha256Hash right) => left.CompareTo(right) > 0;

    /// <inheritdoc cref="IComparisonOperators{TSelf, TOther, TResult}.op_LessThan(TSelf, TOther)" />
    public static bool operator <(Sha256Hash left, Sha256Hash right) => left.CompareTo(right) < 0;

    /// <inheritdoc cref="IComparisonOperators{TSelf, TOther, TResult}.op_GreaterThanOrEqual(TSelf, TOther)" />
    public static bool operator >=(Sha256Hash left, Sha256Hash right) => left.CompareTo(right) >= 0;

    /// <inheritdoc cref="IComparisonOperators{TSelf, TOther, TResult}.op_LessThanOrEqual(TSelf, TOther)" />
    public static bool operator <=(Sha256Hash left, Sha256Hash right) => left.CompareTo(right) <= 0;

    /// <inheritdoc cref="IEqualityOperators{TSelf, TOther, TResult}.op_Equality(TSelf, TOther)" />
    public static bool operator ==(Sha256Hash left, Sha256Hash right) => left.Equals(right);

    /// <inheritdoc cref="IEqualityOperators{TSelf, TOther, TResult}.op_Inequality(TSelf, TOther)" />
    public static bool operator !=(Sha256Hash left, Sha256Hash right) => !left.Equals(right);

    #endregion

    #region Alternate Comparers (.NET 10 Alternate Lookup)

    /// <summary>
    /// Gets an equality comparer that performs ordinal comparisons on <see cref="Sha256Hash"/>
    /// and supports zero-allocation alternate lookups using <see cref="ReadOnlySpan{Char}"/>.
    /// </summary>
    public static IEqualityComparer<Sha256Hash> OrdinalComparer => Sha256HashOrdinalComparer.Instance;

    /// <summary>
    /// Gets an equality comparer that performs case-insensitive ordinal comparisons on <see cref="Sha256Hash"/>
    /// and supports zero-allocation alternate lookups using <see cref="ReadOnlySpan{Char}"/>.
    /// </summary>
    public static IEqualityComparer<Sha256Hash> OrdinalIgnoreCaseComparer => Sha256HashOrdinalIgnoreCaseComparer.Instance;

    private sealed class Sha256HashOrdinalComparer : IEqualityComparer<Sha256Hash>, IAlternateEqualityComparer<ReadOnlySpan<char>, Sha256Hash> {
        public static Sha256HashOrdinalComparer Instance { get; } = new();

        public bool Equals(Sha256Hash x, Sha256Hash y) => x.Equals(y);

        public int GetHashCode(Sha256Hash obj) => obj.GetHashCode();

        public bool Equals(ReadOnlySpan<char> alternate, Sha256Hash other) {
            if(Sha256Hash.TryParse(alternate, out Sha256Hash parsed)) {
                return parsed.Equals(other);
            }
            return false;
        }

        public int GetHashCode(ReadOnlySpan<char> alternate) {
            if(Sha256Hash.TryParse(alternate, out Sha256Hash parsed)) {
                return parsed.GetHashCode();
            }
            return 0;
        }

        public Sha256Hash Create(ReadOnlySpan<char> alternate) => Sha256Hash.Parse(alternate);
    }

    private sealed class Sha256HashOrdinalIgnoreCaseComparer : IEqualityComparer<Sha256Hash>, IAlternateEqualityComparer<ReadOnlySpan<char>, Sha256Hash> {
        public static Sha256HashOrdinalIgnoreCaseComparer Instance { get; } = new();

        public bool Equals(Sha256Hash x, Sha256Hash y) => x.Equals(y);

        public int GetHashCode(Sha256Hash obj) => obj.GetHashCode();

        public bool Equals(ReadOnlySpan<char> alternate, Sha256Hash other) {
            if(Sha256Hash.TryParse(alternate, out Sha256Hash parsed)) {
                return parsed.Equals(other);
            }
            return false;
        }

        public int GetHashCode(ReadOnlySpan<char> alternate) {
            if(Sha256Hash.TryParse(alternate, out Sha256Hash parsed)) {
                return parsed.GetHashCode();
            }
            return 0;
        }

        public Sha256Hash Create(ReadOnlySpan<char> alternate) => Sha256Hash.Parse(alternate);
    }

    #endregion
}

/// <summary>
/// Extension methods for <see cref="Sha256Hash"/>.
/// </summary>
public static partial class Sha256HashExtensions {
    extension(Sha256Hash) {
        /// <summary>
        /// Asynchronously computes the SHA256 hash of a stream without loading it all into memory.
        /// </summary>
        /// <param name="stream">The source stream to hash.</param>
        /// <returns>A task containing the computed <see cref="Sha256Hash"/>.</returns>
        public static ValueTask<Sha256Hash> ComputeAsync(Stream stream) => ComputeAsync(stream, CancellationToken.None);

        /// <summary>
        /// Asynchronously computes the SHA256 hash of a stream without loading it all into memory.
        /// </summary>
        /// <param name="stream">The source stream to hash.</param>
        /// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
        /// <returns>A task containing the computed <see cref="Sha256Hash"/>.</returns>
        public static async ValueTask<Sha256Hash> ComputeAsync(Stream stream, CancellationToken cancellationToken) {
            Preca.ThrowIfNull(stream);

            if(stream.CanSeek) stream.Position = 0;

            byte[] buffer = ArrayPool<byte>.Shared.Rent(Sha256Hash.HashSizeInBytes);
            try {
                await SHA256.HashDataAsync(stream, buffer.AsMemory(0, Sha256Hash.HashSizeInBytes), cancellationToken);

                return new Sha256Hash(buffer.AsSpan(0, Sha256Hash.HashSizeInBytes));
            }
            finally {
                ArrayPool<byte>.Shared.Return(buffer);

                if(stream.CanSeek)
                    stream.Position = 0;
            }
        }
    }
}