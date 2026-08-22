namespace Wiaoj.Primitives.Cryptography.Asymmetric;

/// <summary>
/// Provides extension methods for converting Ed25519 public keys to Octet Key Pair (OKP) JSON Web Keys (JWK) according to RFC 8037.
/// </summary>
public static class Ed25519JwkExtensions {
    /// <summary>
    /// Exports this Ed25519 public key as an immutable <see cref="Jwk"/> (<c>kty: "OKP"</c>, <c>crv: "Ed25519"</c>).
    /// </summary>
    /// <param name="publicKey">The Ed25519 public key instance.</param>
    /// <returns>An immutable <see cref="Jwk"/> representation.</returns>
    public static Jwk ToJwk(this Ed25519PublicKey publicKey) {
        Preca.ThrowIf(publicKey.IsEmpty, static () => new ArgumentException("Cannot convert an empty Ed25519PublicKey to a JWK.", nameof(publicKey)));
        return JwkBuilder.CreateOctetKeyPair("Ed25519", publicKey.X).Build();
    }

    /// <summary>
    /// Initializes a <see cref="JwkBuilder"/> pre-configured with this Ed25519 public key.
    /// </summary>
    /// <param name="publicKey">The Ed25519 public key instance.</param>
    /// <returns>A fluent <see cref="JwkBuilder"/> configured for Ed25519 OKP.</returns>
    public static JwkBuilder ToJwkBuilder(this Ed25519PublicKey publicKey) {
        Preca.ThrowIf(publicKey.IsEmpty, static () => new ArgumentException("Cannot convert an empty Ed25519PublicKey to a JWK.", nameof(publicKey)));
        return JwkBuilder.CreateOctetKeyPair("Ed25519", publicKey.X);
    }

    /// <summary>
    /// Converts a valid Octet Key Pair (OKP / Ed25519) <see cref="Jwk"/> into an <see cref="Ed25519PublicKey"/>.
    /// </summary>
    /// <param name="jwk">The JWK containing OKP parameters.</param>
    /// <returns>An <see cref="Ed25519PublicKey"/> instance.</returns>
    /// <exception cref="ArgumentException">Thrown when the JWK is not an OKP/Ed25519 key or parameters are missing.</exception>
    public static Ed25519PublicKey ToEd25519PublicKey(this Jwk jwk) {
        Preca.ThrowIfNull(jwk);

        Preca.ThrowIfNotEqual(jwk.KeyType, "OKP", StringComparison.OrdinalIgnoreCase);
        Preca.ThrowIfNotEqual(jwk.Curve, "Ed25519", StringComparison.OrdinalIgnoreCase);
        Preca.ThrowIfNullOrWhiteSpace(jwk.X);
  
        return Ed25519PublicKey.Create(Base64UrlString.Parse(jwk.X));
    }
}