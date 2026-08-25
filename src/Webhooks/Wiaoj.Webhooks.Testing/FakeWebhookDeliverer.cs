namespace Wiaoj.Webhooks.Testing;

/// <summary>
/// Test double of <see cref="IWebhookDeliverer"/> recording outbound contexts and returning queued outcomes.
/// </summary>
public sealed class FakeWebhookDeliverer : IWebhookDeliverer {
    private readonly Lock _gate = new();
    private readonly Queue<WebhookDeliveryResult> _results;
    private readonly List<WebhookDeliveryContext> _receivedContexts = [];

    /// <summary>Gets all delivery contexts captured by the deliverer.</summary>
    public IReadOnlyList<WebhookDeliveryContext> ReceivedContexts {
        get {
            lock(this._gate) {
                return [.. this._receivedContexts];
            }
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FakeWebhookDeliverer"/> class with default 200 OK results.
    /// </summary>
    public FakeWebhookDeliverer() : this([WebhookDeliveryResult.Success(200, "{}")]) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="FakeWebhookDeliverer"/> class with a queued sequence of results.
    /// </summary>
    /// <param name="results">The sequence of delivery outcomes to return.</param>
    public FakeWebhookDeliverer(params WebhookDeliveryResult[] results) {
        Preca.ThrowIfNull(results);
        this._results = new Queue<WebhookDeliveryResult>(
            results.Length == 0 ? [WebhookDeliveryResult.Success(200, "{}")] : results);
    }

    /// <inheritdoc/>
    public Task<WebhookDeliveryResult> DeliverAsync(WebhookDeliveryContext context, CancellationToken cancellationToken = default) {
        Preca.ThrowIfNull(context);

        lock(this._gate) {
            this._receivedContexts.Add(context);
            WebhookDeliveryResult result = this._results.Count > 1 ? this._results.Dequeue() : this._results.Peek();
            return Task.FromResult(result);
        }
    }

    /// <summary>Clears all recorded contexts.</summary>
    public void Clear() {
        lock(this._gate) {
            this._receivedContexts.Clear();
        }
    }
}