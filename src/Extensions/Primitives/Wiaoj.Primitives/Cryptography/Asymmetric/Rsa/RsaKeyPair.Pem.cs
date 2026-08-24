using System.Security.Cryptography;

namespace Wiaoj.Primitives.Cryptography.Asymmetric;

/// <summary>
/// PKCS#8 PEM (RFC 7468) support for <see cref="RsaKeyPair"/> private key material.
/// </summary>
/// <remarks>
/// Lives as a partial class (rather than an extension method) because private key export/import
/// must reach the private, disposable <c>RSA</c> instance and <c>_disposeState</c> field — both
/// inaccessible from outside <see cref="RsaKeyPair"/>. Public key PEM conversions live in
/// <see cref="RsaPemExtensions"/> instead, since they only need public properties.
/// </remarks>
public sealed partial class RsaKeyPair {
    /// <summary>
    /// Exports the private key as a PKCS#8 PEM-encoded secret (RFC 5958 / RFC 7468).
    /// The caller must dispose the returned <see cref="Secret{Char}"/>.
    /// </summary>
    /// <returns>A <see cref="Secret{Char}"/> containing the PEM text.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when this key pair has been disposed.</exception>
    public Secret<char> ExportPkcs8PrivateKeyPem() {
        this._disposeState.ThrowIfDisposingOrDisposed(nameof(RsaKeyPair));

        byte[] der = this._rsa.ExportPkcs8PrivateKey();
        try {
            return PemCodec.WriteToSecret(PemLabel.PrivateKey, der);
        }
        finally {
            CryptographicOperations.ZeroMemory(der);
        }
    }

    /// <summary>
    /// Reconstructs an <see cref="RsaKeyPair"/> from a PKCS#8 PEM-encoded private key.
    /// </summary>
    /// <param name="pem">The PKCS#8 PEM character span.</param>
    /// <returns>The reconstructed <see cref="RsaKeyPair"/>.</returns>
    /// <exception cref="CryptographicException">Thrown when <paramref name="pem"/> is not a valid PKCS#8 RSA key.</exception>
    public static RsaKeyPair FromPem(ReadOnlySpan<char> pem) {
        RSA rsa = RSA.Create();
        rsa.ImportFromPem(pem);

        RSAParameters publicParams = rsa.ExportParameters(false);

        Preca.ThrowIfNull(publicParams.Modulus);
        Preca.ThrowIfNull(publicParams.Exponent);

        Base64UrlString modulus = Base64UrlString.FromBytes(publicParams.Modulus);
        Base64UrlString exponent = Base64UrlString.FromBytes(publicParams.Exponent);

        RSA publicRsa = RSA.Create();
        publicRsa.ImportParameters(publicParams);

        RsaPublicKey publicKey = new(publicRsa, modulus, exponent);
        return new RsaKeyPair(rsa, publicKey);
    }
}