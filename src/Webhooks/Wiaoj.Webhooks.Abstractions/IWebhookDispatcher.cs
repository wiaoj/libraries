namespace Wiaoj.Webhooks;

/// <summary>
/// Defines the single entry point for dispatching webhook events.
/// </summary>
public interface IWebhookDispatcher {
    /// <summary>
    /// Dispatches a webhook event to the specified endpoint with an explicit partition key for FIFO ordering.
    /// </summary>
    /// <typeparam name="TEvent">The type of the event being dispatched.</typeparam>
    /// <param name="endpointId">The identifier of the target endpoint.</param>
    /// <param name="payload">The event payload to dispatch.</param>
    /// <param name="partitionKey">The partition key (e.g. OrderId, CustomerId, TenantId, or EndpointId).</param>
    /// <param name="cancellationToken">A token to observe for cancellation requests.</param>
    /// <returns>A delivery handle containing the scheduled job identifier.</returns>
    Task<WebhookDeliveryHandle> DispatchAsync<TEvent>(
        WebhookEndpointId endpointId,
        TEvent payload,
        WebhookPartitionKey partitionKey,
        CancellationToken cancellationToken = default)
        where TEvent : IWebhookEvent;

    /// <summary>
    /// Re-enqueues an existing dead-lettered or failed job for immediate reprocessing.
    /// </summary>
    /// <param name="jobId">The unique identifier of the job to replay.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A delivery handle for the replayed job.</returns>
    Task<WebhookDeliveryHandle> ReplayAsync(WebhookJobId jobId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends an immediate diagnostic ping request to verify endpoint reachability, TLS handshake, and signature verification.
    /// </summary>
    /// <param name="endpointId">The target endpoint identifier to test.</param>
    /// <param name="cancellationToken">A token to observe for cancellation requests.</param>
    /// <returns>A diagnostic result containing reachability status, response code, and measured round-trip latency.</returns>
    Task<WebhookPingResult> PingAsync(WebhookEndpointId endpointId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents the comprehensive diagnostic outcome of an outbound webhook ping delivery.
/// </summary>
public sealed record WebhookPingResult {
    /// <summary>Gets a value indicating whether the ping succeeded with a 2xx HTTP response.</summary>
    public bool IsSuccess { get; }

    /// <summary>Gets the HTTP status code returned by the destination target, or null on network/DNS failure.</summary>
    public int? StatusCode { get; }

    /// <summary>Gets the total round-trip latency of the ping delivery.</summary>
    public TimeSpan Latency { get; }

    /// <summary>Gets the IP address the destination hostname resolved to.</summary>
    public string? ResolvedIpAddress { get; }

    /// <summary>Gets the response payload snippet returned by the destination target.</summary>
    public string? ResponseBodySnippet { get; }

    /// <summary>Gets diagnostic error details if the ping delivery failed.</summary>
    public string? ErrorMessage { get; }

    /// <summary>Gets the unique test identifier generated for this ping execution.</summary>
    public string PingId { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="WebhookPingResult"/> record with basic outcome parameters.
    /// </summary>
    /// <param name="isSuccess">Indicates whether the ping delivery succeeded.</param>
    /// <param name="statusCode">The HTTP status code returned.</param>
    /// <param name="latency">The measured round-trip latency.</param>
    /// <param name="errorMessage">Error details if failed.</param>
    public WebhookPingResult(
        bool isSuccess,
        int? statusCode,
        TimeSpan latency,
        string? errorMessage)
        : this(isSuccess, statusCode, latency, null, null, errorMessage, string.Empty) {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="WebhookPingResult"/> record with all diagnostic parameters.
    /// </summary>
    /// <param name="isSuccess">Indicates whether the ping delivery succeeded.</param>
    /// <param name="statusCode">The HTTP status code returned.</param>
    /// <param name="latency">The measured round-trip latency.</param>
    /// <param name="resolvedIpAddress">The resolved target IP address.</param>
    /// <param name="responseBodySnippet">The response body snippet.</param>
    /// <param name="errorMessage">Error details if failed.</param>
    /// <param name="pingId">The unique ping identifier.</param>
    public WebhookPingResult(
        bool isSuccess,
        int? statusCode,
        TimeSpan latency,
        string? resolvedIpAddress,
        string? responseBodySnippet,
        string? errorMessage,
        string pingId) {
        this.IsSuccess = isSuccess;
        this.StatusCode = statusCode;
        this.Latency = latency;
        this.ResolvedIpAddress = resolvedIpAddress;
        this.ResponseBodySnippet = responseBodySnippet;
        this.ErrorMessage = errorMessage;
        this.PingId = pingId;
    }
}

/// <summary>
/// Built-in diagnostic event payload sent during endpoint ping operations.
/// </summary>
[WebhookEvent("webhook.ping")]
public sealed record WebhookPingEvent(
    string PingId,
    DateTimeOffset Timestamp) : IWebhookEvent;

/// <summary>
/// Extension methods for <see cref="IWebhookDispatcher"/> providing convenient dispatch overloads.
/// </summary>
public static class WebhookDispatcherExtensions {
    /// <summary>
    /// Dispatches a webhook event defaulting the partition key to the target <paramref name="endpointId"/>.
    /// </summary>
    /// <typeparam name="TEvent">The type of the event being dispatched.</typeparam>
    /// <param name="dispatcher">The dispatcher instance.</param>
    /// <param name="endpointId">The identifier of the target endpoint.</param>
    /// <param name="payload">The event payload to dispatch.</param>
    /// <param name="cancellationToken">A token to observe for cancellation requests.</param>
    /// <returns>A delivery handle containing the scheduled job identifier.</returns>
    public static Task<WebhookDeliveryHandle> DispatchAsync<TEvent>(
        this IWebhookDispatcher dispatcher,
        WebhookEndpointId endpointId,
        TEvent payload,
        CancellationToken cancellationToken = default)
        where TEvent : IWebhookEvent {
        Preca.ThrowIfNull(dispatcher);
        return dispatcher.DispatchAsync(endpointId, payload, WebhookPartitionKey.From(endpointId), cancellationToken);
    }
}