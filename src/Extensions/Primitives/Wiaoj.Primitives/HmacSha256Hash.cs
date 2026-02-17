using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json.Serialization;
using Wiaoj.Primitives.JsonConverters;

namespace Wiaoj.Primitives;
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
[JsonConverter(typeof(HmacSha256HashJsonConverter))]
[StructLayout(LayoutKind.Sequential)]
public unsafe struct HmacSha256Hash : IEquatable<HmacSha256Hash> {

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

    #endregion

    #region Data Access & Conversion

    /// <summary>
    /// Returns a read-only span over the 32 bytes of the hash.
    /// </summary>
    /// <returns>A <see cref="ReadOnlySpan{Byte}"/> containing the hash data.</returns>
    public ReadOnlySpan<byte> AsSpan() {
        return MemoryMarshal.CreateReadOnlySpan(ref Unsafe.AsRef<byte>(Unsafe.AsPointer(ref this._bytes[0])), HashSizeInBytes);
    }

    /// <summary>Converts the hash to its <see cref="HexString"/> representation.</summary>
    public HexString ToHexString() => HexString.FromBytes(AsSpan());

    /// <summary>Converts the hash to its <see cref="Base64String"/> representation.</summary>
    public Base64String ToBase64String() => Base64String.FromBytes(AsSpan());

    /// <summary>Converts the hash to its <see cref="Base64UrlString"/> representation.</summary>
    public Base64UrlString ToBase64UrlString() => Base64UrlString.FromBytes(AsSpan());

    /// <summary>Returns the hexadecimal string representation of the hash.</summary>
    /// <returns>An uppercase hexadecimal string.</returns>
    public override string ToString() => Convert.ToHexString(AsSpan());

    #endregion

    #region Parsing

    /// <summary>
    /// Creates a <see cref="HmacSha256Hash"/> from a valid <see cref="HexString"/>.
    /// </summary>
    /// <param name="hex">The hex-encoded string containing the hash.</param>
    /// <returns>A new <see cref="HmacSha256Hash"/> instance.</returns>
    /// <exception cref="FormatException">Thrown when the hex string does not represent 32 bytes.</exception>
    public static HmacSha256Hash FromHex(HexString hex) {
        Span<byte> buffer = stackalloc byte[HashSizeInBytes];
        if(hex.TryDecode(buffer, out int written) && written == HashSizeInBytes) {
            return new HmacSha256Hash(buffer);
        }
        throw new FormatException("Hex string length mismatch for HMAC-SHA256.");
    }

    /// <summary>
    /// Creates a <see cref="HmacSha256Hash"/> from a valid <see cref="Base64String"/>.
    /// </summary>
    /// <param name="b64">The base64-encoded string containing the hash.</param>
    /// <returns>A new <see cref="HmacSha256Hash"/> instance.</returns>
    /// <exception cref="FormatException">Thrown when the decoded base64 data is not 32 bytes.</exception>
    public static HmacSha256Hash FromBase64(Base64String b64) {
        Span<byte> buffer = stackalloc byte[HashSizeInBytes];
        if(b64.TryDecode(buffer, out int written) && written == HashSizeInBytes) {
            return new HmacSha256Hash(buffer);
        }
        throw new FormatException("Base64 string is not a valid 32-byte hash.");
    }

    /// <summary>
    /// Attempts to copy the hash bytes to the specified destination span.
    /// </summary>
    /// <param name="destination">The span to copy the bytes into.</param>
    /// <returns><see langword="true"/> if the copy was successful; otherwise, <see langword="false"/>.</returns>
    public bool TryCopyTo(Span<byte> destination) {
        if(destination.Length < HashSizeInBytes) return false;
        fixed(byte* p = _bytes) {
            new ReadOnlySpan<byte>(p, HashSizeInBytes).CopyTo(destination);
        }
        return true;
    }

    /// <summary>
    /// Implicitly converts a <see cref="HmacSha256Hash"/> to a <see cref="ReadOnlySpan{Byte}"/>.
    /// </summary>
    public static implicit operator ReadOnlySpan<byte>(HmacSha256Hash hash) => hash.AsSpan();

    #endregion

    #region Equality

    /// <summary>
    /// Determines whether two <see cref="HmacSha256Hash"/> instances are equal using a constant-time algorithm.
    /// </summary>
    public bool Equals(HmacSha256Hash other) => CryptographicOperations.FixedTimeEquals(AsSpan(), other.AsSpan());

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is HmacSha256Hash other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() {
        fixed(byte* p = _bytes) {
            var hash = new HashCode();
            hash.AddBytes(new ReadOnlySpan<byte>(p, HashSizeInBytes));
            return hash.ToHashCode();
        }
    }

    /// <summary>Compares two <see cref="HmacSha256Hash"/> instances for equality.</summary>
    public static bool operator ==(HmacSha256Hash left, HmacSha256Hash right) => left.Equals(right);

    /// <summary>Compares two <see cref="HmacSha256Hash"/> instances for inequality.</summary>
    public static bool operator !=(HmacSha256Hash left, HmacSha256Hash right) => !left.Equals(right);

    #endregion
}