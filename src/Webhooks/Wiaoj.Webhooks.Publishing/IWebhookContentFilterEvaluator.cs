namespace Wiaoj.Webhooks.Publishing;

/// <summary>
/// Evaluates content-based filter expressions against domain event payloads.
/// </summary>
public interface IWebhookContentFilterEvaluator {
    /// <summary>
    /// Evaluates whether an event payload satisfies the specified filter expression.
    /// </summary>
    /// <typeparam name="TPayload">The type of the domain event payload.</typeparam>
    /// <param name="filterExpression">The filter expression string (e.g. <c>"amount &gt;= 100 &amp;&amp; currency == 'USD'"</c>).</param>
    /// <param name="payload">The typed domain event payload instance.</param>
    /// <returns><see langword="true"/> if the payload satisfies the filter expression or if the expression is empty; otherwise, <see langword="false"/>.</returns>
    bool Evaluate<TPayload>(string? filterExpression, TPayload payload);
}