using System.Diagnostics;

namespace Wiaoj.Primitives.Cryptography;

/// <summary>
/// Represents an RFC 7468 PEM label — the identifier declared in a PEM block's
/// <c>-----BEGIN {label}-----</c> / <c>-----END {label}-----</c> boundaries.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why not a raw string?</b> PEM labels appear across dozens of call sites (encode, decode, validation).
/// A raw <see cref="string"/> parameter invites typos that only surface at runtime as decode/validation
/// failures. <see cref="PemLabel"/> centralizes the well-known RFC 7468 / RFC 5958 / RFC 8410 labels as
/// static members, giving compile-time-checked call sites (<c>PemLabel.PublicKey</c>) while still allowing
/// <see cref="Custom"/> for labels this library doesn't predefine.
/// </para>
/// </remarks>
[DebuggerDisplay("{Value,nq}")]
public readonly record struct PemLabel {
    /// <summary>Gets the raw label text (e.g. <c>"PUBLIC KEY"</c>).</summary>
    public string Value { get; }

    private PemLabel(string value) {
        this.Value = value;
    }

    // ── Well-Known Labels ─────────────────────────────────────────────────────

    /// <summary>SubjectPublicKeyInfo public key (RFC 5280 / RFC 7468).</summary>
    public static readonly PemLabel PublicKey = new("PUBLIC KEY");

    /// <summary>PKCS#8 unencrypted private key (RFC 5958 / RFC 7468).</summary>
    public static readonly PemLabel PrivateKey = new("PRIVATE KEY");

    /// <summary>PKCS#8 password-encrypted private key (RFC 5958 / RFC 7468).</summary>
    public static readonly PemLabel EncryptedPrivateKey = new("ENCRYPTED PRIVATE KEY");

    /// <summary>Legacy PKCS#1 RSA private key (RFC 8017). Prefer <see cref="PrivateKey"/> (PKCS#8) for new material.</summary>
    public static readonly PemLabel RsaPrivateKey = new("RSA PRIVATE KEY");

    /// <summary>Legacy SEC1 EC private key (RFC 5915). Prefer <see cref="PrivateKey"/> (PKCS#8) for new material.</summary>
    public static readonly PemLabel EcPrivateKey = new("EC PRIVATE KEY");

    /// <summary>X.509 certificate (RFC 5280 / RFC 7468).</summary>
    public static readonly PemLabel Certificate = new("CERTIFICATE");

    /// <summary>PKCS#10 certificate signing request (RFC 2986 / RFC 7468).</summary>
    public static readonly PemLabel CertificateRequest = new("CERTIFICATE REQUEST");

    /// <summary>X.509 certificate revocation list (RFC 5280 / RFC 7468).</summary>
    public static readonly PemLabel X509Crl = new("X509 CRL");

    // ── Custom Labels ─────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a <see cref="PemLabel"/> for a label not predefined by this type.
    /// </summary>
    /// <param name="value">The label text. Must be non-empty and must not itself contain <c>"-----"</c>.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="value"/> is null/whitespace or contains <c>"-----"</c>.</exception>
    public static PemLabel Custom(string value) {
        Preca.ThrowIfNullOrWhiteSpace(value);
        Preca.ThrowIf(
            value.Contains("-----", StringComparison.Ordinal),
            static () => new ArgumentException("A PEM label must not contain the boundary delimiter '-----'.", nameof(value)));

        return new PemLabel(value);
    }
     
    /// <inheritdoc/>
    public override string ToString() => this.Value;
}