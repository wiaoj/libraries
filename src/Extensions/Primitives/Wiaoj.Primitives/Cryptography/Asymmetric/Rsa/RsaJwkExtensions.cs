namespace Wiaoj.Primitives.Cryptography.Asymmetric;

/// <summary>
/// Provides extension methods for converting RSA public keys to JSON Web Keys (JWK).
/// </summary>
public static class RsaJwkExtensions {
    /// <summary>
    /// Exports this public key as a bare <see cref="Jwk"/> with default parameters.
    /// </summary>
    /// <returns>An immutable <see cref="Jwk"/> representing this public key.</returns>
    public static Jwk ToJwk(this RsaPublicKey publicKey) {
        Preca.ThrowIfNull(publicKey);
        return JwkBuilder.CreateRsa(publicKey.Modulus, publicKey.Exponent).Build();
    }

    /// <summary>
    /// Initializes a <see cref="JwkBuilder"/> pre-configured with this public key's Modulus and Exponent.
    /// </summary>
    /// <returns>A fluent <see cref="JwkBuilder"/> ready for customization.</returns>
    public static JwkBuilder ToJwkBuilder(this RsaPublicKey publicKey) {
        Preca.ThrowIfNull(publicKey);
        return JwkBuilder.CreateRsa(publicKey.Modulus, publicKey.Exponent);
    }
}