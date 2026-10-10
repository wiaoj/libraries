namespace Wiaoj.Webhooks;

/// <summary>
/// The ambient <see cref="WebhookCausality"/> of the code that is running, carried across <see langword="await"/>s.
/// </summary>
/// <remarks>
/// The inbound receiver opens a scope with the hop count and causal chain of the request for as long as its handler runs.
/// The dispatcher reads <see cref="Current"/> when it creates a job and stores it on the job, so the causality survives
/// the transport queue, retries and recovery. Open a scope yourself when a webhook's effects are handled outside
/// the receiver, for example by a message consumer.
/// </remarks>
public static class WebhookCausalityScope {
    private static readonly AsyncLocal<WebhookCausality?> AmbientCausality = new();

    /// <summary>
    /// Gets the causality of the current execution flow, or <see cref="WebhookCausality.None"/> outside any scope.
    /// </summary>
    public static WebhookCausality Current => AmbientCausality.Value ?? WebhookCausality.None;

    /// <summary>
    /// Makes <paramref name="causality"/> the ambient causality until the returned scope is disposed.
    /// </summary>
    /// <param name="causality">The causality of the webhook being handled.</param>
    /// <returns>A scope that restores the previous causality when disposed.</returns>
    public static IDisposable Begin(WebhookCausality causality) {
        WebhookCausality? previous = AmbientCausality.Value;
        AmbientCausality.Value = causality;
        return new Scope(previous);
    }

    private sealed class Scope(WebhookCausality? previous) : IDisposable {
        private int _disposed;

        public void Dispose() {
            if(Interlocked.Exchange(ref this._disposed, 1) == 0) {
                AmbientCausality.Value = previous;
            }
        }
    }
}
