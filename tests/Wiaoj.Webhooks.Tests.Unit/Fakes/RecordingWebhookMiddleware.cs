namespace Wiaoj.Webhooks.Tests.Unit.Fakes;

internal sealed class RecordingWebhookMiddleware(string name, List<string> executionLog) : IWebhookMiddleware {
    public async Task InvokeAsync(WebhookDeliveryContext context, WebhookDelegate next, CancellationToken cancellationToken = default) {
        executionLog.Add($"{name}:before");
        await next(context, cancellationToken);
        executionLog.Add($"{name}:after");
    }
}