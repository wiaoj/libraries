using System.Security.Cryptography;

namespace Wiaoj.Primitives.Cryptography.Asymmetric;

/// <summary>
/// PKCS#8 PEM (RFC 7468) support for <see cref="EcdsaKeyPair"/> private key material.
/// </summary>
public sealed partial class EcdsaKeyPair {
    /// <summary>
    /// Exports the private key as a PKCS#8 PEM-encoded secret (RFC 5958 / RFC 7468).
    /// The caller must dispose the returned <see cref="Secret{Char}"/>.
    /// </summary>
    /// <returns>A <see cref="Secret{Char}"/> containing the PEM text.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when this key pair has been disposed.</exception>
    public Secret<char> ExportPkcs8PrivateKeyPem() {
        this._disposeState.ThrowIfDisposingOrDisposed(nameof(EcdsaKeyPair));

        byte[] der = this._ecdsa.ExportPkcs8PrivateKey();
        try {
            return PemCodec.WriteToSecret(PemLabel.PrivateKey, der);
        }
        finally {
            CryptographicOperations.ZeroMemory(der);
        }
    }

    /// <summary>
    /// Reconstructs an <see cref="EcdsaKeyPair"/> from a PKCS#8 PEM-encoded private key.
    /// </summary>
    /// <param name="pem">The PKCS#8 PEM character span.</param>
    /// <returns>The reconstructed <see cref="EcdsaKeyPair"/>.</returns>
    /// <exception cref="CryptographicException">Thrown when <paramref name="pem"/> is not a valid PKCS#8 EC key.</exception>
    /// <exception cref="NotSupportedException">Thrown when the imported curve is not P-256, P-384, or P-521.</exception>
    public static EcdsaKeyPair FromPem(ReadOnlySpan<char> pem) {
        ECDsa ecdsa = ECDsa.Create();
        ecdsa.ImportFromPem(pem);

        ECParameters publicParams = ecdsa.ExportParameters(false);
        string curveName = EcdsaCurveOids.ToCurveName(publicParams.Curve.Oid.Value);
        Base64UrlString x = Base64UrlString.FromBytes(publicParams.Q.X!);
        Base64UrlString y = Base64UrlString.FromBytes(publicParams.Q.Y!);

        ECDsa publicEcdsa = ECDsa.Create(publicParams);
        EcdsaPublicKey publicKey = new(publicEcdsa, curveName, x, y);

        return new EcdsaKeyPair(ecdsa, publicKey);
    }
}