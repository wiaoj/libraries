using Microsoft.AspNetCore.Http;
using System.Diagnostics.CodeAnalysis;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Wiaoj.Webhooks.AspNetCore;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Defines a strategy for extracting the wire-format event discriminator name from an incoming HTTP request.
/// </summary>
public interface IWebhookEventDiscriminatorExtractor {
    /// <summary>
    /// Attempts to extract the event discriminator name from request headers or raw payload bytes.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="rawBody">The raw UTF-8 request body bytes.</param>
    /// <param name="eventName">When this method returns, contains the extracted event name if successful; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> if an event discriminator was successfully extracted; otherwise, <see langword="false"/>.</returns>
    bool TryExtractEventName(HttpContext context, ReadOnlySpan<byte> rawBody, [NotNullWhen(true)] out string? eventName);
}