namespace Wiaoj.Primitives.Cryptography.Asymmetric;

/// <summary>
/// Provides extension methods for converting Elliptic Curve public keys to JSON Web Keys (JWK).
/// </summary>
public static class EcdsaJwkExtensions {
    /// <summary>
    /// Exports this ECDSA public key as an immutable <see cref="Jwk"/> with default parameters.
    /// </summary>
    public static Jwk ToJwk(this EcdsaPublicKey publicKey) {
        Preca.ThrowIfNull(publicKey);
        return JwkBuilder.CreateEcdsa(publicKey.CurveName, publicKey.X, publicKey.Y).Build();
    }

    /// <summary>
    /// Initializes a <see cref="JwkBuilder"/> pre-configured with this ECDSA public key's Curve and Coordinates.
    /// </summary>
    public static JwkBuilder ToJwkBuilder(this EcdsaPublicKey publicKey) {
        Preca.ThrowIfNull(publicKey);
        return JwkBuilder.CreateEcdsa(publicKey.CurveName, publicKey.X, publicKey.Y);
    }

    /// <summary>
    /// Converts a valid Elliptic Curve <see cref="Jwk"/> into an <see cref="EcdsaPublicKey"/>.
    /// </summary>
    /// <param name="jwk">The JWK containing EC parameters.</param>
    /// <returns>An <see cref="EcdsaPublicKey"/> instance.</returns>
    /// <exception cref="ArgumentException">Thrown when the JWK is not an EC key or parameters are missing.</exception>
    public static EcdsaPublicKey ToEcdsaPublicKey(this Jwk jwk) {
        Preca.ThrowIfNull(jwk);

        if(!string.Equals(jwk.KeyType, "EC", StringComparison.OrdinalIgnoreCase)) {
            throw new ArgumentException($"Cannot convert JWK with kty '{jwk.KeyType}' to an EcdsaPublicKey. Expected 'EC'.", nameof(jwk));
        }

        if(string.IsNullOrWhiteSpace(jwk.Curve) || string.IsNullOrWhiteSpace(jwk.X) || string.IsNullOrWhiteSpace(jwk.Y)) {
            throw new ArgumentException("EC JWK must contain 'crv', 'x', and 'y' parameters.", nameof(jwk));
        }

        Preca.ThrowIfNotEqual(jwk.KeyType, "EC", StringComparison.OrdinalIgnoreCase);
        Preca.ThrowIfNullOrWhiteSpace(jwk.Curve);
        Preca.ThrowIfNullOrWhiteSpace(jwk.X);
        Preca.ThrowIfNullOrWhiteSpace(jwk.Y);

        return EcdsaPublicKey.Create(
            jwk.Curve,
            Base64UrlString.Parse(jwk.X),
            Base64UrlString.Parse(jwk.Y));
    }
}