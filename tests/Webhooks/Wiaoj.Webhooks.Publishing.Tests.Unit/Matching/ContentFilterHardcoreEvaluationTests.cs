using Wiaoj.Webhooks.Publishing.Internal;

namespace Wiaoj.Webhooks.Publishing.Tests.Unit.Matching;

[Trait("Category", "Unit")]
[Trait("Feature", "Publishing")]
[Trait("Component", "ContentFilterHardcore")]
public sealed class ContentFilterHardcoreEvaluationTests { 

    public enum OrderStatus {
        Draft,
        Processing,
        Completed,
        Cancelled
    }

    private sealed record ComplexOrderPayload(
        Guid OrderId,
        decimal Balance,
        double TaxRate,
        OrderStatus Status,
        string? CustomerNote,
        bool IsExpressDelivery);

    public sealed class TheSpecialTypesAndNulls {
        private readonly SimpleContentFilterEvaluator _evaluator = new();

        [Fact]
        public void Evaluate_HandlesGuidComparison_Successfully() {
            Guid targetGuid = Guid.Parse("01918a56-b3a2-7289-b00e-395f6170d1ab");
            ComplexOrderPayload payload = new(targetGuid, 100m, 0.18, OrderStatus.Completed, "Urgent", true);

            bool matchTrue = this._evaluator.Evaluate($"OrderId == '{targetGuid}'", payload);
            bool matchFalse = this._evaluator.Evaluate($"OrderId != '{targetGuid}'", payload);

            Assert.True(matchTrue);
            Assert.False(matchFalse);
        }

        [Fact]
        public void Evaluate_HandlesEnumComparison_CaseInsensitively() {
            ComplexOrderPayload payload = new(Guid.NewGuid(), 100m, 0.18, OrderStatus.Processing, null, false);

            bool matchEnumUpper = this._evaluator.Evaluate("Status == 'PROCESSING'", payload);
            bool matchEnumLower = this._evaluator.Evaluate("Status == 'processing'", payload);
            bool matchEnumMismatch = this._evaluator.Evaluate("Status == 'Completed'", payload);

            Assert.True(matchEnumUpper);
            Assert.True(matchEnumLower);
            Assert.False(matchEnumMismatch);
        }

        [Fact]
        public void Evaluate_HandlesNullPropertyValues_Gracefully() {
            ComplexOrderPayload payloadWithNull = new(Guid.NewGuid(), 100m, 0.18, OrderStatus.Draft, null, false);
            ComplexOrderPayload payloadWithValue = new(Guid.NewGuid(), 100m, 0.18, OrderStatus.Draft, "Deliver after 5pm", false);

            Assert.True(this._evaluator.Evaluate("CustomerNote == null", payloadWithNull));
            Assert.True(this._evaluator.Evaluate("CustomerNote != 'null'", payloadWithValue));
            Assert.False(this._evaluator.Evaluate("CustomerNote == 'Deliver after 5pm'", payloadWithNull));
        }

        [Fact]
        public void Evaluate_HandlesBooleanComparison_Correctly() {
            ComplexOrderPayload expressOrder = new(Guid.NewGuid(), 100m, 0.18, OrderStatus.Draft, null, true);

            Assert.True(this._evaluator.Evaluate("IsExpressDelivery == true", expressOrder));
            Assert.True(this._evaluator.Evaluate("IsExpressDelivery == 'TRUE'", expressOrder));
            Assert.False(this._evaluator.Evaluate("IsExpressDelivery == false", expressOrder));
        }
    }

    public sealed class TheNegativeAndFloatingNumbers {
        private readonly SimpleContentFilterEvaluator _evaluator = new();

        [Fact]
        public void Evaluate_HandlesNegativeNumbers_Correctly() {
            ComplexOrderPayload negativeBalance = new(Guid.NewGuid(), -45.50m, 0.18, OrderStatus.Draft, null, false);

            Assert.True(this._evaluator.Evaluate("Balance < 0", negativeBalance));
            Assert.True(this._evaluator.Evaluate("Balance <= -45.50", negativeBalance));
            Assert.True(this._evaluator.Evaluate("Balance > -50.00", negativeBalance));
            Assert.False(this._evaluator.Evaluate("Balance >= 0", negativeBalance));
        }

        [Fact]
        public void Evaluate_HandlesFloatingPointFractions_Accurately() {
            ComplexOrderPayload taxPayload = new(Guid.NewGuid(), 100m, 0.185, OrderStatus.Draft, null, false);

            Assert.True(this._evaluator.Evaluate("TaxRate > 0.18", taxPayload));
            Assert.True(this._evaluator.Evaluate("TaxRate >= 0.185", taxPayload));
            Assert.False(this._evaluator.Evaluate("TaxRate > 0.185", taxPayload));
        }
    }

    public sealed class TheMalformedSyntaxResilience {
        private readonly SimpleContentFilterEvaluator _evaluator = new();

        [Theory]
        // ── 1. Incomplete / Dangling Operators ──
        [InlineData("Amount >=")]                        // Missing right-hand value
        [InlineData("== 'USD'")]                         // Missing left-hand property
        [InlineData(" == 123")]                          // Missing left-hand property with space
        [InlineData("&&")]                               // Lone logical operator
        [InlineData("AND")]                              // Lone logical keyword
        [InlineData("Amount >= 100 &&")]                 // Trailing logical AND
        [InlineData("&& Amount >= 100")]                 // Leading logical AND
        [InlineData("Amount >= 100 AND")]                // Trailing logical keyword
        [InlineData("AND Amount >= 100")]                // Leading logical keyword
        [InlineData("Amount >= 100 && && IsExpressDelivery == true")] // Double logical AND
        [InlineData("Amount >= 100 AND AND IsExpressDelivery == true")] // Double keyword AND

        // ── 2. Unsupported / Invalid Operators ──
        [InlineData(">> 100")]                           // Invalid operator syntax
        [InlineData("Amount = 100")]                     // Single '=' is assignment, not equality '=='
        [InlineData("Amount ! 100")]                     // Unary NOT instead of comparison operator
        [InlineData("Amount <> 100")]                    // SQL-style inequality (we only support '!=')
        [InlineData("Amount === 100")]                   // JavaScript strict equality
        [InlineData("Amount !== 100")]                   // JavaScript strict inequality

        // ── 3. Unmatched Quotes & Corrupted Literals ──
        [InlineData("CustomerNote == 'Deliver")]         // Unclosed single quote
        [InlineData("CustomerNote == \"Deliver")]        // Unclosed double quote
        [InlineData("CustomerNote == Deliver'")]         // Missing opening quote
        [InlineData("CustomerNote == 'Deliver'extra")]   // Trailing garbage after quote

        // ── 4. Invalid Property Identifiers (LHS) ──
        [InlineData("123Amount >= 100")]                 // Property starts with a digit
        [InlineData("$Amount >= 100")]                   // Property starts with invalid symbol
        [InlineData("Amount-Total >= 100")]              // Hyphenated identifier
        [InlineData("null == 123")]                      // Null literal used as property name
        [InlineData("'' == 123")]                        // Empty string literal as property name
        [InlineData("' ' == 123")]                       // Whitespace literal as property name
        [InlineData("true == 123")]                      // Boolean literal as property name
        [InlineData("InvalidPropertyThatDoesNotExist == 123")] // Non-existent property

        // ── 5. Malformed Numeric Formats (RHS) ──
        [InlineData("Amount >= 100.50.25")]              // Multiple decimal points
        [InlineData("Amount >= 100abc")]                 // Unquoted alphanumeric garbage
        [InlineData("Amount >= --100")]                  // Double negative signs
        [InlineData("Amount >= +")]                      // Lone sign without digits

        // ── 6. Injection & Special Payload Attacks ──
        [InlineData("; DROP TABLE Subscriptions; --")]   // SQL Injection payload
        [InlineData("<script>alert(1)</script>")]        // XSS script injection
        [InlineData("{{ 7 * 7 }}")]                      // Template expression injection
        public void Evaluate_WhenSyntaxIsMalformed_ReturnsFalse_WithoutThrowingExceptions(string malformedExpression) {
            ComplexOrderPayload payload = new(Guid.NewGuid(), 100m, 0.18, OrderStatus.Draft, "Deliver", false);

            bool result = this._evaluator.Evaluate(malformedExpression, payload);

            Assert.False(result);
        }

        [Theory]
        // ── 1. JavaScript Strict Equality & Inequality Leaks ──
        [InlineData("CustomerNote === 100", "= 100")]            // '===' leaves '= 100' on RHS
        [InlineData("CustomerNote !== 100", "= 100")]            // '!==' leaves '= 100' on RHS

        // ── 2. Bitshift & Repeated Comparison Operators ──
        [InlineData("CustomerNote >> 50", "> 50")]               // '>>' leaves '> 50' on RHS
        [InlineData("CustomerNote << 20", "< 20")]               // '<<' leaves '< 20' on RHS
        [InlineData("CustomerNote >== 0", "== 0")]               // '>==' leaves '== 0' on RHS
        [InlineData("CustomerNote <== 0", "== 0")]               // '<==' leaves '== 0' on RHS

        // ── 3. Stacked & Compounded Comparison Operators ──
        [InlineData("CustomerNote >= <= 100", "<= 100")]         // '>= <=' leaves '<= 100' on RHS
        [InlineData("CustomerNote <= >= 100", ">= 100")]         // '<= >=' leaves '>= 100' on RHS
        [InlineData("CustomerNote == != 100", "!= 100")]         // '== !=' leaves '!= 100' on RHS
        [InlineData("CustomerNote != == 100", "== 100")]         // '!= ==' leaves '== 100' on RHS
        [InlineData("CustomerNote == ! 42", "! 42")]             // '== !' leaves '! 42' on RHS
        public void Evaluate_WhenPropertyValueMatchesBleedingOperatorArtifacts_ReturnsFalse(string malformedExpression, string rawPropertyValue) {
            ComplexOrderPayload payload = new(Guid.NewGuid(), 100m, 0.18, OrderStatus.Draft, rawPropertyValue, false);

            bool result = this._evaluator.Evaluate(malformedExpression, payload);

            Assert.False(result);
        }
    }
}