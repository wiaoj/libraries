namespace Wiaoj.Webhooks;

/// <summary>
/// Resolves a <see cref="WebhookEndpointId"/> to the concrete <see cref="WebhookEndpoint"/>
/// it refers to (target URL, secret, and any other delivery-relevant configuration).
/// </summary>
/// <remarks>
/// <see cref="WebhookEndpointId"/> is intentionally opaque — it may be a database-generated
/// <see cref="Guid"/>, a hash, or a raw URL. This resolver is the single place where that
/// opaque identifier is turned into something a deliverer can actually send to.
/// </remarks>
public interface IWebhookEndpointResolver {

    /// <summary>
    /// Resolves <paramref name="endpointId"/> to its registered endpoint details.
    /// </summary>
    /// <param name="endpointId">The identifier of the endpoint to resolve.</param>
    /// <param name="cancellationToken">A token to observe for cancellation requests.</param>
    /// <returns>The resolved <see cref="WebhookEndpoint"/>, or <see langword="null"/> if no endpoint is registered for <paramref name="endpointId"/>.</returns>
    ValueTask<WebhookEndpoint?> ResolveAsync(WebhookEndpointId endpointId, CancellationToken cancellationToken = default);
}