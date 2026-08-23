using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Wiaoj.Primitives.Hashing;

namespace Wiaoj.Webhooks.Security;

/// <summary>
/// Outbound middleware that calculates payload integrity digests (e.g. SHA-256, XXHash128, CRC32)
/// and injects the RFC 9530 <c>Content-Digest</c> header into the delivery context.
/// </summary>
public sealed class ContentDigestMiddleware : IWebhookMiddleware {
    private readonly ContentDigestOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContentDigestMiddleware"/> class using default configuration options.
    /// </summary>
    public ContentDigestMiddleware() : this(new ContentDigestOptions()) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="ContentDigestMiddleware"/> class with the specified configuration options.
    /// </summary>
    /// <param name="options">The configuration options controlling the digest algorithm and header output.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> is <see langword="null"/>.</exception>
    public ContentDigestMiddleware(ContentDigestOptions options) {
        Preca.ThrowIfNull(options);
        this._options = options;
    }

    /// <inheritdoc />
    public async Task InvokeAsync(WebhookDeliveryContext context, WebhookDelegate next, CancellationToken cancellationToken = default) {
        Preca.ThrowIfNull(context);
        Preca.ThrowIfNull(next);

        if(this._options.Algorithm != ContentDigestAlgorithm.None) {
            string digestValue = ComputeDigest(this._options.Algorithm, context.SerializedPayload);

            context.SetHeader(this._options.HeaderName, digestValue);

            if(this._options.AlsoEmitWebhookHashHeader) {
                context.SetHeader(WebhookHeaderNames.WebhookHash, digestValue);
            }
        }

        await next(context, cancellationToken).ConfigureAwait(false);
    }

    private static string ComputeDigest(ContentDigestAlgorithm algorithm, string payload) {
        return algorithm switch {
            ContentDigestAlgorithm.XxHash128 => $"{ContentDigestPrefixes.XxHash128}{XxHash128.Compute(payload)}",
            ContentDigestAlgorithm.XxHash3 => $"{ContentDigestPrefixes.XxHash3}{XxHash3.Compute(payload)}",
            ContentDigestAlgorithm.Sha256 => $"{ContentDigestPrefixes.Sha256Prefix}{Sha256Hash.Compute(payload).ToBase64String()}{ContentDigestPrefixes.StructuredFieldSuffix}",
            ContentDigestAlgorithm.Sha512 => $"{ContentDigestPrefixes.Sha512Prefix}{Sha512Hash.Compute(payload).ToBase64String()}{ContentDigestPrefixes.StructuredFieldSuffix}",
            ContentDigestAlgorithm.Crc32 => $"{ContentDigestPrefixes.Crc32}{Crc32Hash.Compute(payload)}",
            _ => string.Empty
        };
    }
}