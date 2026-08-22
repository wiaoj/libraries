using System.Diagnostics;
using System.Security.Cryptography;
using Wiaoj.Primitives.Buffers;

namespace Wiaoj.Primitives.Cryptography.Asymmetric;

/// <summary>
/// Represents an asymmetric RSA key pair containing both private and public keys.
/// Used for digital signing and data decryption.
/// </summary>
/// <remarks>
/// Private key operations leverage hardware acceleration and automatically zero unmanaged memory on disposal.
/// </remarks>
[DebuggerDisplay("RsaKeyPair ({KeySizeInBits}-bit)")]
public sealed partial class RsaKeyPair : IDisposable {
    private readonly RSA _rsa;
    private readonly DisposeState _disposeState = new();

    /// <summary>Gets the public key portion of this key pair.</summary>
    public RsaPublicKey PublicKey { get; }

    /// <summary>Gets the size of the key in bits (e.g. 2048, 3072, 4096).</summary>
    public int KeySizeInBits {
        get {
            this._disposeState.ThrowIfDisposingOrDisposed(nameof(RsaKeyPair));
            return this._rsa.KeySize;
        }
    }

    private RsaKeyPair(RSA rsa, RsaPublicKey publicKey) {
        this._rsa = rsa;
        this.PublicKey = publicKey;
    }

    // ── Key Generation Factories ──────────────────────────────────────────────

    /// <summary>
    /// Generates a standard 2048-bit RSA key pair (baseline industry security).
    /// </summary>
    /// <returns>A new <see cref="RsaKeyPair"/> instance.</returns>
    public static RsaKeyPair Generate2048() {
        return Generate(2048);
    }

    /// <summary>
    /// Generates a high-security 3072-bit RSA key pair (128-bit security level equivalent).
    /// </summary>
    /// <returns>A new <see cref="RsaKeyPair"/> instance.</returns>
    public static RsaKeyPair Generate3072() {
        return Generate(3072);
    }

    /// <summary>
    /// Generates an ultra-secure 4096-bit RSA key pair.
    /// </summary>
    /// <returns>A new <see cref="RsaKeyPair"/> instance.</returns>
    public static RsaKeyPair Generate4096() {
        return Generate(4096);
    }

    /// <summary>
    /// Generates a new cryptographically secure RSA key pair with the specified bit size.
    /// </summary>
    /// <param name="keySizeInBits">The key size in bits. Must be at least 2048 and a multiple of 64. Defaults to 2048.</param>
    /// <returns>A newly generated <see cref="RsaKeyPair"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="keySizeInBits"/> is less than 2048 or not aligned to 64 bits.</exception>
    public static RsaKeyPair Generate(int keySizeInBits = 2048) {
        Preca.ThrowIfInvalidRsaKeySize(keySizeInBits);

        RSA rsa = RSA.Create(keySizeInBits);
        RSAParameters publicParams = rsa.ExportParameters(false);

        Base64UrlString modulus = Base64UrlString.FromBytes(publicParams.Modulus!);
        Base64UrlString exponent = Base64UrlString.FromBytes(publicParams.Exponent!);

        RSA publicRsa = RSA.Create();
        publicRsa.ImportParameters(publicParams);

        RsaPublicKey publicKey = new(publicRsa, modulus, exponent);
        return new RsaKeyPair(rsa, publicKey);
    }

    // ── Cryptographic Operations ──────────────────────────────────────────────

    /// <summary>
    /// Signs data using the private key and the specified RSA algorithm.
    /// </summary>
    /// <param name="data">The data span to compute a digital signature for.</param>
    /// <param name="algorithm">The signing algorithm (e.g. <see cref="RsaAlgorithm.PS256"/> or <see cref="RsaAlgorithm.RS256"/>).</param>
    /// <returns>A byte array containing the digital signature.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="algorithm"/> is null.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when this key pair has been disposed.</exception>
    public byte[] Sign(ReadOnlySpan<byte> data, RsaAlgorithm algorithm) {
        this._disposeState.ThrowIfDisposingOrDisposed(nameof(RsaKeyPair));
        Preca.ThrowIfNull(algorithm);

        int signatureSize = this._rsa.KeySize / 8;
        byte[] signature = new byte[signatureSize];

        this._rsa.SignData(data, signature, algorithm.HashName, algorithm.Padding);
        return signature;
    }

    /// <summary>
    /// Decrypts ciphertext directly into a destination span using the specified RSA algorithm with ZERO heap allocations.
    /// </summary>
    /// <param name="ciphertext">The ciphertext bytes to decrypt.</param>
    /// <param name="destination">The destination span to write the decrypted plaintext into.</param>
    /// <param name="algorithm">The encryption algorithm (e.g. <see cref="RsaEncryptionAlgorithm.RsaOaep256"/>).</param>
    /// <returns>The number of plaintext bytes written into <paramref name="destination"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="algorithm"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="destination"/> is too short to hold the decrypted plaintext.</exception>
    /// <exception cref="CryptographicException">Thrown when decryption fails.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when this key pair has been disposed.</exception>
    public int Decrypt(
        ReadOnlySpan<byte> ciphertext,
        Span<byte> destination,
        RsaEncryptionAlgorithm algorithm) {

        this._disposeState.ThrowIfDisposingOrDisposed(nameof(RsaKeyPair));
        Preca.ThrowIfNull(algorithm);

        return this._rsa.Decrypt(ciphertext, destination, algorithm.Padding);
    }

    /// <summary>
    /// Decrypts ciphertext directly into secure unmanaged memory without managed heap allocations.
    /// </summary>
    /// <param name="ciphertext">The ciphertext bytes to decrypt.</param>
    /// <param name="algorithm">The encryption algorithm (e.g. <see cref="RsaEncryptionAlgorithm.RsaOaep256"/>).</param>
    /// <returns>A <see cref="Secret{Byte}"/> containing the decrypted plaintext. The caller must dispose the returned secret.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="algorithm"/> is null.</exception>
    /// <exception cref="CryptographicException">Thrown when decryption or padding validation fails.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when this key pair has been disposed.</exception>
    public Secret<byte> DecryptToSecret(ReadOnlySpan<byte> ciphertext, RsaEncryptionAlgorithm algorithm) {
        this._disposeState.ThrowIfDisposingOrDisposed(nameof(RsaKeyPair));
        Preca.ThrowIfNull(algorithm);

        //  (2048-bit = 256B, 4096-bit = 512B).
        int maxPlaintextSize = this._rsa.KeySize / 8;

        using ValueBuffer<byte> plainBuffer = ValueBuffer.Create(
            maxPlaintextSize,
            stackalloc byte[512],
            CryptographicOperations.ZeroMemory);

        int bytesWritten = this._rsa.Decrypt(ciphertext, plainBuffer.Span, algorithm.Padding);

        return Secret<byte>.From(plainBuffer.Span[..bytesWritten]);
    }

    /// <summary>
    /// Securely disposes the private key and associated public key instances.
    /// </summary>
    public void Dispose() {
        if(this._disposeState.TryBeginDispose()) {
            this.PublicKey.Dispose();
            this._rsa.Dispose();
            this._disposeState.SetDisposed();
        }
    }
}