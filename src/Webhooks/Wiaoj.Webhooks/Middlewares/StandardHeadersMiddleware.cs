using System.Reflection;

#pragma warning disable IDE0130
namespace Wiaoj.Webhooks;
#pragma warning restore IDE0130

/// <summary>
/// Pipeline middleware that injects standardized RFC and industry webhook metadata headers
/// (<c>User-Agent</c>, <c>Webhook-Id</c>, <c>Webhook-Event</c>, <c>Webhook-Attempt</c>)
/// and endpoint-specific custom static headers into the outbound delivery context.
/// </summary>
internal sealed class StandardHeadersMiddleware : IWebhookMiddleware {
    private static readonly string DefaultUserAgent =
        $"Wiaoj-Webhooks/{typeof(StandardHeadersMiddleware).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion?.Split('+')[0]
                ?? typeof(StandardHeadersMiddleware).Assembly.GetName().Version?.ToString(3)
                ?? "1.0.0"}";

    private readonly StandardHeadersOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="StandardHeadersMiddleware"/> class using default configuration options.
    /// </summary>
    public StandardHeadersMiddleware() : this(new StandardHeadersOptions()) {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StandardHeadersMiddleware"/> class with the specified configuration options.
    /// </summary>
    /// <param name="options">The configuration options controlling which headers are emitted and their custom header names.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> is <see langword="null"/>.</exception>
    public StandardHeadersMiddleware(StandardHeadersOptions options) {
        Preca.ThrowIfNull(options);
        this._options = options;
    }

    /// <inheritdoc />
    public async Task InvokeAsync(WebhookDeliveryContext context, WebhookDelegate next, CancellationToken cancellationToken = default) {
        Preca.ThrowIfNull(context);
        Preca.ThrowIfNull(next); 

        if(context.Endpoint.CustomHeaders is not null) {
            foreach(KeyValuePair<string, string> kvp in context.Endpoint.CustomHeaders) {
                context.SetHeader(kvp.Key, kvp.Value);
            }
        }

        if(this._options.IncludeWebhookId) {
            context.SetHeader(this._options.WebhookIdHeaderName, context.JobId.Value);
        }

        if(this._options.IncludeWebhookEvent) {
            context.SetHeader(this._options.WebhookEventHeaderName, context.EventType);
        }

        if(this._options.IncludeWebhookAttempt) {
            context.SetHeader(this._options.WebhookAttemptHeaderName, context.GetCurrentAttemptNumber().ToString());
        }

        if(this._options.IncludeUserAgent && !context.GetHeaders().ContainsKey(this._options.UserAgentHeaderName)) {
            context.SetHeader(this._options.UserAgentHeaderName, this._options.CustomUserAgent ?? DefaultUserAgent);
        }

        await next(context, cancellationToken).ConfigureAwait(false);
    }
}