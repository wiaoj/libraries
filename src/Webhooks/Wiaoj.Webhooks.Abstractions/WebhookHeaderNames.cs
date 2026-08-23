namespace Wiaoj.Webhooks;

/// <summary>
/// Defines canonical HTTP header name constants used across the webhook engine and delivery pipeline.
/// </summary>
public static class WebhookHeaderNames {
    /// <summary>Canonical header for unique delivery job identifier (<c>"Webhook-Id"</c>).</summary>
    public const string WebhookId = "Webhook-Id";

    /// <summary>Canonical header for wire-format event discriminator name (<c>"Webhook-Event"</c>).</summary>
    public const string WebhookEvent = "Webhook-Event";

    /// <summary>Canonical header for delivery attempt sequence number (<c>"Webhook-Attempt"</c>).</summary>
    public const string WebhookAttempt = "Webhook-Attempt";

    /// <summary>Canonical header for HTTP user agent identity (<c>"User-Agent"</c>).</summary>
    public const string UserAgent = "User-Agent";

    /// <summary>Standard RFC 9530 header for payload integrity digest (<c>"Content-Digest"</c>).</summary>
    public const string ContentDigest = "Content-Digest";

    /// <summary>Legacy/Secondary header for payload hash (<c>"Webhook-Hash"</c>).</summary>
    public const string WebhookHash = "Webhook-Hash";

    /// <summary>Standard cryptographic signature transport header (<c>"Webhook-Signature"</c>).</summary>
    public const string WebhookSignature = "Webhook-Signature";

    /// <summary>Standard RFC 9110 retry backoff instruction header (<c>"Retry-After"</c>).</summary>
    public const string RetryAfter = "Retry-After";
}