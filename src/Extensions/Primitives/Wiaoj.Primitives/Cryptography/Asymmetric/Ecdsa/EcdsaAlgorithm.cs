using System.Security.Cryptography;

namespace Wiaoj.Primitives.Cryptography.Asymmetric;

/// <summary>
/// Represents a strongly-typed Elliptic Curve Digital Signature Algorithm (ECDSA) combining a curve, hash algorithm, and signature format.
/// </summary>
public sealed record EcdsaAlgorithm {
    /// <summary>Gets the standard IANA / JOSE algorithm name (e.g. <c>"ES256"</c>, <c>"ES384"</c>, <c>"ES512"</c>).</summary>
    public string Name { get; }

    /// <summary>Gets the RFC curve name (e.g. <c>"P-256"</c>, <c>"P-384"</c>, <c>"P-521"</c>).</summary>
    public string CurveName { get; }

    /// <summary>Gets the cryptographic hash algorithm name.</summary>
    public HashAlgorithmName HashName { get; }

    /// <summary>Gets the underlying .NET <see cref="ECCurve"/> representation.</summary>
    public ECCurve Curve { get; }

    private EcdsaAlgorithm(string name, string curveName, HashAlgorithmName hashName, ECCurve curve) {
        this.Name = name;
        this.CurveName = curveName;
        this.HashName = hashName;
        this.Curve = curve;
    }

    // ── Standard JOSE ECDSA Algorithms (RFC 7518 Section 3.4) ─────────────────

    /// <summary>ECDSA using P-256 curve and SHA-256 (RFC 7518 Section 3.4). Required for DPoP (RFC 9449).</summary>
    public static readonly EcdsaAlgorithm ES256 = new("ES256", "P-256", HashAlgorithmName.SHA256, ECCurve.NamedCurves.nistP256);

    /// <summary>ECDSA using P-384 curve and SHA-384 (RFC 7518 Section 3.4).</summary>
    public static readonly EcdsaAlgorithm ES384 = new("ES384", "P-384", HashAlgorithmName.SHA384, ECCurve.NamedCurves.nistP384);

    /// <summary>ECDSA using P-521 curve and SHA-512 (RFC 7518 Section 3.4).</summary>
    public static readonly EcdsaAlgorithm ES512 = new("ES512", "P-521", HashAlgorithmName.SHA512, ECCurve.NamedCurves.nistP521);

    /// <summary>Returns the standard IANA algorithm identifier.</summary>
    public override string ToString() => this.Name;
}