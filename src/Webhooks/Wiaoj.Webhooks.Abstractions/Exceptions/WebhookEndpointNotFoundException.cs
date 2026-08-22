#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Wiaoj.Webhooks;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Thrown when a <see cref="WebhookDeliveryJob"/> references a <see cref="WebhookEndpointId"/>
/// that does not resolve to a registered endpoint.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="WebhookEndpointNotFoundException"/> class.
/// </remarks>
/// <param name="endpointId">The endpoint identifier that could not be resolved.</param>
public sealed class WebhookEndpointNotFoundException(WebhookEndpointId endpointId) 
    : Exception($"No webhook endpoint is registered for id '{endpointId}'.") {

    /// <summary>The endpoint identifier that could not be resolved.</summary>
    public WebhookEndpointId EndpointId { get; } = endpointId;
}