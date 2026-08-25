using Wiaoj.Webhooks.Publishing.Internal;

namespace Wiaoj.Webhooks.Publishing.Tests.Unit.Matching;

[Trait("Category", "Unit")]
[Trait("Feature", "Publishing")]
[Trait("Component", "ContentFilterEvaluator")]
public sealed class ContentFilterEvaluatorTests {
    private readonly SimpleContentFilterEvaluator _evaluator = new();

    private sealed record TestOrderPayload(string OrderId, decimal Amount, string Currency, int ItemCount, bool IsVip);

    // ────────────────────────────────────────────────────────────────────────
    // 1. NUMERICAL COMPARISONS (>, >=, <, <=, ==, !=)
    // ────────────────────────────────────────────────────────────────────────

    public sealed class NumericalComparisons {
        private readonly SimpleContentFilterEvaluator _evaluator = new();

        [Theory]
        [InlineData("Amount > 100", true)]
        [InlineData("Amount >= 150", true)]
        [InlineData("Amount >= 150.00", true)]
        [InlineData("Amount > 150", false)]
        [InlineData("Amount < 200", true)]
        [InlineData("Amount <= 150", true)]
        [InlineData("Amount <= 100", false)]
        [InlineData("Amount == 150", true)]
        [InlineData("Amount != 150", false)]
        [InlineData("ItemCount >= 3", true)]
        [InlineData("ItemCount < 3", false)]
        public void Evaluate_CalculatesNumericalOperatorsCorrectly(string expression, bool expected) {
            TestOrderPayload payload = new("ORD-1", 150.00m, "USD", 3, true);
            bool result = this._evaluator.Evaluate(expression, payload);
            Assert.Equal(expected, result);
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    // 2. STRING EQUALITY & CASE INSENSITIVITY
    // ────────────────────────────────────────────────────────────────────────

    public sealed class StringComparisons {
        private readonly SimpleContentFilterEvaluator _evaluator = new();

        [Theory]
        [InlineData("Currency == 'USD'", true)]
        [InlineData("Currency == 'usd'", true)] // Case-insensitive
        [InlineData("Currency == \"USD\"", true)] // Double quotes support
        [InlineData("Currency != 'EUR'", true)]
        [InlineData("Currency == 'EUR'", false)]
        [InlineData("OrderId == 'ORD-999'", true)]
        [InlineData("OrderId != 'ORD-999'", false)]
        public void Evaluate_CalculatesStringOperatorsCorrectly(string expression, bool expected) {
            TestOrderPayload payload = new("ORD-999", 50m, "USD", 1, false);
            bool result = this._evaluator.Evaluate(expression, payload);
            Assert.Equal(expected, result);
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    // 3. LOGICAL CONJUNCTION (AND / &&)
    // ────────────────────────────────────────────────────────────────────────

    public sealed class LogicalConjunctions {
        private readonly SimpleContentFilterEvaluator _evaluator = new();

        [Theory]
        [InlineData("Amount >= 100 && Currency == 'USD'", true)]
        [InlineData("Amount >= 100 AND Currency == 'USD'", true)]
        [InlineData("Amount >= 500 && Currency == 'USD'", false)] // Amount fails
        [InlineData("Amount >= 100 && Currency == 'EUR'", false)] // Currency fails
        [InlineData("Amount >= 100 && Currency == 'USD' && ItemCount == 5", true)] // 3 conditions
        [InlineData("Amount >= 100 && Currency == 'USD' && ItemCount == 10", false)]
        public void Evaluate_CalculatesMultiClauseConjunctionsCorrectly(string expression, bool expected) {
            TestOrderPayload payload = new("ORD-COMPOUND", 250m, "USD", 5, true);
            bool result = this._evaluator.Evaluate(expression, payload);
            Assert.Equal(expected, result);
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    // 4. FAST-PATH & EDGE CASES
    // ────────────────────────────────────────────────────────────────────────

    public sealed class FastPathAndEdgeCases {
        private readonly SimpleContentFilterEvaluator _evaluator = new();

        [Fact]
        public void Evaluate_WhenExpressionIsNullOrEmpty_ReturnsTrueImmediately() {
            TestOrderPayload payload = new("ORD-1", 10m, "USD", 1, false);

            Assert.True(this._evaluator.Evaluate(null, payload));
            Assert.True(this._evaluator.Evaluate("", payload));
            Assert.True(this._evaluator.Evaluate("   ", payload));
        }

        [Fact]
        public void Evaluate_WhenPropertyDoesNotExistOnPayload_ReturnsFalse() {
            TestOrderPayload payload = new("ORD-1", 10m, "USD", 1, false);

            bool result = this._evaluator.Evaluate("NonExistentProperty == 'Value'", payload);
            Assert.False(result);
        }
    }
}