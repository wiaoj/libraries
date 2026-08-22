using System.Diagnostics;
using System.Security.Cryptography;

namespace Wiaoj.Primitives.Cryptography.Asymmetric;

/// <summary>
/// Represents an asymmetric Ed25519 key pair containing a 32-byte private seed and a 32-byte public key.
/// </summary>
/// <remarks>
/// The private key seed is held in secure unmanaged memory (<see cref="Secret{Byte}"/>) and securely zeroed on disposal.
/// </remarks>
[DebuggerDisplay("Ed25519KeyPair (Public: {PublicKey.X.Value})")]
public sealed partial class Ed25519KeyPair : IDisposable {
    /// <summary>The size of an Ed25519 private seed in bytes (32 bytes / 256 bits).</summary>
    public const int SeedSizeInBytes = 32;

    private readonly Secret<byte> _privateSeed;
    private readonly DisposeState _disposeState = new();

    /// <summary>Gets the corresponding public key portion of this key pair.</summary>
    public Ed25519PublicKey PublicKey { get; }

    private Ed25519KeyPair(Secret<byte> privateSeed, Ed25519PublicKey publicKey) {
        this._privateSeed = privateSeed;
        this.PublicKey = publicKey;
    }

    // ── Key Generation & Factories ────────────────────────────────────────────

    /// <summary>
    /// Creates an <see cref="Ed25519KeyPair"/> from an existing secure private seed and public key.
    /// Takes ownership of <paramref name="privateSeed"/>.
    /// </summary>
    /// <param name="privateSeed">The 32-byte private key seed in secure unmanaged memory.</param>
    /// <param name="publicKey">The corresponding 32-byte public key.</param>
    /// <returns>A new <see cref="Ed25519KeyPair"/> instance.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="privateSeed"/> is not 32 bytes long.</exception>
    public static Ed25519KeyPair Create(Secret<byte> privateSeed, Ed25519PublicKey publicKey) {
        Preca.ThrowIf(
            privateSeed.Length != SeedSizeInBytes,
            static () => new ArgumentException($"Ed25519 private seed must be exactly {SeedSizeInBytes} bytes long.", nameof(privateSeed)));

        return new Ed25519KeyPair(privateSeed, publicKey);
    }

    /// <summary>
    /// Creates an <see cref="Ed25519KeyPair"/> from raw private seed bytes and a public key.
    /// Copies seed bytes into secure unmanaged memory.
    /// </summary>
    /// <param name="privateSeedBytes">The 32-byte private key seed span.</param>
    /// <param name="publicKey">The corresponding 32-byte public key.</param>
    /// <returns>A new <see cref="Ed25519KeyPair"/> instance.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="privateSeedBytes"/> is not 32 bytes long.</exception>
    public static Ed25519KeyPair Create(ReadOnlySpan<byte> privateSeedBytes, Ed25519PublicKey publicKey) {
        Preca.ThrowIf(
            privateSeedBytes.Length != SeedSizeInBytes,
            static () => new ArgumentException($"Ed25519 private seed must be exactly {SeedSizeInBytes} bytes long.", nameof(privateSeedBytes)));

        return new Ed25519KeyPair(Secret<byte>.From(privateSeedBytes), publicKey);
    }

    /// <summary>
    /// Generates a new cryptographically random 32-byte Ed25519 private seed.
    /// </summary>
    /// <param name="publicKeyDerivation">A delegate that derives the 32-byte public key from the generated 32-byte private seed.</param>
    /// <returns>A newly generated <see cref="Ed25519KeyPair"/>.</returns>
    public static Ed25519KeyPair Generate(Func<ReadOnlySpan<byte>, Ed25519PublicKey> publicKeyDerivation) {
        Preca.ThrowIfNull(publicKeyDerivation);

        Secret<byte> seed = Secret.Generate(SeedSizeInBytes);
        try {
            Ed25519PublicKey publicKey = seed.Expose(publicKeyDerivation);
            return new Ed25519KeyPair(seed, publicKey);
        }
        catch {
            seed.Dispose();
            throw;
        }
    }

    // ── Key Exposure & Operations ─────────────────────────────────────────────

    /// <summary>
    /// Provides scoped, secure access to the raw 32-byte private seed.
    /// </summary>
    /// <param name="action">The delegate receiving the private seed span.</param>
    /// <exception cref="ObjectDisposedException">Thrown when this key pair has been disposed.</exception>
    public void ExposeSeed(Action<ReadOnlySpan<byte>> action) {
        this._disposeState.ThrowIfDisposingOrDisposed(nameof(Ed25519KeyPair));
        this._privateSeed.Expose(action);
    }

    /// <summary>
    /// Exports a copy of the private key seed into a new secure <see cref="Secret{Byte}"/>.
    /// The caller must dispose the returned secret.
    /// </summary>
    public Secret<byte> ExportPrivateKeySeed() {
        this._disposeState.ThrowIfDisposingOrDisposed(nameof(Ed25519KeyPair));
        return this._privateSeed.Expose(span => Secret<byte>.From(span));
    }

    /// <summary>
    /// Securely wipes and frees the private key material from unmanaged memory.
    /// </summary>
    public void Dispose() {
        if(this._disposeState.TryBeginDispose()) {
            this._privateSeed.Dispose();
            this._disposeState.SetDisposed();
        }
    }
}