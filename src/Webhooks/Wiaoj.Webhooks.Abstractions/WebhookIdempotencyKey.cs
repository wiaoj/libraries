using Wiaoj.Primitives.Hashing;

namespace Wiaoj.Webhooks;

/// <summary>
/// Builds the deterministic <see cref="IdempotencyKey"/> a webhook delivery is deduplicated by.
/// </summary>
/// <remarks>
/// The key itself lives in <c>Wiaoj.Idempotency.Abstractions</c> and knows nothing about webhooks; this is the
/// webhook-shaped way of producing one.
/// </remarks>
public static class WebhookIdempotencyKey {
    private const string Prefix = "idemp:";

    /// <summary>
    /// Creates a deterministic idempotency key from the endpoint id, the event name and a 128-bit payload digest.
    /// </summary>
    /// <param name="endpointId">The destination endpoint identifier.</param>
    /// <param name="eventName">The wire-format event name.</param>
    /// <param name="payloadHash">The 128-bit SIMD hash of the payload.</param>
    /// <returns>A new <see cref="IdempotencyKey"/> instance.</returns>
    public static IdempotencyKey Create(WebhookEndpointId endpointId, string eventName, XxHash128 payloadHash) {
        Preca.ThrowIfNullOrWhiteSpace(eventName);

        int length = Prefix.Length + endpointId.Value.Length + 1 + eventName.Length + 1 + (XxHash128.HashSizeInBytes * 2);

        string keyString = string.Create(length, (endpointId.Value, eventName, payloadHash), static (span, state) => {
            Prefix.AsSpan().CopyTo(span);
            span = span[Prefix.Length..];

            state.Value.AsSpan().CopyTo(span);
            span = span[state.Value.Length..];

            span[0] = ':';
            span = span[1..];

            state.eventName.AsSpan().CopyTo(span);
            span = span[state.eventName.Length..];

            span[0] = ':';
            span = span[1..];

            state.payloadHash.TryFormat(span, out _);
        });

        return new IdempotencyKey(keyString);
    }
}
