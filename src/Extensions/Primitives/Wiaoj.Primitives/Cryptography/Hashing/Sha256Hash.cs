using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;

namespace Wiaoj.Primitives.Cryptography.Hashing;
/// <summary>
/// Represents a 32-byte SHA256 hash. This struct guarantees the correct size
/// and provides high-performance, allocation-free operations for computing and comparing hashes.
/// </summary>
[DebuggerDisplay("{ToString(),nq}")]
[StructLayout(LayoutKind.Sequential)]
[JsonConverter(typeof(JsonConverters.Sha256HashJsonConverter))]
public unsafe struct Sha256Hash : IEquatable<Sha256Hash>, ISpanFormattable, IUtf8SpanFormattable {
    internal const int HashSizeInBytes = 32;
    private fixed byte _bytes[HashSizeInBytes];

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
    public static readonly Sha256Hash Empty = new(stackalloc byte[HashSizeInBytes]);

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
            // We avoid creating a byte[] on the heap by using stackalloc.
            // This is both more secure and more performant.
            int maxByteCount = encoding.GetMaxByteCount(chars.Length);
            Span<byte> bytesOnStack = stackalloc byte[maxByteCount];
            int bytesWritten = encoding.GetBytes(chars, bytesOnStack);

            // Compute the hash from the byte span on the stack.
            return Compute(bytesOnStack[..bytesWritten]);
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
    public static Sha256Hash Compute(string text, Encoding encoding) {
        Preca.ThrowIfNull(text);
        Preca.ThrowIfNull(encoding);
        return Compute(encoding.GetBytes(text));
    }

    /// <summary>
    /// Computes the SHA256 hash of a string using UTF-8 encoding by default.
    /// </summary>
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
    /// Encodes the hash bytes into a type-safe <see cref="Base64String"/>.
    /// </summary>
    /// <returns>A <see cref="Base64String"/> representation of the SHA256 hash.</returns>
    public Base64String ToBase64String() {
        return Base64String.FromBytes(AsSpan());
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
    /// Returns the hash as a hexadecimal string.
    /// </summary>
    public override string ToString() {
        return Convert.ToHexString(AsSpan());
    }

    // IFormattable — format "x" = lowercase hex, default = uppercase
    string IFormattable.ToString(string? format, IFormatProvider? formatProvider) =>
        format is "x" ? Convert.ToHexStringLower(AsSpan()) : Convert.ToHexString(AsSpan());

    // ISpanFormattable — zero-alloc hex write
    bool ISpanFormattable.TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider) {
        int required = HashSizeInBytes * 2;
        if(destination.Length < required) { charsWritten = 0; return false; }
        bool lower = format.Equals("x", StringComparison.Ordinal);
        bool ok = lower
            ? Convert.TryToHexStringLower(AsSpan(), destination, out charsWritten)
            : Convert.TryToHexString(AsSpan(), destination, out charsWritten);
        return ok;
    }

    // IUtf8SpanFormattable — write hex as UTF-8 bytes (ASCII)
    bool IUtf8SpanFormattable.TryFormat(Span<byte> utf8Destination, out int bytesWritten, ReadOnlySpan<char> format, IFormatProvider? provider) {
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

    #region Equality

    /// <summary>
    /// Compares two hashes for equality in a way that is resistant to timing attacks.
    /// </summary>
    public bool Equals(Sha256Hash other) {
        return CryptographicOperations.FixedTimeEquals(AsSpan(), other.AsSpan());
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) {
        return obj is Sha256Hash other && Equals(other);
    }

    /// <summary>
    /// Gets the hash code for this instance. Note: This is not a cryptographic hash.
    /// It is suitable for use in collections like dictionaries and hash sets.
    /// </summary>
    public override int GetHashCode() {
        HashCode hash = new();
        hash.AddBytes(AsSpan());
        return hash.ToHashCode();
    }

    public static bool operator ==(Sha256Hash left, Sha256Hash right) {
        return left.Equals(right);
    }

    public static bool operator !=(Sha256Hash left, Sha256Hash right) {
        return !left.Equals(right);
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
        /// This method does not use the 'async' keyword directly to remain compatible with the 'unsafe' struct context.
        /// </summary>
        public static async ValueTask<Sha256Hash> ComputeAsync(Stream stream, CancellationToken cancellationToken = default) {
            byte[] buffer = ArrayPool<byte>.Shared.Rent(Sha256Hash.HashSizeInBytes);
            try {
                int bytesWritten = await SHA256.HashDataAsync(stream, buffer.AsMemory(0, Sha256Hash.HashSizeInBytes), cancellationToken);

                return new Sha256Hash(buffer.AsSpan(0, Sha256Hash.HashSizeInBytes));
            }
            finally {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
    }
}