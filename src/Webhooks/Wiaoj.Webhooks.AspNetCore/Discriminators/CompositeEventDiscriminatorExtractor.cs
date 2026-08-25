using Microsoft.AspNetCore.Http;
using System.Diagnostics.CodeAnalysis;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Wiaoj.Webhooks.AspNetCore;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Composite discriminator extractor evaluating multiple child extraction strategies in sequential order.
/// </summary>
public sealed class CompositeEventDiscriminatorExtractor : IWebhookEventDiscriminatorExtractor {
    private readonly IReadOnlyList<IWebhookEventDiscriminatorExtractor> _extractors;

    /// <summary>
    /// Gets the default composite extractor inspecting standard <c>Webhook-Event</c> header first,
    /// followed by root <c>"type"</c> and <c>"event"</c> JSON payload properties.
    /// </summary>
    public static CompositeEventDiscriminatorExtractor Default { get; } = new(
        new HeaderEventDiscriminatorExtractor(WebhookHeaderNames.WebhookEvent),
        new JsonPropertyEventDiscriminatorExtractor("type"),
        new JsonPropertyEventDiscriminatorExtractor("event"));

    /// <summary>
    /// Initializes a new instance of the <see cref="CompositeEventDiscriminatorExtractor"/> class with a read-only list.
    /// </summary>
    /// <param name="extractors">The ordered collection of discriminator extractors to evaluate.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="extractors"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="extractors"/> is empty.</exception>
    public CompositeEventDiscriminatorExtractor(params IReadOnlyList<IWebhookEventDiscriminatorExtractor> extractors) {
        Preca.ThrowIfNull(extractors);
        Preca.ThrowIfLessThan(extractors.Count, 1);
        this._extractors = extractors;
    }

    /// <inheritdoc/>
    public bool TryExtractEventName(HttpContext context, ReadOnlySpan<byte> rawBody, [NotNullWhen(true)] out string? eventName) {
        Preca.ThrowIfNull(context);

        for(int i = 0; i < this._extractors.Count; i++) {
            if(this._extractors[i].TryExtractEventName(context, rawBody, out eventName)) {
                return true;
            }
        }

        eventName = null;
        return false;
    }
}