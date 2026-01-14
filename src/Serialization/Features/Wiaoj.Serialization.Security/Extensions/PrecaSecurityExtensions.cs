using System.Runtime.CompilerServices;

namespace Wiaoj.Serialization.Security.Extensions;
/// <summary>
/// Provides security-specific extension methods for the Preca validation library.
/// </summary>
public static class PrecaSecurityExtensions {
    /// <summary>
    /// Throws a <see cref="WiaojSecurityConfigurationException"/> if the provided key's
    /// length is not a valid size for AES (128, 192, or 256 bits).
    /// </summary>
    /// <param name="_">The Preca extension point.</param>
    /// <param name="key">The key to validate.</param>
    /// <param name="paramName">The name of the argument being validated.</param>
    public static void ThrowIfNotValidAesKeySize(
        this PrecaExtensions _,
        ReadOnlySpan<byte> key,
        [CallerArgumentExpression(nameof(key))] string? paramName = null) {
        if(key.Length is not 16 and not 24 and not 32) {
            throw new WiaojSecurityConfigurationException(
                $"Invalid key size provided in '{paramName}'. AES-GCM requires a key of 128, 192, or 256 bits (16, 24, or 32 bytes).");
        }
    }
}