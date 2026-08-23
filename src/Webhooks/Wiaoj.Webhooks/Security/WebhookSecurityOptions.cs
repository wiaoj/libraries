using System.Net;

namespace Wiaoj.Webhooks.Security;

/// <summary>
/// Security and outbound hardening options for the webhook HTTP transport.
/// </summary>
public sealed class WebhookSecurityOptions {
    /// <summary>
    /// The default maximum number of response body bytes captured for logging (8 KB).
    /// </summary>
    public const int DefaultMaxResponseBodyBytes = 8 * 1024;

    /// <summary>
    /// The default TCP socket connection timeout (5 seconds).
    /// </summary>
    public static readonly TimeSpan DefaultConnectTimeout = TimeSpan.FromSeconds(5);

    /// <summary>
    /// The default overall HTTP request timeout (15 seconds).
    /// </summary>
    public static readonly TimeSpan DefaultRequestTimeout = TimeSpan.FromSeconds(15);

    /// <summary>
    /// Gets or sets a value indicating whether deliveries to local, private, loopback, or link-local networks are permitted.
    /// Default is <see langword="false"/> (strict SSRF protection enabled).
    /// </summary>
    public bool AllowPrivateNetworks { get; set; } = false;

    /// <summary>
    /// Gets or sets an optional outbound egress forward proxy (e.g. Squid, Envoy, DMZ proxy).
    /// </summary>
    public IWebProxy? Proxy { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of response body bytes to read for audit history and delivery results.
    /// Prevents unbounded memory consumption (OOM) from oversized crash dumps. Default is 8192 bytes (8 KB).
    /// </summary>
    public int MaxResponseBodyBytes { get; set; } = DefaultMaxResponseBodyBytes;

    /// <summary>
    /// Gets or sets the TCP connection timeout. Default is 5 seconds.
    /// </summary>
    public TimeSpan ConnectTimeout { get; set; } = DefaultConnectTimeout;

    /// <summary>
    /// Gets or sets the total outbound request timeout. Default is 15 seconds.
    /// </summary>
    public TimeSpan RequestTimeout { get; set; } = DefaultRequestTimeout;
}