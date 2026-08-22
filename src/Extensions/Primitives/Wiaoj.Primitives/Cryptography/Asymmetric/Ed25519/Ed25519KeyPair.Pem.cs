using System.Security.Cryptography;

namespace Wiaoj.Primitives.Cryptography.Asymmetric;

/// <summary>
/// RFC 8410 PKCS#8 PEM support for <see cref="Ed25519KeyPair"/> private key material.
/// </summary>
/// <remarks>
/// Lives as a partial class (rather than an extension method) because private key export/import must
/// reach the private key seed held in secure unmanaged memory and the <c>_disposeState</c> field — both
/// inaccessible from outside <see cref="Ed25519KeyPair"/>. Public key PEM conversions live in
/// <see cref="Ed25519PemExtensions"/> instead, since they only need public properties. The underlying
/// ASN.1 DER shapes are shared via <see cref="Ed25519Asn"/>.
/// </remarks>
public sealed partial class Ed25519KeyPair {
    /// <summary>
    /// Exports the private key as an RFC 8410 PKCS#8 PEM-encoded secret.
    /// The caller must dispose the returned <see cref="Secret{Char}"/>.
    /// </summary>
    /// <returns>A <see cref="Secret{Char}"/> containing the PEM text.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when this key pair has been disposed.</exception>
    public Secret<char> ExportPkcs8PrivateKeyPem() {
        this._disposeState.ThrowIfDisposingOrDisposed(nameof(Ed25519KeyPair));

        byte[] der = default!;
        this.ExposeSeed(seed => der = Ed25519Asn.EncodePkcs8(seed));

        try {
            return PemCodec.WriteToSecret(PemLabel.PrivateKey, der);
        }
        finally {
            CryptographicOperations.ZeroMemory(der);
        }
    }

    /// <summary>
    /// Reconstructs an <see cref="Ed25519KeyPair"/> from an RFC 8410 PKCS#8 PEM-encoded private key.
    /// </summary>
    /// <param name="pem">The PKCS#8 PEM character span.</param>
    /// <param name="publicKeyDerivation">A delegate that derives the 32-byte public key from the decoded 32-byte seed.</param>
    /// <returns>The reconstructed <see cref="Ed25519KeyPair"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="publicKeyDerivation"/> is null.</exception>
    /// <exception cref="FormatException">Thrown when <paramref name="pem"/> does not carry the expected PEM label, the ASN.1 structure is malformed, or the decoded seed is not 32 bytes.</exception>
    /// <exception cref="NotSupportedException">Thrown when the encoded algorithm OID is not Ed25519.</exception>
    public static Ed25519KeyPair FromPem(ReadOnlySpan<char> pem, Func<ReadOnlySpan<byte>, Ed25519PublicKey> publicKeyDerivation) {
        Preca.ThrowIfNull(publicKeyDerivation);

        PemString pemString = PemString.Parse(pem);
        pemString.EnsureLabel(PemLabel.PrivateKey);

        byte[] der = pemString.ToDerBytes();
        try {
            byte[] seed = Ed25519Asn.DecodePkcs8(der);
            try {
                return Create(seed, publicKeyDerivation(seed));
            }
            finally {
                CryptographicOperations.ZeroMemory(seed);
            }
        }
        finally {
            CryptographicOperations.ZeroMemory(der);
        }
    }
}