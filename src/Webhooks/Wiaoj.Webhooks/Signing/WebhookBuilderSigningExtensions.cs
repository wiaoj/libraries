using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Wiaoj.Webhooks.Signing;

#pragma warning disable IDE0130
namespace Wiaoj.Webhooks;
#pragma warning restore IDE0130

/// <summary>
/// Extension methods for configuring cryptographic payload signing algorithms and middleware on <see cref="IWebhookBuilder"/>.
/// </summary>
public static partial class WebhookBuilderSigningExtensions {
    /// <summary>
    /// Configures HMAC-SHA256 (scheme prefix <c>"v1"</c>) cryptographic signing and registers <see cref="SigningMiddleware"/> in the pipeline.
    /// </summary>
    /// <param name="builder">The webhook builder being configured.</param>
    /// <returns>The <see cref="IWebhookBuilder"/> instance for fluent method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> is <see langword="null"/>.</exception>
    public static IWebhookBuilder UseHmacSha256Signing(this IWebhookBuilder builder) {
        Preca.ThrowIfNull(builder);
        builder.Services.RemoveAll<IWebhookSigner>();
        builder.Services.AddSingleton<IWebhookSigner, HmacSha256WebhookSigner>();
        builder.AddMiddleware<SigningMiddleware>();
        return builder;
    }

    /// <summary>
    /// Configures HMAC-SHA512 (scheme prefix <c>"v2"</c>) cryptographic signing and registers <see cref="SigningMiddleware"/> in the pipeline.
    /// </summary>
    /// <param name="builder">The webhook builder being configured.</param>
    /// <returns>The <see cref="IWebhookBuilder"/> instance for fluent method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> is <see langword="null"/>.</exception>
    public static IWebhookBuilder UseHmacSha512Signing(this IWebhookBuilder builder) {
        Preca.ThrowIfNull(builder);
        builder.Services.RemoveAll<IWebhookSigner>();
        builder.Services.AddSingleton<IWebhookSigner, HmacSha512WebhookSigner>();
        builder.AddMiddleware<SigningMiddleware>();
        return builder;
    }

    /// <summary>
    /// Configures a custom signer type and registers <see cref="SigningMiddleware"/> in the delivery pipeline.
    /// </summary>
    /// <typeparam name="TSigner">The type implementing <see cref="IWebhookSigner"/>.</typeparam>
    /// <param name="builder">The webhook builder being configured.</param>
    /// <returns>The <see cref="IWebhookBuilder"/> instance for fluent method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> is <see langword="null"/>.</exception>
    public static IWebhookBuilder UseSigner<TSigner>(this IWebhookBuilder builder) where TSigner : class, IWebhookSigner {
        Preca.ThrowIfNull(builder);
        builder.Services.RemoveAll<IWebhookSigner>();
        builder.Services.AddSingleton<IWebhookSigner, TSigner>();
        builder.AddMiddleware<SigningMiddleware>();
        return builder;
    }

    /// <summary>
    /// Configures a custom signer instance and registers <see cref="SigningMiddleware"/> in the delivery pipeline.
    /// </summary>
    /// <param name="builder">The webhook builder being configured.</param>
    /// <param name="signer">The webhook signer instance.</param>
    /// <returns>The <see cref="IWebhookBuilder"/> instance for fluent method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> or <paramref name="signer"/> is <see langword="null"/>.</exception>
    public static IWebhookBuilder UseSigner(this IWebhookBuilder builder, IWebhookSigner signer) {
        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNull(signer);
        builder.Services.RemoveAll<IWebhookSigner>();
        builder.Services.AddSingleton(signer);
        builder.AddMiddleware<SigningMiddleware>();
        return builder;
    }

    /// <summary>
    /// Configures a custom signer using a factory delegate and registers <see cref="SigningMiddleware"/> in the delivery pipeline.
    /// </summary>
    /// <param name="builder">The webhook builder being configured.</param>
    /// <param name="implementationFactory">The factory delegate used to resolve the signer.</param>
    /// <returns>The <see cref="IWebhookBuilder"/> instance for fluent method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> or <paramref name="implementationFactory"/> is <see langword="null"/>.</exception>
    public static IWebhookBuilder UseSigner(this IWebhookBuilder builder, Func<IServiceProvider, IWebhookSigner> implementationFactory) {
        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNull(implementationFactory);
        builder.Services.RemoveAll<IWebhookSigner>();
        builder.Services.AddSingleton(implementationFactory);
        builder.AddMiddleware<SigningMiddleware>();
        return builder;
    }
}