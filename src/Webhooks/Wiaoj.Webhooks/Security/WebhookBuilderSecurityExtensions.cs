using Microsoft.Extensions.DependencyInjection;
using System.Net;
using Wiaoj.Webhooks.Security;

#pragma warning disable IDE0130
namespace Wiaoj.Webhooks;
#pragma warning restore IDE0130

/// <summary>
/// Extension methods for configuring outbound security, SSRF hardening, and proxy egress on <see cref="IWebhookBuilder"/>.
/// </summary>
public static partial class WebhookBuilderSecurityExtensions {
    /// <summary>
    /// Configures outbound security settings such as SSRF private network filtering, response body limits, and connection timeouts.
    /// </summary>
    /// <param name="builder">The webhook builder being configured.</param>
    /// <param name="configure">The delegate used to configure <see cref="WebhookSecurityOptions"/>.</param>
    /// <returns>The <see cref="IWebhookBuilder"/> instance for fluent method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> or <paramref name="configure"/> is <see langword="null"/>.</exception>
    public static IWebhookBuilder ConfigureSecurity(this IWebhookBuilder builder, Action<WebhookSecurityOptions> configure) {
        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNull(configure);
        builder.Services.Configure(configure);
        return builder;
    }

    /// <summary>
    /// Permits outbound webhook deliveries to local, private, loopback, and link-local networks (useful for development and local testing).
    /// </summary>
    /// <param name="builder">The webhook builder being configured.</param>
    /// <returns>The <see cref="IWebhookBuilder"/> instance for fluent method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> is <see langword="null"/>.</exception>
    public static IWebhookBuilder AllowPrivateNetworks(this IWebhookBuilder builder) {
        return builder.ConfigureSecurity(options => options.AllowPrivateNetworks = true);
    }

    /// <summary>
    /// Configures an outbound forward proxy using a proxy URL string.
    /// </summary>
    /// <param name="builder">The webhook builder being configured.</param>
    /// <param name="proxyUrl">The proxy server URI string (e.g. <c>"http://egress-proxy:8080"</c>).</param>
    /// <returns>The <see cref="IWebhookBuilder"/> instance for fluent method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="proxyUrl"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <remarks>
    /// <para>
    /// <b>Important:</b> configuring a proxy replaces the built-in <c>ConnectCallback</c>-based SSRF protection
    /// (DNS resolution + <see cref="WebhookIpFilter"/> validation against the resolved IP before opening the socket).
    /// When a proxy is set, outbound sockets are routed through it instead, and destination filtering becomes the
    /// proxy's responsibility. Make sure the configured proxy enforces its own egress allow-list/deny-list for
    /// private, loopback, and cloud-metadata ranges before relying on it in production.
    /// </para>
    /// </remarks>
    public static IWebhookBuilder UseProxy(this IWebhookBuilder builder, string proxyUrl) {
        Preca.ThrowIfNullOrWhiteSpace(proxyUrl);
        return builder.ConfigureSecurity(options => options.Proxy = new WebProxy(proxyUrl));
    }

    /// <summary>
    /// Configures an outbound forward proxy with custom network credentials.
    /// </summary>
    /// <param name="builder">The webhook builder being configured.</param>
    /// <param name="proxyUrl">The proxy server URI string.</param>
    /// <param name="credentials">The network credentials required by the proxy server.</param>
    /// <returns>The <see cref="IWebhookBuilder"/> instance for fluent method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/>, <paramref name="proxyUrl"/>, or <paramref name="credentials"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>
    /// <b>Important:</b> configuring a proxy replaces the built-in <c>ConnectCallback</c>-based SSRF protection
    /// (DNS resolution + <see cref="WebhookIpFilter"/> validation against the resolved IP before opening the socket).
    /// When a proxy is set, outbound sockets are routed through it instead, and destination filtering becomes the
    /// proxy's responsibility. Make sure the configured proxy enforces its own egress allow-list/deny-list for
    /// private, loopback, and cloud-metadata ranges before relying on it in production.
    /// </para>
    /// </remarks>
    public static IWebhookBuilder UseProxy(this IWebhookBuilder builder, string proxyUrl, ICredentials credentials) {
        Preca.ThrowIfNullOrWhiteSpace(proxyUrl);
        Preca.ThrowIfNull(credentials);
        return builder.ConfigureSecurity(options => options.Proxy = new WebProxy(proxyUrl) { Credentials = credentials });
    }

    /// <summary>
    /// Configures an outbound forward proxy using a custom <see cref="IWebProxy"/> instance.
    /// </summary>
    /// <param name="builder">The webhook builder being configured.</param>
    /// <param name="proxy">The web proxy instance.</param>
    /// <returns>The <see cref="IWebhookBuilder"/> instance for fluent method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> or <paramref name="proxy"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>
    /// <b>Important:</b> configuring a proxy replaces the built-in <c>ConnectCallback</c>-based SSRF protection
    /// (DNS resolution + <see cref="WebhookIpFilter"/> validation against the resolved IP before opening the socket).
    /// When a proxy is set, outbound sockets are routed through it instead, and destination filtering becomes the
    /// proxy's responsibility. Make sure the configured proxy enforces its own egress allow-list/deny-list for
    /// private, loopback, and cloud-metadata ranges before relying on it in production.
    /// </para>
    /// </remarks>
    public static IWebhookBuilder UseProxy(this IWebhookBuilder builder, IWebProxy proxy) {
        Preca.ThrowIfNull(proxy);
        return builder.ConfigureSecurity(options => options.Proxy = proxy);
    }

    /// <summary>
    /// Calculates payload content digests and injects the RFC 9530 <c>Content-Digest</c> header into outbound requests.
    /// </summary>
    /// <param name="builder">The webhook builder being configured.</param>
    /// <param name="algorithm">The digest algorithm to use. Default is <see cref="ContentDigestAlgorithm.XxHash128"/>.</param>
    /// <param name="configure">An optional delegate to configure <see cref="ContentDigestOptions"/>.</param>
    /// <returns>The <see cref="IWebhookBuilder"/> instance for fluent method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> is <see langword="null"/>.</exception>
    public static IWebhookBuilder UseContentDigest(
        this IWebhookBuilder builder,
        ContentDigestAlgorithm algorithm = ContentDigestAlgorithm.XxHash128,
        Action<ContentDigestOptions>? configure = null) {
        Preca.ThrowIfNull(builder);

        ContentDigestOptions options = new() { Algorithm = algorithm };
        configure?.Invoke(options);

        builder.Services.AddSingleton(options);
        builder.AddMiddleware<ContentDigestMiddleware>();
        return builder;
    }
}