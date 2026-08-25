using System.Runtime.CompilerServices;

namespace Wiaoj.Webhooks.Publishing.Internal;

/// <summary>
/// Composite subscription matcher orchestrating topic pattern matching and content-based filter evaluation.
/// </summary>
internal sealed class CompositeSubscriptionMatcher : IWebhookSubscriptionMatcher {
    private readonly IWebhookTopicMatcher _topicMatcher;
    private readonly IWebhookContentFilterEvaluator _contentEvaluator;

    /// <summary>
    /// Initializes a new instance of the <see cref="CompositeSubscriptionMatcher"/> class with default topic matcher and content evaluator.
    /// </summary>
    public CompositeSubscriptionMatcher() : this(new WildcardTopicMatcher(), new SimpleContentFilterEvaluator()) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="CompositeSubscriptionMatcher"/> class with a custom topic matcher and default content evaluator.
    /// </summary>
    /// <param name="topicMatcher">The topic pattern matcher implementation.</param>
    public CompositeSubscriptionMatcher(IWebhookTopicMatcher topicMatcher)
        : this(topicMatcher, new SimpleContentFilterEvaluator()) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="CompositeSubscriptionMatcher"/> class with default topic matcher and a custom content evaluator.
    /// </summary>
    /// <param name="contentEvaluator">The content filter evaluator implementation.</param>
    public CompositeSubscriptionMatcher(IWebhookContentFilterEvaluator contentEvaluator)
        : this(new WildcardTopicMatcher(), contentEvaluator) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="CompositeSubscriptionMatcher"/> class with custom matchers.
    /// </summary>
    /// <param name="topicMatcher">The topic pattern matcher implementation.</param>
    /// <param name="contentEvaluator">The content filter evaluator implementation.</param>
    public CompositeSubscriptionMatcher(
        IWebhookTopicMatcher topicMatcher,
        IWebhookContentFilterEvaluator contentEvaluator) {
        Preca.ThrowIfNull(topicMatcher);
        Preca.ThrowIfNull(contentEvaluator);

        this._topicMatcher = topicMatcher;
        this._contentEvaluator = contentEvaluator;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Matches<TEvent>(WebhookSubscription subscription, string eventName, TEvent payload) where TEvent : IWebhookEvent {
        Preca.ThrowIfNull(subscription);

        // 1. Evaluate topic pattern
        if(!this._topicMatcher.Matches(subscription.EventTypePattern, eventName)) {
            return false;
        }

        // 2. Evaluate content filter expression
        if(string.IsNullOrWhiteSpace(subscription.FilterExpression)) {
            return true;
        }

        return this._contentEvaluator.Evaluate(subscription.FilterExpression, payload);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Matches(string pattern, string eventName) {
        return this._topicMatcher.Matches(pattern, eventName);
    }
}