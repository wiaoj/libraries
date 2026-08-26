using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Diagnostics.CodeAnalysis;
using Wiaoj.Webhooks.Signing;
using Wiaoj.Webhooks.Signing.Asymmetric; 

#pragma warning disable IDE0130
namespace Wiaoj.Webhooks;
#pragma warning restore IDE0130

/// <summary>
/// Extension methods for configuring asymmetric cryptographic signing algorithms on <see cref="IWebhookBuilder"/>.
/// </summary>
public static partial class WebhookBuilderAsymmetricSigningExtensions {
    /// <summary>
    /// Configures modern RSASSA-PSS (PS256) asymmetric RSA signing in the outbound pipeline.
    /// </summary>
    /// <param name="builder">The webhook builder being configured.</param>
    /// <returns>The <see cref="IWebhookBuilder"/> instance for fluent chaining.</returns>
    public static IWebhookBuilder UseRsaSigning(this IWebhookBuilder builder) =>
        UseRsaSigning(builder, RsaAlgorithm.PS256);

    /// <summary>
    /// Configures asymmetric RSA signing with the specified algorithm (e.g. RS256, PS256, PS512).
    /// </summary>
    /// <param name="builder">The webhook builder being configured.</param>
    /// <param name="algorithm">The RSA algorithm configuration.</param>
    /// <returns>The <see cref="IWebhookBuilder"/> instance for fluent chaining.</returns>
    public static IWebhookBuilder UseRsaSigning(this IWebhookBuilder builder, RsaAlgorithm algorithm) {
        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNull(algorithm);

        builder.Services.RemoveAll<IWebhookSigner>();
        builder.Services.AddSingleton<IWebhookSigner>(new RsaWebhookSigner(algorithm));
        builder.AddMiddleware<SigningMiddleware>();
        return builder;
    }

    /// <summary>
    /// Configures asymmetric ECDSA (NIST P-256 / ES256) signing in the outbound pipeline.
    /// </summary>
    /// <param name="builder">The webhook builder being configured.</param>
    /// <returns>The <see cref="IWebhookBuilder"/> instance for fluent chaining.</returns>
    public static IWebhookBuilder UseEcdsaSigning(this IWebhookBuilder builder) =>
        UseEcdsaSigning(builder, EcdsaAlgorithm.ES256);

    /// <summary>
    /// Configures asymmetric ECDSA signing with the specified curve (e.g. ES256, ES384, ES512).
    /// </summary>
    /// <param name="builder">The webhook builder being configured.</param>
    /// <param name="algorithm">The ECDSA algorithm configuration.</param>
    /// <returns>The <see cref="IWebhookBuilder"/> instance for fluent chaining.</returns>
    public static IWebhookBuilder UseEcdsaSigning(this IWebhookBuilder builder, EcdsaAlgorithm algorithm) {
        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNull(algorithm);

        builder.Services.RemoveAll<IWebhookSigner>();
        builder.Services.AddSingleton<IWebhookSigner>(new EcdsaWebhookSigner(algorithm));
        builder.AddMiddleware<SigningMiddleware>();
        return builder;
    }
     
}