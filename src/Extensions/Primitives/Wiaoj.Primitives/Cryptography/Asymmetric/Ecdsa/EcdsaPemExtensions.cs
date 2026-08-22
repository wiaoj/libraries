using System.Security.Cryptography;

namespace Wiaoj.Primitives.Cryptography.Asymmetric;

/// <summary>
/// Provides extension methods for converting ECDSA public keys to and from SubjectPublicKeyInfo PEM (RFC 7468).
/// </summary>
public static class EcdsaPemExtensions {

    /// <summary>
    /// Exports this ECDSA public key as a SubjectPublicKeyInfo <see cref="PemString"/>.
    /// </summary>
    /// <param name="publicKey">The ECDSA public key to export.</param>
    /// <returns>A <see cref="PemString"/> containing the SubjectPublicKeyInfo PEM.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="publicKey"/> is null.</exception>
    /// <exception cref="NotSupportedException">Thrown when <see cref="EcdsaPublicKey.CurveName"/> is not a supported JOSE curve.</exception>
    public static PemString ToPem(this EcdsaPublicKey publicKey) {
        Preca.ThrowIfNull(publicKey);

        ECCurve curve = ResolveCurve(publicKey.CurveName);
        byte[] x = publicKey.X.ToBytes();
        byte[] y = publicKey.Y.ToBytes();
        try {
            using ECDsa ecdsa = ECDsa.Create(new ECParameters { Curve = curve, Q = new ECPoint { X = x, Y = y } });
            byte[] der = ecdsa.ExportSubjectPublicKeyInfo();
            return PemCodec.WriteToPemString(PemLabel.PublicKey, der);
        }
        finally {
            CryptographicOperations.ZeroMemory(x);
            CryptographicOperations.ZeroMemory(y);
        }
    }

    /// <summary>
    /// Parses a SubjectPublicKeyInfo PEM block into an <see cref="EcdsaPublicKey"/>.
    /// </summary>
    /// <param name="pem">The PEM-encoded ECDSA public key.</param>
    /// <returns>The parsed <see cref="EcdsaPublicKey"/> instance.</returns>
    /// <exception cref="FormatException">Thrown when <paramref name="pem"/> does not carry the expected PEM label.</exception>
    /// <exception cref="CryptographicException">Thrown when the PEM payload is not a valid EC SubjectPublicKeyInfo.</exception>
    /// <exception cref="NotSupportedException">Thrown when the encoded curve is not P-256, P-384, or P-521.</exception>
    public static EcdsaPublicKey ToEcdsaPublicKey(this PemString pem) {
        pem.EnsureLabel(PemLabel.PublicKey);

        using ECDsa ecdsa = ECDsa.Create();
        ecdsa.ImportFromPem(pem.Value);
        ECParameters parameters = ecdsa.ExportParameters(false);

        string curveName = EcdsaCurveOids.ToCurveName(parameters.Curve.Oid.Value);
        return EcdsaPublicKey.Create(curveName, Base64UrlString.FromBytes(parameters.Q.X!), Base64UrlString.FromBytes(parameters.Q.Y!));
    }

    private static ECCurve ResolveCurve(string curveName) => curveName switch {
        "P-256" => ECCurve.NamedCurves.nistP256,
        "P-384" => ECCurve.NamedCurves.nistP384,
        "P-521" => ECCurve.NamedCurves.nistP521,
        _ => throw new NotSupportedException($"Elliptic curve '{curveName}' is not supported.")
    };
}