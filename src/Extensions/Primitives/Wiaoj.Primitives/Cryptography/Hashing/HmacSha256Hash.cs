using System.Buffers;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using Wiaoj.Primitives.JsonConverters;

namespace Wiaoj.Primitives.Cryptography.Hashing;
/// <summary>
/// Represents a fixed-size, 32-byte HMAC-SHA256 hash.
/// </summary>
/// <remarks>
/// This struct utilizes a fixed-size buffer to ensure that the hash is stored inline, 
/// minimizing heap allocations and pressure on the Garbage Collector.
/// Equality comparisons are implemented using <see cref="CryptographicOperations.FixedTimeEquals"/> 
/// to prevent timing-based side-channel attacks.
/// </remarks>
[DebuggerDisplay("{ToString(),nq}")]
[StructLayout(LayoutKind.Sequential)]
[JsonConverter(typeof(HmacSha256HashJsonConverter))]
public unsafe struct HmacSha256Hash
    : IEquatable<HmacSha256Hash>,
    ISpanFormattable,
    IUtf8SpanFormattable,
    IEqualityOperators<HmacSha256Hash, HmacSha256Hash, bool> {

    /// <summary>The size of the HMAC-SHA256 hash in bytes (32 bytes).</summary>
    internal const int HashSizeInBytes = 32;

    private fixed byte _bytes[HashSizeInBytes];

    /// <summary>
    /// Initializes a new instance of the <see cref="HmacSha256Hash"/> struct from a span of bytes.
    /// </summary>
    /// <param name="source">A span containing exactly 32 bytes of hash data.</param>
    /// <exception cref="ArgumentException">Thrown when the source span length is not 32.</exception>
    internal HmacSha256Hash(ReadOnlySpan<byte> source) {
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

    /// <summary>Represents an empty (zero-filled) HMAC-SHA256 hash.</summary>
    public static readonly HmacSha256Hash Empty = default;

    #region Computation

    /// <summary>
    /// Computes the HMAC-SHA256 hash of the specified data using a secure secret key.
    /// </summary>
    /// <param name="key">The secret key stored in unmanaged memory.</param>
    /// <param name="data">The data to be hashed.</param>
    /// <returns>A <see cref="HmacSha256Hash"/> instance representing the result.</returns>
    [SkipLocalsInit]
    public static HmacSha256Hash Compute(Secret<byte> key, ReadOnlySpan<byte> data) {
        return key.Expose(data, static (dataState, keySpan) => Compute(keySpan, dataState));
    }

    /// <summary>
    /// Computes the HMAC-SHA256 hash where both the key and the data are sensitive secrets.
    /// </summary>
    /// <param name="key">The secret key.</param>
    /// <param name="data">The secret data.</param>
    /// <returns>A <see cref="HmacSha256Hash"/> instance.</returns>
    public static HmacSha256Hash Compute(Secret<byte> key, Secret<byte> data) {
        return data.Expose(key, static (keySecret, dataSpan) => Compute(keySecret, dataSpan));
    }

    /// <summary>
    /// Computes the HMAC-SHA256 hash of a string using a secure secret key and the specified encoding.
    /// </summary>
    public static HmacSha256Hash Compute(Secret<byte> key, string data, Encoding encoding) {
        Preca.ThrowIfNull(data);
        Preca.ThrowIfNull(encoding);

        return key.Expose((data, encoding), static (state, keySpan) => Compute(keySpan, state.data, state.encoding));
    }

    /// <summary>
    /// Computes the HMAC-SHA256 hash of a string using a secure secret key and UTF-8 encoding.
    /// </summary>
    public static HmacSha256Hash Compute(Secret<byte> key, string data) {
        return Compute(key, data, Encoding.UTF8);
    }
    /// <summary>
    /// Computes the HMAC-SHA256 hash using raw byte spans for both key and data.
    /// </summary>
    /// <param name="key">The cryptographic key.</param>
    /// <param name="data">The data to hash.</param>
    /// <returns>A <see cref="HmacSha256Hash"/> instance.</returns>
    [SkipLocalsInit]
    public static HmacSha256Hash Compute(ReadOnlySpan<byte> key, ReadOnlySpan<byte> data) {
        Span<byte> hashBuffer = stackalloc byte[HashSizeInBytes];
        HMACSHA256.HashData(key, data, hashBuffer);
        return new HmacSha256Hash(hashBuffer);
    }

    /// <summary>
    /// Computes the HMAC-SHA256 hash of a string using the specified key and encoding.
    /// </summary>
    /// <param name="key">The cryptographic key.</param>
    /// <param name="data">The string data to hash.</param>
    /// <param name="encoding">The character encoding used to convert the string to bytes.</param>
    /// <returns>A <see cref="HmacSha256Hash"/> instance.</returns>
    public static HmacSha256Hash Compute(ReadOnlySpan<byte> key, string data, Encoding encoding) {
        Preca.ThrowIfNull(data);
        Preca.ThrowIfNull(encoding);
        return Compute(key, encoding.GetBytes(data));
    }

    /// <summary>
    /// Computes the HMAC-SHA256 hash of a string using the specified key and UTF-8 encoding.
    /// </summary>
    /// <param name="key">The cryptographic key.</param>
    /// <param name="data">The string data to hash.</param>
    /// <returns>A <see cref="HmacSha256Hash"/> instance.</returns>
    public static HmacSha256Hash Compute(ReadOnlySpan<byte> key, string data) {
        return Compute(key, data, Encoding.UTF8);
    }

    #endregion

    #region Data Access & Conversion

    /// <summary>
    /// Returns a read-only span over the 32 bytes of the hash.
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

    // IFormattable — "x" = lowercase hex, default = uppercase
    string IFormattable.ToString(string? format, IFormatProvider? formatProvider) {
        return format is "x" ? Convert.ToHexStringLower(AsSpan()) : Convert.ToHexString(AsSpan());
    }

    // ISpanFormattable
    bool ISpanFormattable.TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider) {
        int required = HashSizeInBytes * 2;
        if(destination.Length < required) { charsWritten = 0; return false; }
        return format.Equals("x", StringComparison.Ordinal)
            ? Convert.TryToHexStringLower(AsSpan(), destination, out charsWritten)
            : Convert.TryToHexString(AsSpan(), destination, out charsWritten);
    }

    // IUtf8SpanFormattable
    bool IUtf8SpanFormattable.TryFormat(Span<byte> utf8Destination, out int bytesWritten, ReadOnlySpan<char> format, IFormatProvider? provider) {
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

    #region Parsing

    /// <summary>
    /// Creates a <see cref="HmacSha256Hash"/> from a valid <see cref="HexString"/>.
    /// </summary>
    /// <param name="hex">The hex-encoded string containing the hash.</param>
    /// <returns>A new <see cref="HmacSha256Hash"/> instance.</returns>
    /// <exception cref="FormatException">Thrown when the hex string does not represent 32 bytes.</exception>
    public static HmacSha256Hash From(HexString hex) {
        Span<byte> buffer = stackalloc byte[HashSizeInBytes];
        if(hex.TryDecode(buffer, out int written) && written == HashSizeInBytes) {
            return new HmacSha256Hash(buffer);
        }
        throw new FormatException("Hex string length mismatch for HMAC-SHA256.");
    }

    /// <summary>
    /// Creates a <see cref="HmacSha256Hash"/> from a valid <see cref="Base64String"/>.
    /// </summary>
    /// <param name="base64">The base64-encoded string containing the hash.</param>
    /// <returns>A new <see cref="HmacSha256Hash"/> instance.</returns>
    /// <exception cref="FormatException">Thrown when the decoded base64 data is not 32 bytes.</exception>
    public static HmacSha256Hash From(Base64String base64) {
        Span<byte> buffer = stackalloc byte[HashSizeInBytes];
        if(base64.TryDecode(buffer, out int written) && written == HashSizeInBytes) {
            return new HmacSha256Hash(buffer);
        }
        throw new FormatException("Base64 string is not a valid 32-byte hash.");
    }

    /// <summary>
    /// Creates a hash instance from a valid <see cref="Base32String"/>.
    /// </summary>
    public static HmacSha256Hash From(Base32String base32) {
        Span<byte> buffer = stackalloc byte[HashSizeInBytes];
        if(base32.TryDecode(buffer, out int written) && written == HashSizeInBytes) {
            return new(buffer);
        }
        throw new FormatException($"Base32 string length mismatch for {HashSizeInBytes}-byte hash.");
    }

    /// <summary>
    /// Creates a hash instance from a valid <see cref="Base62String"/>.
    /// </summary>
    public static HmacSha256Hash From(Base62String base62) {
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
    /// Tries to create a <see cref="HmacSha256Hash"/> from a <see cref="HexString"/>.
    /// </summary>
    /// <param name="hex">The hex-encoded string to parse.</param>
    /// <param name="result">
    ///     When this method returns <see langword="true"/>, contains the parsed hash;
    ///     otherwise, contains the default value.
    /// </param>
    /// <returns>
    ///     <see langword="true"/> if <paramref name="hex"/> represents exactly 32 bytes;
    ///     otherwise, <see langword="false"/>.
    /// </returns>
    public static bool TryParse(HexString hex, out HmacSha256Hash result) {
        if(hex.GetDecodedLength() != HashSizeInBytes) {
            result = default;
            return false;
        }
        Span<byte> buffer = stackalloc byte[HashSizeInBytes];
        hex.TryDecode(buffer, out _);
        result = new HmacSha256Hash(buffer);
        return true;
    }

    /// <summary>
    /// Creates a <see cref="HmacSha256Hash"/> from a raw byte array.
    /// </summary>
    /// <param name="bytes">A byte array of exactly 32 bytes.</param>
    /// <returns>A new <see cref="HmacSha256Hash"/> instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="bytes"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the array length is not 32.</exception>
    public static HmacSha256Hash FromBytes(byte[] bytes) {
        Preca.ThrowIfNull(bytes);
        return new HmacSha256Hash(bytes.AsSpan());
    }

    /// <summary>
    /// Creates a <see cref="HmacSha256Hash"/> from a read-only span of bytes.
    /// </summary>
    /// <param name="source">A span of exactly 32 bytes.</param>
    /// <returns>A new <see cref="HmacSha256Hash"/> instance.</returns>
    /// <exception cref="ArgumentException">Thrown when the span length is not 32.</exception>
    public static HmacSha256Hash FromBytes(ReadOnlySpan<byte> source) {
        return new HmacSha256Hash(source);
    }

    /// <summary>
    /// Attempts to create a <see cref="HmacSha256Hash"/> from a read-only span of bytes.
    /// </summary>
    /// <param name="source">A span of bytes.</param>
    /// <param name="result">
    ///     When this method returns <see langword="true"/>, contains the parsed hash;
    ///     otherwise, contains the default value.
    /// </param>
    /// <returns>
    ///     <see langword="true"/> if <paramref name="source"/> contains exactly 32 bytes;
    ///     otherwise, <see langword="false"/>.
    /// </returns>
    public static bool TryFromBytes(ReadOnlySpan<byte> source, out HmacSha256Hash result) {
        if(source.Length != HashSizeInBytes) {
            result = default;
            return false;
        }
        result = new HmacSha256Hash(source);
        return true;
    }

    /// <summary>
    /// Copies the hash bytes to a destination span.
    /// </summary>
    /// <param name="destination">The span to copy the bytes into. Must be at least 32 bytes.</param>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="destination"/> is shorter than 32 bytes.
    /// </exception>
    public void CopyTo(Span<byte> destination) {
        if(destination.Length < HashSizeInBytes)
            throw new ArgumentException("Destination span must be at least 32 bytes long.", nameof(destination));
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
    /// Implicitly converts a <see cref="HmacSha256Hash"/> to a <see cref="ReadOnlySpan{Byte}"/>.
    /// </summary>
    public static implicit operator ReadOnlySpan<byte>(HmacSha256Hash hash) {
        return hash.AsSpan();
    }

    #endregion

    #region Equality

    /// <summary>
    /// Determines whether two <see cref="HmacSha256Hash"/> instances are equal using a constant-time algorithm.
    /// </summary>
    public bool Equals(HmacSha256Hash other) {
        return CryptographicOperations.FixedTimeEquals(AsSpan(), other.AsSpan());
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) {
        return obj is HmacSha256Hash other && Equals(other);
    }

    /// <inheritdoc/>
    public override int GetHashCode() {
        fixed(byte* p = this._bytes) {
            HashCode hash = new();
            hash.AddBytes(new ReadOnlySpan<byte>(p, HashSizeInBytes));
            return hash.ToHashCode();
        }
    }

    /// <inheritdoc cref="IEqualityOperators{TSelf, TOther, TResult}.op_Equality(TSelf, TOther)" />
    public static bool operator ==(HmacSha256Hash left, HmacSha256Hash right) {
        return left.Equals(right);
    }

    /// <inheritdoc cref="IEqualityOperators{TSelf, TOther, TResult}.op_Inequality(TSelf, TOther)" />
    public static bool operator !=(HmacSha256Hash left, HmacSha256Hash right) {
        return !left.Equals(right);
    }
    #endregion
}

/// <summary>
/// Extension methods for <see cref="HmacSha256Hash"/>.
/// </summary>
public static partial class HmacSha256HashExtensions {
    extension(HmacSha256Hash) {
        /// <summary>
        /// Asynchronously computes the HMAC-SHA256 hash of a stream.
        /// Ensures the stream is reset before and after computation, and manages memory securely.
        /// </summary>
        public static async ValueTask<HmacSha256Hash> ComputeAsync(
            Stream stream,
            Secret<byte> key,
            CancellationToken cancellationToken = default) {
            Preca.ThrowIfNull(stream);
            Preca.ThrowIfNull(key);

            if(stream.CanSeek) stream.Position = 0;

            int keyLength = key.Expose(k => k.Length);
            byte[] keyBuffer = ArrayPool<byte>.Shared.Rent(keyLength);

            byte[] hashBuffer = ArrayPool<byte>.Shared.Rent(HmacSha256Hash.HashSizeInBytes);

            try {
                key.Expose(k => k.CopyTo(keyBuffer));

                await HMACSHA256.HashDataAsync(
                    new ReadOnlyMemory<byte>(keyBuffer, 0, keyLength),
                    stream,
                    hashBuffer.AsMemory(0, HmacSha256Hash.HashSizeInBytes),
                    cancellationToken);

                if(stream.CanSeek) stream.Position = 0;

                return new HmacSha256Hash(hashBuffer.AsSpan(0, HmacSha256Hash.HashSizeInBytes));
            }
            finally {
                CryptographicOperations.ZeroMemory(keyBuffer.AsSpan(0, keyLength));
                ArrayPool<byte>.Shared.Return(keyBuffer);
                ArrayPool<byte>.Shared.Return(hashBuffer);
            }
        }
    }
}