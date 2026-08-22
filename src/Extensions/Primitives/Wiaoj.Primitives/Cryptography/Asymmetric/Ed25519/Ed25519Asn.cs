// Asymmetric/Ed25519/Ed25519Asn.cs
using System.Formats.Asn1;

namespace Wiaoj.Primitives.Cryptography.Asymmetric;

/// <summary>
/// Shared RFC 8410 ASN.1 DER encode/decode helpers for Ed25519 keys.
/// </summary>
/// <remarks>
/// <para>
/// .NET has no native <c>Ed25519</c> BCL type, so the DER structures defined by RFC 8410 are hand-encoded
/// via <see cref="AsnWriter"/> / <see cref="AsnReader"/> against the shape:
/// <c>AlgorithmIdentifier { algorithm = 1.3.101.112 }</c> wrapping a raw 32-byte OCTET STRING (private key,
/// double-wrapped per RFC 8410 §7) or BIT STRING (public key).
/// </para>
/// <para>
/// This type is shared between <see cref="Ed25519PemExtensions"/> (public key PEM) and
/// <c>Ed25519KeyPair.Pem.cs</c> (private key PEM) so the ASN.1 shape is defined in exactly one place.
/// </para>
/// </remarks>
internal static class Ed25519Asn {
    /// <summary>The RFC 8410 object identifier for the Ed25519 signature algorithm.</summary>
    public const string Ed25519AlgorithmOid = "1.3.101.112";

    // ── Public Key (SubjectPublicKeyInfo) ────────────────────────────────────

    /// <summary>
    /// Encodes a raw 32-byte Ed25519 public key as an RFC 8410 SubjectPublicKeyInfo DER structure.
    /// </summary>
    /// <param name="rawPublicKey">The raw 32-byte public key.</param>
    /// <returns>The DER-encoded SubjectPublicKeyInfo bytes.</returns>
    public static byte[] EncodeSubjectPublicKeyInfo(ReadOnlySpan<byte> rawPublicKey) {
        AsnWriter writer = new(AsnEncodingRules.DER);
        using(writer.PushSequence()) {
            using(writer.PushSequence()) {
                writer.WriteObjectIdentifier(Ed25519AlgorithmOid);
            }
            writer.WriteBitString(rawPublicKey);
        }
        return writer.Encode();
    }

    /// <summary>
    /// Decodes an RFC 8410 SubjectPublicKeyInfo DER structure into an <see cref="Ed25519PublicKey"/>.
    /// </summary>
    /// <param name="der">The DER-encoded SubjectPublicKeyInfo bytes.</param>
    /// <returns>The decoded <see cref="Ed25519PublicKey"/>.</returns>
    /// <exception cref="FormatException">Thrown when the ASN.1 structure is malformed, or the BIT STRING has unused bits.</exception>
    /// <exception cref="NotSupportedException">Thrown when the encoded algorithm OID is not Ed25519.</exception>
    public static Ed25519PublicKey DecodeSubjectPublicKeyInfo(byte[] der) {
        AsnReader reader = new(der, AsnEncodingRules.DER);
        AsnReader sequence = reader.ReadSequence();

        AsnReader algorithm = sequence.ReadSequence();
        string oid = algorithm.ReadObjectIdentifier();
        if(oid != Ed25519AlgorithmOid) {
            throw new NotSupportedException($"Unsupported public key algorithm OID '{oid}'. Expected Ed25519 ('{Ed25519AlgorithmOid}').");
        }

        byte[] keyBytes = sequence.ReadBitString(out int unusedBitCount);
        if(unusedBitCount != 0) {
            throw new FormatException("Invalid Ed25519 SubjectPublicKeyInfo: unexpected unused bit count.");
        }

        return Ed25519PublicKey.Create(keyBytes);
    }

    // ── Private Key (PKCS#8) ──────────────────────────────────────────────────

    /// <summary>
    /// Encodes a raw 32-byte Ed25519 private seed as an RFC 8410 PKCS#8 DER structure.
    /// </summary>
    /// <param name="seed">The raw 32-byte private key seed.</param>
    /// <returns>The DER-encoded PKCS#8 bytes.</returns>
    public static byte[] EncodePkcs8(ReadOnlySpan<byte> seed) {
        // RFC 8410 §7: the CurvePrivateKey is itself a DER OCTET STRING, then wrapped again
        // in the outer PKCS#8 OCTET STRING (double-wrapped by design).
        AsnWriter curvePrivateKeyWriter = new(AsnEncodingRules.DER);
        curvePrivateKeyWriter.WriteOctetString(seed);

        AsnWriter writer = new(AsnEncodingRules.DER);
        using(writer.PushSequence()) {
            writer.WriteInteger(0); // version
            using(writer.PushSequence()) {
                writer.WriteObjectIdentifier(Ed25519AlgorithmOid);
            }
            writer.WriteOctetString(curvePrivateKeyWriter.Encode());
        }

        return writer.Encode();
    }

    /// <summary>
    /// Decodes an RFC 8410 PKCS#8 DER structure into a raw 32-byte Ed25519 private seed.
    /// </summary>
    /// <param name="der">The DER-encoded PKCS#8 bytes.</param>
    /// <returns>The decoded 32-byte private key seed.</returns>
    /// <exception cref="FormatException">Thrown when the ASN.1 structure is malformed, or the decoded seed is not exactly 32 bytes.</exception>
    /// <exception cref="NotSupportedException">Thrown when the encoded algorithm OID is not Ed25519.</exception>
    public static byte[] DecodePkcs8(byte[] der) {
        AsnReader reader = new(der, AsnEncodingRules.DER);
        AsnReader sequence = reader.ReadSequence();

        _ = sequence.ReadInteger(); // version

        AsnReader algorithm = sequence.ReadSequence();
        string oid = algorithm.ReadObjectIdentifier();
        if(oid != Ed25519AlgorithmOid) {
            throw new NotSupportedException($"Unsupported private key algorithm OID '{oid}'. Expected Ed25519 ('{Ed25519AlgorithmOid}').");
        }

        byte[] wrappedOctet = sequence.ReadOctetString();
        AsnReader curvePrivateKeyReader = new(wrappedOctet, AsnEncodingRules.DER);
        byte[] seed = curvePrivateKeyReader.ReadOctetString();

        if(seed.Length != Ed25519KeyPair.SeedSizeInBytes) {
            throw new FormatException($"Decoded Ed25519 seed must be exactly {Ed25519KeyPair.SeedSizeInBytes} bytes.");
        }

        return seed;
    }
}