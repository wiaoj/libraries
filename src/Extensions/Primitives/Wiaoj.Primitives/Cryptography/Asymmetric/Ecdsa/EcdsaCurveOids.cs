namespace Wiaoj.Primitives.Cryptography.Asymmetric;

/// <summary>
/// Maps between JOSE curve names (RFC 7518) and their corresponding ANSI X9.62 / SEC1 curve OIDs,
/// as used by imported <see cref="System.Security.Cryptography.ECParameters"/>.
/// </summary>
internal static class EcdsaCurveOids {
    /// <summary>Resolves a curve OID (as reported by an imported key) to its JOSE curve name.</summary>
    /// <exception cref="NotSupportedException">Thrown when the OID does not correspond to a supported JOSE curve.</exception>
    public static string ToCurveName(string? oid) {
        return oid switch {
            "1.2.840.10045.3.1.7" => "P-256",
            "1.3.132.0.34" => "P-384",
            "1.3.132.0.35" => "P-521",
            _ => throw new NotSupportedException($"Imported curve OID '{oid}' is not a supported JOSE curve.")
        };
    }
}