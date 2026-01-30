using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;

namespace Wiaoj.Primitives; 
/// <summary>
/// Represents a 16-byte MD5 hash. 
/// This struct guarantees the correct size and provides high-performance, 
/// allocation-free operations for computing and comparing hashes.
/// </summary>
[DebuggerDisplay("{ToString(),nq}")]
public unsafe struct Md5Hash : IEquatable<Md5Hash> {
    internal const int HashSizeInBytes = 16; // MD5 is 128 bits = 16 bytes
    private fixed byte _bytes[HashSizeInBytes];

    internal Md5Hash(ReadOnlySpan<byte> source) {
        if(source.Length != HashSizeInBytes) {
            throw new ArgumentException("Source span must be exactly 16 bytes long.", nameof(source));
        }

        fixed(byte* p = this._bytes) {
            source.CopyTo(new Span<byte>(p, HashSizeInBytes));
        }
    }

    #region Factory Methods

    /// <summary>
    /// Represents an MD5 hash consisting of all zero bytes.
    /// </summary>
    public static readonly Md5Hash Empty = new(stackalloc byte[HashSizeInBytes]);

    /// <summary>
    /// Creates a Md5Hash instance from a 16-byte span.
    /// </summary>
    public static Md5Hash FromBytes(ReadOnlySpan<byte> source) {
        return new Md5Hash(source);
    }

    /// <summary>
    /// Creates a Md5Hash instance from a hexadecimal string representation.
    /// </summary>
    /// <exception cref="FormatException">The input is not a valid 32-character hexadecimal string.</exception>
    public static Md5Hash From(HexString hex) {
        if(hex.GetDecodedLength() != HashSizeInBytes) {
            throw new FormatException("Source HexString must represent exactly 16 bytes (32 hex characters).");
        }

        Span<byte> buffer = stackalloc byte[HashSizeInBytes];
        hex.TryDecode(buffer, out _);
        return new Md5Hash(buffer);
    }

    /// <summary>
    /// Creates a Md5Hash instance from a Base64String.
    /// </summary>
    public static Md5Hash From(Base64String base64) {
        if(base64.GetDecodedLength() != HashSizeInBytes) {
            throw new FormatException("Source Base64String must represent exactly 16 bytes.");
        }

        Span<byte> buffer = stackalloc byte[HashSizeInBytes];
        if(!base64.TryDecode(buffer, out int written) || written != HashSizeInBytes) {
            throw new FormatException("Failed to decode Base64 into Hash.");
        }
        return new Md5Hash(buffer);
    }

    /// <summary>
    /// Tries to create a Md5Hash instance from a hexadecimal string representation.
    /// </summary>
    public static bool TryParse(HexString hex, out Md5Hash result) {
        if(hex.GetDecodedLength() != HashSizeInBytes) {
            result = default;
            return false;
        }

        Span<byte> buffer = stackalloc byte[HashSizeInBytes];
        if(hex.TryDecode(buffer, out _)) {
            result = new Md5Hash(buffer);
            return true;
        }

        result = default;
        return false;
    }

    #endregion

    #region High-Performance Computation

    /// <summary>
    /// Computes the MD5 hash for the contents of a <see cref="Secret{Byte}"/>.
    /// </summary>
    public static Md5Hash Compute(Secret<byte> secret) {
        Preca.ThrowIfNull(secret);
        return secret.Expose(span => Compute(span));
    }

    /// <summary>
    /// Computes the MD5 hash of a span of bytes. This method is allocation-free.
    /// </summary>
    [SkipLocalsInit]
    public static Md5Hash Compute(ReadOnlySpan<byte> data) {
        Span<byte> hashBuffer = stackalloc byte[HashSizeInBytes];
        MD5.HashData(data, hashBuffer);
        return new Md5Hash(hashBuffer);
    }

    /// <summary>
    /// Computes the MD5 hash for the contents of a <see cref="Secret{Char}"/> using the specified encoding.
    /// </summary>
    public static Md5Hash Compute(Secret<char> secret, Encoding encoding) {
        Preca.ThrowIfNull(secret);
        Preca.ThrowIfNull(encoding);

        return secret.Expose(chars => {
            int maxByteCount = encoding.GetMaxByteCount(chars.Length);
            // Use stack if small enough, otherwise rent from pool to avoid LOH/GC pressure
            byte[]? rented = null;
            Span<byte> byteSpan = maxByteCount <= 512
                ? stackalloc byte[maxByteCount]
                : (rented = ArrayPool<byte>.Shared.Rent(maxByteCount));

            try {
                int bytesWritten = encoding.GetBytes(chars, byteSpan);
                return Compute(byteSpan[..bytesWritten]);
            }
            finally {
                if(rented != null) ArrayPool<byte>.Shared.Return(rented);
            }
        });
    }

    /// <summary>
    /// Computes the MD5 hash of a string using UTF-8 encoding by default.
    /// </summary>
    public static Md5Hash Compute(string text) {
        return Compute(text, Encoding.UTF8);
    }

    /// <summary>
    /// Computes the MD5 hash of a string using the specified encoding.
    /// </summary>
    public static Md5Hash Compute(string text, Encoding encoding) {
        Preca.ThrowIfNull(text);
        Preca.ThrowIfNull(encoding);

        // Optimize for common short strings using stack allocation
        int maxByteCount = encoding.GetMaxByteCount(text.Length);
        if(maxByteCount <= 256) {
            Span<byte> buffer = stackalloc byte[maxByteCount];
            int written = encoding.GetBytes(text, buffer);
            return Compute(buffer[..written]);
        }

        return Compute(encoding.GetBytes(text));
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
            throw new ArgumentException("Destination span must be at least 16 bytes long.", nameof(destination));
        }
        AsSpan().CopyTo(destination);
    }

    /// <summary>
    /// Returns a <see cref="ReadOnlySpan{Byte}"/> view of the hash bytes.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlySpan<byte> AsSpan() {
        return new ReadOnlySpan<byte>(Unsafe.AsPointer(ref Unsafe.AsRef(in this._bytes[0])), HashSizeInBytes);
    }

    /// <summary>
    /// Returns the hash as a type-safe HexString.
    /// </summary>
    public HexString ToHexString() {
        return HexString.FromBytes(AsSpan());
    }

    /// <summary>
    /// Returns the hash as a type-safe Base64String.
    /// </summary>
    public Base64String ToBase64String() {
        return Base64String.FromBytes(AsSpan());
    }

    /// <summary>
    /// Returns the hash as a hexadecimal string (Legacy support).
    /// </summary>
    public override string ToString() {
        return Convert.ToHexString(AsSpan());
    }

    #endregion

    #region Equality

    /// <summary>
    /// Compares two hashes for equality in a way that is resistant to timing attacks.
    /// </summary>
    public bool Equals(Md5Hash other) {
        return CryptographicOperations.FixedTimeEquals(AsSpan(), other.AsSpan());
    }

    public override bool Equals(object? obj) {
        return obj is Md5Hash other && Equals(other);
    }

    public override int GetHashCode() {
        // Read first 4 bytes as int for fast hash code distribution
        return BitConverter.ToInt32(AsSpan());
    }

    public static bool operator ==(Md5Hash left, Md5Hash right) {
        return left.Equals(right);
    }

    public static bool operator !=(Md5Hash left, Md5Hash right) {
        return !left.Equals(right);
    }

    #endregion
}

/// <summary>
/// Extension methods for <see cref="Md5Hash"/>.
/// </summary>
public static partial class Md5HashExtensions {

    /// <summary>
    /// Asynchronously computes the MD5 hash of a stream.
    /// </summary>
    public static async ValueTask<Md5Hash> ComputeMd5Async(this Stream stream, CancellationToken cancellationToken = default) {
        byte[] buffer = ArrayPool<byte>.Shared.Rent(Md5Hash.HashSizeInBytes);
        try {
            await MD5.HashDataAsync(stream, buffer.AsMemory(0, Md5Hash.HashSizeInBytes), cancellationToken);
            return new Md5Hash(buffer.AsSpan(0, Md5Hash.HashSizeInBytes));
        }
        finally {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
}