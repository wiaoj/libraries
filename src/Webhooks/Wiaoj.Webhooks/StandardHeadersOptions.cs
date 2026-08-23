namespace Wiaoj.Webhooks;

/// <summary>
/// Configuration options for standard outbound webhook HTTP metadata headers.
/// </summary>
public sealed class StandardHeadersOptions {
    /// <summary>Gets or sets the HTTP header name for the unique delivery job ID.</summary>
    public string WebhookIdHeaderName { get; set; } = WebhookHeaderNames.WebhookId;

    /// <summary>Gets or sets the HTTP header name for the event discriminator.</summary>
    public string WebhookEventHeaderName { get; set; } = WebhookHeaderNames.WebhookEvent;

    /// <summary>Gets or sets the HTTP header name for the attempt counter.</summary>
    public string WebhookAttemptHeaderName { get; set; } = WebhookHeaderNames.WebhookAttempt;

    /// <summary>Gets or sets the HTTP header name for User-Agent.</summary>
    public string UserAgentHeaderName { get; set; } = WebhookHeaderNames.UserAgent;

    /// <summary>Gets or sets a custom User-Agent value. When <see langword="null"/>, uses framework default.</summary>
    public string? CustomUserAgent { get; set; }

    /// <summary>Enables or disables emitting the Webhook-Id header. Default is <see langword="true"/>.</summary>
    public bool IncludeWebhookId { get; set; } = true;

    /// <summary>Enables or disables emitting the Webhook-Event header. Default is <see langword="true"/>.</summary>
    public bool IncludeWebhookEvent { get; set; } = true;

    /// <summary>Enables or disables emitting the Webhook-Attempt header. Default is <see langword="true"/>.</summary>
    public bool IncludeWebhookAttempt { get; set; } = true;

    /// <summary>Enables or disables emitting the User-Agent header. Default is <see langword="true"/>.</summary>
    public bool IncludeUserAgent { get; set; } = true;
}