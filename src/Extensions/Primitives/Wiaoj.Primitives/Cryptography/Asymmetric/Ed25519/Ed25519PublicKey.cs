using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace Wiaoj.Primitives.Cryptography.Asymmetric;

/// <summary>
/// Represents an immutable 32-byte (256-bit) Ed25519 public key (RFC 8032 / RFC 8037).
/// </summary>
/// <remarks>
/// Ed25519 public keys are fixed-size 32-byte compressed Edwards curve points (represented in JWK as <c>kty: "OKP"</c>, <c>crv: "Ed25519"</c>).
/// </remarks>
[DebuggerDisplay("Ed25519PublicKey ({X.Value})")]
public readonly record struct Ed25519PublicKey : IEquatable<Ed25519PublicKey> {
    /// <summary>The size of an Ed25519 public key in bytes (32 bytes / 256 bits).</summary>
    public const int KeySizeInBytes = 32;

    /// <summary>The size of an Ed25519 digital signature in bytes (64 bytes / 512 bits).</summary>
    public const int SignatureSizeInBytes = 64;

    /// <summary>Gets the Base64Url-encoded public key parameter (x).</summary>
    public Base64UrlString X { get; }

    /// <summary>Gets a value indicating whether this public key is uninitialized or empty.</summary>
    public bool IsEmpty => this.X.IsEmpty;

    private Ed25519PublicKey(Base64UrlString x) {
        this.X = x;
    }

    // ── Factories ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates an <see cref="Ed25519PublicKey"/> from a validated <see cref="Base64UrlString"/>.
    /// </summary>
    /// <param name="x">The Base64Url-encoded 32-byte public key parameter.</param>
    /// <returns>A validated <see cref="Ed25519PublicKey"/> instance.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="x"/> is empty or does not represent 32 bytes.</exception>
    public static Ed25519PublicKey Create(Base64UrlString x) {
        Preca.ThrowIfEmpty(x);
         
        Span<byte> buffer = stackalloc byte[KeySizeInBytes];

        try { 
            if(!x.TryDecode(buffer, out int written) || written != KeySizeInBytes) {
                throw new ArgumentException(
                    $"Ed25519 public key must decode to exactly {KeySizeInBytes} bytes. Got {written} bytes.",
                    nameof(x));
            }

            return new Ed25519PublicKey(x);
        }
        finally { 
            CryptographicOperations.ZeroMemory(buffer);
        }
    }

    /// <summary>
    /// Creates an <see cref="Ed25519PublicKey"/> directly from a 32-byte span.
    /// </summary>
    /// <param name="publicKeyBytes">The 32-byte span containing the raw public key.</param>
    /// <returns>A validated <see cref="Ed25519PublicKey"/> instance.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="publicKeyBytes"/> is not exactly 32 bytes long.</exception>
    public static Ed25519PublicKey Create(ReadOnlySpan<byte> publicKeyBytes) {
        Preca.ThrowIf(
            publicKeyBytes.Length != KeySizeInBytes,
            static () => new ArgumentException($"Ed25519 public key must be exactly {KeySizeInBytes} bytes long.", nameof(publicKeyBytes)));

        return new Ed25519PublicKey(Base64UrlString.FromBytes(publicKeyBytes));
    }

    // ── Data Access ───────────────────────────────────────────────────────────

    /// <summary>
    /// Returns a copy of the raw 32-byte public key in a managed byte array.
    /// </summary>
    /// <returns>A 32-byte array.</returns>
    public byte[] ToByteArray() => this.X.ToBytes();

    /// <summary>
    /// Copies the raw 32-byte public key into the destination span.
    /// </summary>
    /// <param name="destination">The destination span. Must be at least 32 bytes long.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="destination"/> is shorter than 32 bytes.</exception>
    public void CopyTo(Span<byte> destination) {
        Preca.ThrowIfLessThan(destination.Length, KeySizeInBytes);
        byte[] bytes = this.X.ToBytes();
        try {
            bytes.CopyTo(destination);
        }
        finally {
            CryptographicOperations.ZeroMemory(bytes);
        }
    }

    /// <summary>
    /// Attempts to copy the raw 32-byte public key into the destination span.
    /// </summary>
    public bool TryCopyTo(Span<byte> destination) {
        if(destination.Length < KeySizeInBytes) return false;
        CopyTo(destination);
        return true;
    }

    // ── Equality & Display ────────────────────────────────────────────────────

    public bool Equals(Ed25519PublicKey other) => this.X.Equals(other.X);
    public override int GetHashCode() => this.X.GetHashCode();
    public override string ToString() => $"[ED25519_PUBLIC_KEY ({this.X.Value})]";
}