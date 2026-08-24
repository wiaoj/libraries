namespace Wiaoj.Webhooks.Tests.Unit.Fakes;

internal sealed class FakeWebhookDeliverer(params WebhookDeliveryResult[] results) : IWebhookDeliverer {
    private readonly Lock _gate = new();
    private readonly Queue<WebhookDeliveryResult> _results = new( 
        results.Length == 0 
            ? [WebhookDeliveryResult.Success(200, "{}")] 
            : results);
    private readonly List<WebhookDeliveryContext> _receivedContexts = [];

    public IReadOnlyList<WebhookDeliveryContext> ReceivedContexts {
        get {
            lock(this._gate) {
                return [.. this._receivedContexts];
            }
        }
    }

    public Task<WebhookDeliveryResult> DeliverAsync(WebhookDeliveryContext context, CancellationToken cancellationToken = default) {
        lock(this._gate) {
            this._receivedContexts.Add(context);
            WebhookDeliveryResult result = this._results.Count > 1 ? this._results.Dequeue() : this._results.Peek();
            return Task.FromResult(result);
        }
    }
}