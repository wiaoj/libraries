using System.Diagnostics;
using System.Security.Cryptography;
using Wiaoj.Primitives.Buffers;

namespace Wiaoj.Primitives.Cryptography.Asymmetric;

/// <summary>
/// Represents an RSA public key used for digital signature verification and data encryption.
/// </summary>
/// <remarks>
/// This class holds strictly public key material (<c>Modulus</c> and <c>Exponent</c>), 
/// making it impossible to accidentally expose private keys when publishing via JWKS endpoints.
/// </remarks>
[DebuggerDisplay("RsaPublicKey ({KeySizeInBits}-bit)")]
public sealed class RsaPublicKey : IDisposable {
    private readonly RSA _rsa;
    private readonly DisposeState _disposeState = new();

    /// <summary>Gets the Base64Url-encoded RSA modulus parameter (n).</summary>
    public Base64UrlString Modulus { get; }

    /// <summary>Gets the Base64Url-encoded RSA public exponent parameter (e).</summary>
    public Base64UrlString Exponent { get; }

    /// <summary>Gets the size of the key in bits (e.g. 2048, 3072, 4096).</summary>
    public int KeySizeInBits {
        get {
            this._disposeState.ThrowIfDisposingOrDisposed(nameof(RsaPublicKey));
            return this._rsa.KeySize;
        }
    }

    internal RsaPublicKey(RSA rsa, Base64UrlString modulus, Base64UrlString exponent) {
        this._rsa = rsa;
        this.Modulus = modulus;
        this.Exponent = exponent;
    }

    /// <summary>
    /// Creates an <see cref="RsaPublicKey"/> directly from raw Modulus (n) and Exponent (e) byte spans.
    /// </summary>
    /// <param name="modulus">The raw big-endian modulus bytes.</param>
    /// <param name="exponent">The raw big-endian exponent bytes.</param>
    /// <returns>A validated <see cref="RsaPublicKey"/> instance.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="modulus"/> or <paramref name="exponent"/> is empty.</exception>
    public static RsaPublicKey Create(ReadOnlySpan<byte> modulus, ReadOnlySpan<byte> exponent) { 
        Preca.ThrowIfEmpty(modulus);
        Preca.ThrowIfEmpty(exponent);

        RSAParameters parameters = new() {
            Modulus = modulus.ToArray(),
            Exponent = exponent.ToArray()
        };

        RSA rsa = RSA.Create();
        rsa.ImportParameters(parameters);

        Base64UrlString modulusStr = Base64UrlString.FromBytes(modulus);
        Base64UrlString exponentStr = Base64UrlString.FromBytes(exponent);

        return new RsaPublicKey(rsa, modulusStr, exponentStr);
    }

    /// <summary>
    /// Creates an <see cref="RsaPublicKey"/> from Base64Url-encoded Modulus (n) and Exponent (e) parameters.
    /// </summary>
    /// <param name="modulus">The Base64Url-encoded Modulus (n).</param>
    /// <param name="exponent">The Base64Url-encoded Exponent (e).</param>
    /// <returns>A validated <see cref="RsaPublicKey"/> instance.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="modulus"/> or <paramref name="exponent"/> is empty.</exception>
    public static RsaPublicKey Create(Base64UrlString modulus, Base64UrlString exponent) {
        Preca.ThrowIfEmpty(modulus);
        Preca.ThrowIfEmpty(exponent);

        byte[] modArray = modulus.ToBytes();
        byte[] expArray = exponent.ToBytes();

        try {
            RSAParameters parameters = new() {
                Modulus = modArray,
                Exponent = expArray
            };

            RSA rsa = RSA.Create();
            rsa.ImportParameters(parameters);
            return new RsaPublicKey(rsa, modulus, exponent);
        }
        finally {
            CryptographicOperations.ZeroMemory(modArray);
            CryptographicOperations.ZeroMemory(expArray);
        }
    }

    /// <summary>
    /// Verifies that a digital signature matches the provided data using the specified RSA algorithm.
    /// </summary>
    /// <param name="data">The original data span that was signed.</param>
    /// <param name="signature">The digital signature bytes to verify.</param>
    /// <param name="algorithm">The signing algorithm used to produce the signature (e.g. <see cref="RsaAlgorithm.PS256"/>).</param>
    /// <returns><see langword="true"/> if the signature is valid; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="algorithm"/> is null.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when this public key has been disposed.</exception>
    public bool Verify(ReadOnlySpan<byte> data, ReadOnlySpan<byte> signature, RsaAlgorithm algorithm) {
        this._disposeState.ThrowIfDisposingOrDisposed(nameof(RsaPublicKey));
        Preca.ThrowIfNull(algorithm);

        return this._rsa.VerifyData(data, signature, algorithm.HashName, algorithm.Padding);
    }

    /// <summary>
    /// Encrypts plaintext data using this public key with the specified RSA encryption padding.
    /// </summary>
    /// <param name="plaintext">The plaintext bytes to encrypt.</param>
    /// <param name="algorithm">The encryption algorithm (e.g. <see cref="RsaEncryptionAlgorithm.RsaOaep256"/>).</param>
    /// <returns>A strongly-typed <see cref="Ciphertext"/> instance containing the encrypted payload.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="algorithm"/> is null.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when this public key has been disposed.</exception>
    /// <exception cref="CryptographicException">Thrown when the plaintext length exceeds the maximum allowed by the RSA key size and padding scheme.</exception>
    public Ciphertext Encrypt(ReadOnlySpan<byte> plaintext, RsaEncryptionAlgorithm algorithm) {
        this._disposeState.ThrowIfDisposingOrDisposed(nameof(RsaPublicKey));
        Preca.ThrowIfNull(algorithm);

        // RSA şifreleme çıktısı daima anahtar boyutunun byte karşılığına eşittir (Örn: 2048-bit = 256 byte).
        int outputSize = this._rsa.KeySize / 8;
        byte[] destination = new byte[outputSize];

        // plaintext.ToArray() yapmadan doğrudan Span üzerinden şifreler:
        int bytesWritten = this._rsa.Encrypt(plaintext, destination, algorithm.Padding);

        return Ciphertext.From(destination.AsSpan(0, bytesWritten));
    }

    /// <summary>Disposes the underlying cryptographic resources.</summary>
    public void Dispose() {
        if(this._disposeState.TryBeginDispose()) {
            this._rsa.Dispose();
            this._disposeState.SetDisposed();
        }
    }
}