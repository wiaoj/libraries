using Microsoft.AspNetCore.Http;
using System.Text;

namespace Wiaoj.Webhooks.AspNetCore;

/// <summary>
/// Encapsulates all contextual state, verified payload, and security metadata for an incoming webhook delivery.
/// </summary>
/// <typeparam name="TEvent">The strongly-typed webhook event payload type.</typeparam>
public sealed class WebhookReceiverContext<TEvent> where TEvent : IWebhookEvent {
    private string? _bodyText;

    /// <summary>Gets the current ASP.NET Core HTTP context.</summary>
    public required HttpContext HttpContext { get; init; }

    /// <summary>Gets the deserialized domain event payload.</summary>
    public required TEvent Payload { get; init; }

    /// <summary>Gets the canonical wire-format event discriminator name.</summary>
    public required string EventType { get; init; }

    /// <summary>Gets the derived idempotency key if deduplication is enabled.</summary>
    public IdempotencyKey? IdempotencyKey { get; init; }

    /// <summary>Gets the parsed cryptographic signature details if verification succeeded.</summary>
    public WebhookSignature? Signature { get; init; }

    /// <summary>Gets the raw UTF-8 request body bytes directly from the socket without string allocations.</summary>
    public required ReadOnlyMemory<byte> RawBody { get; init; }

    /// <summary>Gets the raw UTF-8 string representation of the request body, materialized lazily on demand.</summary>
    public string BodyText => this._bodyText ??= Encoding.UTF8.GetString(this.RawBody.Span);

    /// <summary>Gets the request headers dictionary.</summary>
    public required IHeaderDictionary Headers { get; init; }
}