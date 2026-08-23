namespace Wiaoj.Webhooks.Security;

/// <summary>
/// Configuration options for payload content digest hashing.
/// </summary>
public sealed class ContentDigestOptions {
    /// <summary>
    /// Gets or sets the digest algorithm to compute. Default is <see cref="ContentDigestAlgorithm.XxHash128"/>.
    /// </summary>
    public ContentDigestAlgorithm Algorithm { get; set; } = ContentDigestAlgorithm.XxHash128;

    /// <summary>
    /// Gets or sets the header name for the digest. Default is <c>"Content-Digest"</c> (RFC 9530).
    /// </summary>
    public string HeaderName { get; set; } = "Content-Digest";

    /// <summary>
    /// When true, also emits a legacy/secondary <c>"Webhook-Hash"</c> header. Default is <see langword="false"/>.
    /// </summary>
    public bool AlsoEmitWebhookHashHeader { get; set; } = false;
}