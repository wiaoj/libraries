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
    /// The default pooled connection lifetime (15 minutes).
    /// </summary>
    public static readonly TimeSpan DefaultPooledConnectionLifetime = TimeSpan.FromMinutes(15);

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

    /// <summary>
    /// Gets or sets how long a pooled outbound connection (and the DNS resolution + SSRF check performed when it
    /// was opened) may be reused before it is torn down and re-established on the next request. Default is 15 minutes.
    /// </summary>
    /// <remarks>
    /// Lowering this value increases how often <see cref="WebhookIpFilter"/> re-validates the destination
    /// (tighter defense against DNS rebinding, at the cost of more frequent DNS lookups and TCP handshakes).
    /// Raising it reduces per-delivery connection overhead but widens the window during which a destination's
    /// DNS record could change without being re-checked.
    /// </remarks>
    public TimeSpan PooledConnectionLifetime { get; set; } = DefaultPooledConnectionLifetime;

    /// <summary>
    /// Validates the configuration values, throwing an exception if any value is out of acceptable bounds.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when a value is negative, zero where a positive
    /// duration is required, or otherwise outside the range <see cref="SocketsHttpHandler"/> and the underlying
    /// socket layer can safely accept.</exception>
    public void Validate() {
        if(this.ConnectTimeout <= TimeSpan.Zero) {
            throw new ArgumentOutOfRangeException(nameof(this.ConnectTimeout), this.ConnectTimeout, "Connect timeout must be greater than zero.");
        }
        if(this.RequestTimeout <= TimeSpan.Zero) {
            throw new ArgumentOutOfRangeException(nameof(this.RequestTimeout), this.RequestTimeout, "Request timeout must be greater than zero.");
        }
        if(this.RequestTimeout < this.ConnectTimeout) {
            throw new ArgumentOutOfRangeException(nameof(this.RequestTimeout), this.RequestTimeout, "Request timeout cannot be less than the connect timeout.");
        }
        if(this.PooledConnectionLifetime <= TimeSpan.Zero && this.PooledConnectionLifetime != Timeout.InfiniteTimeSpan) {
            throw new ArgumentOutOfRangeException(nameof(this.PooledConnectionLifetime), this.PooledConnectionLifetime,
                "Pooled connection lifetime must be greater than zero, or Timeout.InfiniteTimeSpan to disable connection recycling.");
        }
        if(this.MaxResponseBodyBytes <= 0) {
            throw new ArgumentOutOfRangeException(nameof(this.MaxResponseBodyBytes), this.MaxResponseBodyBytes, "Max response body bytes must be greater than zero.");
        }
    }
}