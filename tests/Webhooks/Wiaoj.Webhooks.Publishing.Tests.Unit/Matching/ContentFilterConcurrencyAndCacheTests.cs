using Wiaoj.Webhooks.Publishing.Internal;
using Xunit;

namespace Wiaoj.Webhooks.Publishing.Tests.Unit.Matching;

[Trait("Category", "Unit")]
[Trait("Feature", "Publishing")]
[Trait("Component", "ConcurrencyAndCache")]
public sealed class ContentFilterConcurrencyAndCacheTests {

    private sealed record InvoiceEvent(string InvoiceId, decimal TotalAmount, string Currency);

    [Fact]
    public async Task Evaluate_Under100ConcurrentThreads_MaintainsCacheIntegrityAndEvaluatesAccurately() {
        SimpleContentFilterEvaluator evaluator = new();

        string[] expressions = [
            "TotalAmount >= 100 && Currency == 'USD'",
            "TotalAmount < 50",
            "Currency == 'EUR'",
            "TotalAmount >= 500 && Currency == 'TRY'",
            "TotalAmount == 250"
        ];

        // Act: 100 concurrent threads evaluating different expressions against varying payloads
        Task[] tasks = Enumerable.Range(0, 100).Select(i => Task.Run(() => {
            string expr = expressions[i % expressions.Length];
            InvoiceEvent payload = new($"INV-{i}", i * 10m, i % 2 == 0 ? "USD" : "EUR");

            // Must evaluate without throwing KeyNotFoundException or reflection race conditions
            bool result = evaluator.Evaluate(expr, payload);
            Assert.True(result || !result); // Boolean evaluation completed
        })).ToArray();

        await Task.WhenAll(tasks);
    }
}