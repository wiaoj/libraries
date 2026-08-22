using System.Security.Cryptography;

namespace Wiaoj.Primitives.Cryptography.Asymmetric;

/// <summary>
/// Provides extension methods for converting RSA public keys to and from SubjectPublicKeyInfo PEM (RFC 7468).
/// </summary>
/// <remarks>
/// Only public key material is handled here, since <see cref="RsaPublicKey"/> exposes exclusively public
/// properties (<c>Modulus</c>, <c>Exponent</c>) that an extension method can safely operate on. Private
/// key PEM support lives on <see cref="RsaKeyPair"/> itself (see <c>RsaKeyPair.Pem.cs</c>), since it must
/// reach the private, disposable <c>RSA</c> instance that only the key pair's own instance methods can access.
/// </remarks>
public static class RsaPemExtensions {
    /// <summary>
    /// Exports this RSA public key as a SubjectPublicKeyInfo <see cref="PemString"/>.
    /// </summary>
    /// <param name="publicKey">The RSA public key to export.</param>
    /// <returns>A <see cref="PemString"/> containing the SubjectPublicKeyInfo PEM.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="publicKey"/> is null.</exception>
    public static PemString ToPem(this RsaPublicKey publicKey) {
        Preca.ThrowIfNull(publicKey);

        byte[] modulus = publicKey.Modulus.ToBytes();
        byte[] exponent = publicKey.Exponent.ToBytes();
        try {
            using RSA rsa = RSA.Create();
            rsa.ImportParameters(new RSAParameters { Modulus = modulus, Exponent = exponent });
            byte[] der = rsa.ExportSubjectPublicKeyInfo();
            return PemCodec.WriteToPemString(PemLabel.PublicKey, der);
        }
        finally {
            CryptographicOperations.ZeroMemory(modulus);
            CryptographicOperations.ZeroMemory(exponent);
        }
    }

    /// <summary>
    /// Parses a SubjectPublicKeyInfo PEM block into an <see cref="RsaPublicKey"/>.
    /// </summary>
    /// <param name="pem">The PEM-encoded RSA public key.</param>
    /// <returns>The parsed <see cref="RsaPublicKey"/> instance.</returns>
    /// <exception cref="FormatException">Thrown when <paramref name="pem"/> does not carry the expected PEM label.</exception>
    /// <exception cref="CryptographicException">Thrown when the PEM payload is not a valid RSA SubjectPublicKeyInfo.</exception>
    public static RsaPublicKey ToRsaPublicKey(this PemString pem) {
        pem.EnsureLabel(PemLabel.PublicKey);

        using RSA rsa = RSA.Create();
        rsa.ImportFromPem(pem.Value);

        RSAParameters parameters = rsa.ExportParameters(false);
        return RsaPublicKey.Create(parameters.Modulus, parameters.Exponent);
    }
}