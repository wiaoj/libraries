using Wiaoj.Primitives.Hashing;

namespace Wiaoj.Webhooks.Idempotency;

/// <summary>
/// High-performance default idempotency key generator combining endpoint identifier, 
/// canonical event name, and SIMD-accelerated 128-bit payload digest (<see cref="XxHash128"/>).
/// </summary>
public sealed class DefaultIdempotencyKeyGenerator : IIdempotencyKeyGenerator {
    /// <inheritdoc/>
    public IdempotencyKey GenerateKey(WebhookDeliveryContext context) {
        Preca.ThrowIfNull(context);
        return GenerateKey(context.Endpoint.Id, context.EventType, context.SerializedPayload);
    }

    /// <inheritdoc/>
    public IdempotencyKey GenerateKey(WebhookEndpointId endpointId, string eventType, string serializedPayload) {
        Preca.ThrowIfNullOrWhiteSpace(eventType);
        Preca.ThrowIfNull(serializedPayload);

        XxHash128 hash = XxHash128.Compute(serializedPayload);
        return IdempotencyKey.Create(endpointId, eventType, hash);
    } 
}