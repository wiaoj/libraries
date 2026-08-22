using System.Security.Cryptography;

namespace Wiaoj.Primitives.Cryptography.Asymmetric;

/// <summary>
/// Represents a strongly-typed RSA digital signature algorithm combining a hash function and padding mode.
/// </summary>
/// <remarks>
/// Used by <see cref="RsaKeyPair.Sign"/> and <see cref="RsaPublicKey.Verify"/> to perform digital signing operations.
/// </remarks>
public sealed record RsaAlgorithm {
    /// <summary>Gets the standard IANA / JOSE algorithm name (e.g. <c>"RS256"</c>, <c>"PS256"</c>).</summary>
    public string Name { get; }

    /// <summary>Gets the cryptographic hash algorithm name used by this signing algorithm.</summary>
    public HashAlgorithmName HashName { get; }

    /// <summary>Gets the RSA signature padding mode (<see cref="RSASignaturePadding.Pkcs1"/> or <see cref="RSASignaturePadding.Pss"/>).</summary>
    public RSASignaturePadding Padding { get; }

    private RsaAlgorithm(string name, HashAlgorithmName hashName, RSASignaturePadding padding) {
        this.Name = name;
        this.HashName = hashName;
        this.Padding = padding;
    }

    // ── RSASSA-PKCS1-v1_5 Algorithms (RFC 7518 Section 3.3) ───────────────────

    /// <summary>RSASSA-PKCS1-v1_5 with SHA-256 (RFC 7518 Section 3.3).</summary>
    public static readonly RsaAlgorithm RS256 = new("RS256", HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

    /// <summary>RSASSA-PKCS1-v1_5 with SHA-384 (RFC 7518 Section 3.3).</summary>
    public static readonly RsaAlgorithm RS384 = new("RS384", HashAlgorithmName.SHA384, RSASignaturePadding.Pkcs1);

    /// <summary>RSASSA-PKCS1-v1_5 with SHA-512 (RFC 7518 Section 3.3).</summary>
    public static readonly RsaAlgorithm RS512 = new("RS512", HashAlgorithmName.SHA512, RSASignaturePadding.Pkcs1);

    // ── RSASSA-PSS Algorithms (RFC 7518 Section 3.5 - Recommended) ────────────

    /// <summary>RSASSA-PSS with SHA-256 and MGF1 (RFC 7518 Section 3.5). Highly recommended for modern applications.</summary>
    public static readonly RsaAlgorithm PS256 = new("PS256", HashAlgorithmName.SHA256, RSASignaturePadding.Pss);

    /// <summary>RSASSA-PSS with SHA-384 and MGF1 (RFC 7518 Section 3.5).</summary>
    public static readonly RsaAlgorithm PS384 = new("PS384", HashAlgorithmName.SHA384, RSASignaturePadding.Pss);

    /// <summary>RSASSA-PSS with SHA-512 and MGF1 (RFC 7518 Section 3.5).</summary>
    public static readonly RsaAlgorithm PS512 = new("PS512", HashAlgorithmName.SHA512, RSASignaturePadding.Pss);

    /// <summary>Returns the standard IANA algorithm identifier.</summary>
    public override string ToString() {
        return this.Name;
    }
}

/// <summary>
/// Represents a strongly-typed RSA encryption and key wrapping algorithm.
/// </summary>
/// <remarks>
/// Used by <see cref="RsaPublicKey.Encrypt"/> and <see cref="RsaKeyPair.DecryptToSecret"/> to perform asymmetric encryption.
/// </remarks>
public sealed record RsaEncryptionAlgorithm {
    /// <summary>Gets the standard IANA / JOSE encryption algorithm name (e.g. <c>"RSA-OAEP-256"</c>).</summary>
    public string Name { get; }

    /// <summary>Gets the RSA encryption padding scheme.</summary>
    public RSAEncryptionPadding Padding { get; }

    private RsaEncryptionAlgorithm(string name, RSAEncryptionPadding padding) {
        this.Name = name;
        this.Padding = padding;
    }

    /// <summary>RSAES-OAEP with SHA-1 (RFC 7518 Section 4.3). Maintained for legacy compatibility.</summary>
    public static readonly RsaEncryptionAlgorithm RsaOaep = new("RSA-OAEP", RSAEncryptionPadding.OaepSHA1);

    /// <summary>RSAES-OAEP with SHA-256 and MGF1 (RFC 7518 Section 4.3). Recommended standard for encryption.</summary>
    public static readonly RsaEncryptionAlgorithm RsaOaep256 = new("RSA-OAEP-256", RSAEncryptionPadding.OaepSHA256);

    /// <summary>RSAES-OAEP with SHA-384 and MGF1 (RFC 7518 Section 4.3).</summary>
    public static readonly RsaEncryptionAlgorithm RsaOaep384 = new("RSA-OAEP-384", RSAEncryptionPadding.OaepSHA384);

    /// <summary>RSAES-OAEP with SHA-512 and MGF1 (RFC 7518 Section 4.3).</summary>
    public static readonly RsaEncryptionAlgorithm RsaOaep512 = new("RSA-OAEP-512", RSAEncryptionPadding.OaepSHA512);

    /// <summary>Returns the standard IANA encryption algorithm identifier.</summary>
    public override string ToString() {
        return this.Name;
    }
}