namespace Wiaoj.Webhooks.Exceptions;

/// <summary>
/// Exception thrown when an outbound webhook delivery request is blocked by SSRF (Server-Side Request Forgery) protection
/// due to resolving to a prohibited local, private, loopback, or cloud metadata network address.
/// </summary>
public sealed class WebhookSsrfBlockedException : Exception {
    /// <summary>
    /// Initializes a new instance of the <see cref="WebhookSsrfBlockedException"/> class with an error message.
    /// </summary>
    /// <param name="message">The message that describes the SSRF violation.</param>
    public WebhookSsrfBlockedException(string message) : base(message) {
    }
}