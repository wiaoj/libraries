#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Wiaoj.Webhooks;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Exception thrown when an outbound webhook delivery request is blocked by SSRF (Server-Side Request Forgery) protection
/// due to resolving to a prohibited local, private, loopback, or cloud metadata network address.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="WebhookSsrfBlockedException"/> class with an error message.
/// </remarks>
/// <param name="message">The message that describes the SSRF violation.</param>
public sealed class WebhookSsrfBlockedException(string message) : Exception(message);