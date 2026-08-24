namespace Wiaoj.Webhooks.Tests.Unit.Fakes;

internal sealed class ShortCircuitingWebhookMiddleware(List<string> executionLog) : IWebhookMiddleware {
    // Deliberately never calls `next` — simulates e.g. a rate-limit rejection.
    public Task InvokeAsync(WebhookDeliveryContext context, WebhookDelegate next, CancellationToken cancellationToken = default) {
        executionLog.Add("short-circuit");
        return Task.CompletedTask;
    }
}