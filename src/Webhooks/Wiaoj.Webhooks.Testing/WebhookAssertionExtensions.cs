namespace Wiaoj.Webhooks.Testing;

/// <summary>
/// Fluent assertion extension methods for <see cref="FakeWebhookDispatcher"/>,
/// <see cref="FakeWebhookTransport"/>, and <see cref="FakeWebhookDeliverer"/>.
/// </summary>
public static class WebhookAssertionExtensions {

    // ────────────────────────────────────────────────────────────────────────
    // 1. DISPATCHER ASSERTIONS
    // ────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Asserts that an event of type <typeparamref name="TEvent"/> was dispatched at least once.
    /// </summary>
    /// <typeparam name="TEvent">The expected event payload type.</typeparam>
    /// <param name="dispatcher">The fake dispatcher instance.</param>
    /// <exception cref="InvalidOperationException">Thrown when no matching event was dispatched.</exception>
    public static void ShouldHaveDispatched<TEvent>(this FakeWebhookDispatcher dispatcher) where TEvent : IWebhookEvent {
        Preca.ThrowIfNull(dispatcher);

        if(!dispatcher.HasDispatched<TEvent>()) {
            throw new InvalidOperationException($"Expected event of type '{typeof(TEvent).Name}' to be dispatched, but no matching event was recorded.");
        }
    }

    /// <summary>
    /// Asserts that an event of type <typeparamref name="TEvent"/> was dispatched to the specified endpoint.
    /// </summary>
    /// <typeparam name="TEvent">The expected event payload type.</typeparam>
    /// <param name="dispatcher">The fake dispatcher instance.</param>
    /// <param name="endpointId">The target endpoint identifier.</param>
    /// <exception cref="InvalidOperationException">Thrown when no matching event was dispatched to the endpoint.</exception>
    public static void ShouldHaveDispatched<TEvent>(this FakeWebhookDispatcher dispatcher, WebhookEndpointId endpointId) where TEvent : IWebhookEvent {
        Preca.ThrowIfNull(dispatcher);

        bool matched = dispatcher.Calls.Any(c => c.EndpointId == endpointId && c.EventType == typeof(TEvent));
        if(!matched) {
            throw new InvalidOperationException($"Expected event '{typeof(TEvent).Name}' to be dispatched to endpoint '{endpointId.Value}', but no matching call was found.");
        }
    }

    /// <summary>
    /// Asserts that a specific event was dispatched to the target endpoint by matching custom criteria.
    /// </summary>
    /// <typeparam name="TEvent">The expected event payload type.</typeparam>
    /// <param name="dispatcher">The fake dispatcher instance.</param>
    /// <param name="endpointId">The target endpoint identifier.</param>
    /// <param name="predicate">The custom predicate validating the payload.</param>
    /// <exception cref="InvalidOperationException">Thrown when no matching call meets the predicate.</exception>
    public static void ShouldHaveDispatched<TEvent>(
        this FakeWebhookDispatcher dispatcher,
        WebhookEndpointId endpointId,
        Func<TEvent, bool> predicate) where TEvent : IWebhookEvent {

        Preca.ThrowIfNull(dispatcher);
        Preca.ThrowIfNull(predicate);

        bool matched = dispatcher.Calls.Any(c => c.EndpointId == endpointId && c.Payload is TEvent typed && predicate(typed));
        if(!matched) {
            throw new InvalidOperationException($"Expected event '{typeof(TEvent).Name}' matching predicate to be dispatched to endpoint '{endpointId.Value}', but no matching call was found.");
        }
    }

    /// <summary>
    /// Asserts that no dispatch calls were recorded for the specified endpoint.
    /// </summary>
    /// <param name="dispatcher">The fake dispatcher instance.</param>
    /// <param name="endpointId">The endpoint identifier expected to have no dispatches.</param>
    /// <exception cref="InvalidOperationException">Thrown when dispatches were recorded for the endpoint.</exception>
    public static void ShouldNotHaveDispatched(this FakeWebhookDispatcher dispatcher, WebhookEndpointId endpointId) {
        Preca.ThrowIfNull(dispatcher);

        int count = dispatcher.Calls.Count(c => c.EndpointId == endpointId);
        if(count > 0) {
            throw new InvalidOperationException($"Expected 0 dispatches for endpoint '{endpointId.Value}', but recorded {count} calls.");
        }
    }

    /// <summary>
    /// Asserts that the total number of dispatch calls equals the expected count.
    /// </summary>
    /// <param name="dispatcher">The fake dispatcher instance.</param>
    /// <param name="expectedCount">The expected number of dispatches.</param>
    /// <exception cref="InvalidOperationException">Thrown when call count does not match.</exception>
    public static void ShouldHaveDispatchCount(this FakeWebhookDispatcher dispatcher, int expectedCount) {
        Preca.ThrowIfNull(dispatcher);

        if(dispatcher.Calls.Count != expectedCount) {
            throw new InvalidOperationException($"Expected {expectedCount} total dispatches, but recorded {dispatcher.Calls.Count}.");
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    // 2. TRANSPORT ASSERTIONS
    // ────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Asserts that the transport recorded an enqueued job with the specified job ID.
    /// </summary>
    /// <param name="transport">The fake transport instance.</param>
    /// <param name="jobId">The unique job identifier.</param>
    /// <exception cref="InvalidOperationException">Thrown when the job was not enqueued.</exception>
    public static void ShouldHaveEnqueued(this FakeWebhookTransport transport, WebhookJobId jobId) {
        Preca.ThrowIfNull(transport);

        bool matched = transport.EnqueuedJobs.Any(j => j.Job.Id == jobId);
        if(!matched) {
            throw new InvalidOperationException($"Expected job '{jobId.Value}' to be enqueued in transport, but it was not found.");
        }
    }

    /// <summary>
    /// Asserts that the transport recorded an enqueued job targeting the specified endpoint.
    /// </summary>
    /// <param name="transport">The fake transport instance.</param>
    /// <param name="endpointId">The target endpoint identifier.</param>
    /// <exception cref="InvalidOperationException">Thrown when no matching job was found.</exception>
    public static void ShouldHaveEnqueued(this FakeWebhookTransport transport, WebhookEndpointId endpointId) {
        Preca.ThrowIfNull(transport);

        bool matched = transport.EnqueuedJobs.Any(j => j.Job.EndpointId == endpointId);
        if(!matched) {
            throw new InvalidOperationException($"Expected at least one job targeting endpoint '{endpointId.Value}' to be enqueued, but none was found.");
        }
    }

    /// <summary>
    /// Asserts that the total number of enqueued jobs in the transport equals the expected count.
    /// </summary>
    /// <param name="transport">The fake transport instance.</param>
    /// <param name="expectedCount">The expected count of enqueued jobs.</param>
    /// <exception cref="InvalidOperationException">Thrown when count does not match.</exception>
    public static void ShouldHaveEnqueuedCount(this FakeWebhookTransport transport, int expectedCount) {
        Preca.ThrowIfNull(transport);

        if(transport.EnqueuedJobs.Count != expectedCount) {
            throw new InvalidOperationException($"Expected {expectedCount} enqueued jobs, but recorded {transport.EnqueuedJobs.Count}.");
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    // 3. DELIVERER ASSERTIONS
    // ────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Asserts that the deliverer attempted transmission to the specified endpoint at least once.
    /// </summary>
    /// <param name="deliverer">The fake deliverer instance.</param>
    /// <param name="endpointId">The target endpoint identifier.</param>
    /// <exception cref="InvalidOperationException">Thrown when no delivery attempt was recorded.</exception>
    public static void ShouldHaveDeliveredTo(this FakeWebhookDeliverer deliverer, WebhookEndpointId endpointId) {
        Preca.ThrowIfNull(deliverer);

        bool matched = deliverer.ReceivedContexts.Any(c => c.Endpoint.Id == endpointId);
        if(!matched) {
            throw new InvalidOperationException($"Expected deliverer to attempt delivery to endpoint '{endpointId.Value}', but no context was captured.");
        }
    }

    /// <summary>
    /// Asserts that the total number of delivery attempts captured by the deliverer equals the expected count.
    /// </summary>
    /// <param name="deliverer">The fake deliverer instance.</param>
    /// <param name="expectedCount">The expected count of delivery attempts.</param>
    /// <exception cref="InvalidOperationException">Thrown when count does not match.</exception>
    public static void ShouldHaveDeliveryCount(this FakeWebhookDeliverer deliverer, int expectedCount) {
        Preca.ThrowIfNull(deliverer);

        if(deliverer.ReceivedContexts.Count != expectedCount) {
            throw new InvalidOperationException($"Expected {expectedCount} delivery attempts, but recorded {deliverer.ReceivedContexts.Count}.");
        }
    }
}