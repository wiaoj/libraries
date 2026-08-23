namespace Wiaoj.Webhooks;

/// <summary>
/// Defines a contract for generating deterministic, unique idempotency keys for outbound webhook deliveries.
/// </summary>
public interface IIdempotencyKeyGenerator {
    /// <summary>
    /// Generates a deterministic idempotency key directly from the active delivery context.
    /// </summary>
    /// <param name="context">The active delivery context containing endpoint, event metadata, and pre-serialized payload.</param>
    /// <returns>A strongly-typed <see cref="IdempotencyKey"/> instance.</returns>
    public IdempotencyKey GenerateKey(WebhookDeliveryContext context) {
        Preca.ThrowIfNull(context);
        return GenerateKey(context.Endpoint.Id, context.EventType, context.SerializedPayload);
    }

    /// <summary>
    /// Generates a deterministic idempotency key for the specified endpoint, event name, and serialized payload.
    /// </summary>
    /// <param name="endpointId">The destination endpoint identifier.</param>
    /// <param name="eventType">The canonical wire-format event name.</param>
    /// <param name="serializedPayload">The serialized payload content (typically JSON).</param>
    /// <returns>A strongly-typed <see cref="IdempotencyKey"/> instance.</returns>
    IdempotencyKey GenerateKey(WebhookEndpointId endpointId, string eventType, string serializedPayload);
}