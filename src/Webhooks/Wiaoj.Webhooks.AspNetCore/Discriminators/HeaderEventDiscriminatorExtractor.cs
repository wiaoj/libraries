using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using System.Diagnostics.CodeAnalysis;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Wiaoj.Webhooks.AspNetCore;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Extracts the event discriminator name directly from a specified HTTP request header.
/// </summary>
public sealed class HeaderEventDiscriminatorExtractor : IWebhookEventDiscriminatorExtractor {
    /// <summary>
    /// Gets the HTTP header name configured for discriminator extraction.
    /// </summary>
    public string HeaderName { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="HeaderEventDiscriminatorExtractor"/> class.
    /// </summary>
    /// <param name="headerName">The HTTP header name to inspect (e.g. <c>"X-GitHub-Event"</c>, <c>"Webhook-Event"</c>).</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="headerName"/> is <see langword="null"/>, empty, or whitespace.</exception>
    public HeaderEventDiscriminatorExtractor(string headerName) {
        Preca.ThrowIfNullOrWhiteSpace(headerName);
        this.HeaderName = headerName;
    }

    /// <inheritdoc/>
    public bool TryExtractEventName(
        HttpContext context,
        ReadOnlySpan<byte> rawBody,
        [NotNullWhen(true)] out string? eventName) {
        Preca.ThrowIfNull(context);

        if(context.Request.Headers.TryGetValue(this.HeaderName, out StringValues values) && values.Count == 1) {
            string? firstValue = values[0];
            if(!string.IsNullOrWhiteSpace(firstValue)) {
                eventName = firstValue.Trim();
                return true;
            }
        }

        eventName = null;
        return false;
    }
}