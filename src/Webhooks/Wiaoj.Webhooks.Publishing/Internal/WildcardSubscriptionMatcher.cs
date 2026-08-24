using System.Runtime.CompilerServices;

namespace Wiaoj.Webhooks.Publishing.Internal;

/// <summary>
/// High-performance, zero-allocation wildcard event pattern matcher supporting
/// prefix, suffix, universal wildcard, and exact matching without RegEx overhead.
/// </summary>
internal sealed class WildcardSubscriptionMatcher : IWebhookSubscriptionMatcher {
    private const char UniversalWildcard = '*';
    private const string PrefixWildcardSuffix = ".*"; // pattern "order.*"
    private const string SuffixWildcardPrefix = "*.";  // pattern "*.created"

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Matches<TEvent>(WebhookSubscription subscription, string eventName, TEvent payload) where TEvent : IWebhookEvent {
        Preca.ThrowIfNull(subscription);
        return Matches(subscription.EventTypePattern, eventName);
    }
     
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Matches(string pattern, string eventName) {
        Preca.ThrowIfNullOrWhiteSpace(pattern);
        Preca.ThrowIfNullOrWhiteSpace(eventName);

        ReadOnlySpan<char> patternSpan = pattern.AsSpan();
        ReadOnlySpan<char> eventSpan = eventName.AsSpan();

        // 1. Universal Wildcard ("*")
        if(patternSpan.Length == 1 && patternSpan[0] == UniversalWildcard)
            return true;

        // 2. Exact Match ("order.created" == "order.created")
        if(patternSpan.Equals(eventSpan, StringComparison.OrdinalIgnoreCase))
            return true;

        // 3. Prefix Wildcard ("order.*" matches "order.created", "order.paid")
        if(patternSpan.EndsWith(PrefixWildcardSuffix.AsSpan(), StringComparison.Ordinal)) {
            ReadOnlySpan<char> prefix = patternSpan[..^2];
            return eventSpan.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }

        // 4. Suffix Wildcard ("*.created" matches "order.created", "invoice.created")
        if(patternSpan.StartsWith(SuffixWildcardPrefix.AsSpan(), StringComparison.Ordinal)) {
            ReadOnlySpan<char> suffix = patternSpan[2..];
            return eventSpan.EndsWith(suffix, StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }
}