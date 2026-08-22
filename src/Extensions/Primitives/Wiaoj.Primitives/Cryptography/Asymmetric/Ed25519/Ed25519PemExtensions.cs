namespace Wiaoj.Primitives.Cryptography.Asymmetric;

/// <summary>
/// Provides RFC 8410 PEM (SubjectPublicKeyInfo) support for Ed25519 public keys.
/// </summary>
/// <remarks>
/// Only public key material is handled here, since <see cref="Ed25519PublicKey"/> exposes exclusively
/// public properties (<c>X</c>) that an extension method can safely operate on. Private key PEM support
/// lives on <see cref="Ed25519KeyPair"/> itself (see <c>Ed25519KeyPair.Pem.cs</c>), since it must reach
/// the private key seed held in secure unmanaged memory, which is only accessible through the key pair's
/// own instance methods. The underlying ASN.1 DER shapes are shared via <see cref="Ed25519Asn"/>.
/// </remarks>
public static class Ed25519PemExtensions {
    /// <summary>
    /// Exports this Ed25519 public key as an RFC 8410 SubjectPublicKeyInfo <see cref="PemString"/>.
    /// </summary>
    /// <param name="publicKey">The Ed25519 public key to export.</param>
    /// <returns>A <see cref="PemString"/> containing the SubjectPublicKeyInfo PEM.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="publicKey"/> is empty or uninitialized.</exception>
    public static PemString ToPem(this Ed25519PublicKey publicKey) {
        Preca.ThrowIf(publicKey.IsEmpty, static () => new ArgumentException("Cannot export an empty Ed25519PublicKey.", nameof(publicKey)));

        Span<byte> raw = stackalloc byte[Ed25519PublicKey.KeySizeInBytes];
        publicKey.CopyTo(raw);

        byte[] der = Ed25519Asn.EncodeSubjectPublicKeyInfo(raw);
        return PemCodec.WriteToPemString(PemLabel.PublicKey, der);
    }

    /// <summary>
    /// Parses an RFC 8410 SubjectPublicKeyInfo PEM block into an <see cref="Ed25519PublicKey"/>.
    /// </summary>
    /// <param name="pem">The PEM-encoded Ed25519 public key.</param>
    /// <returns>The parsed <see cref="Ed25519PublicKey"/> instance.</returns>
    /// <exception cref="FormatException">Thrown when <paramref name="pem"/> does not carry the expected PEM label, or the ASN.1 structure is malformed.</exception>
    /// <exception cref="NotSupportedException">Thrown when the encoded algorithm OID is not Ed25519.</exception>
    public static Ed25519PublicKey ToEd25519PublicKey(this PemString pem) {
        pem.EnsureLabel(PemLabel.PublicKey);

        byte[] der = pem.ToDerBytes();
        return Ed25519Asn.DecodeSubjectPublicKeyInfo(der);
    }
}