namespace Wiaoj.Webhooks.Tests.Unit.Delivery;

[Trait("Category", "Unit")]
[Trait("Component", "Delivery")]
public sealed class WebhookDeliveryResultTests {

    // ────────────────────────────────────────────────────────────────────────
    // 1. DELIVERED SUBTYPE TESTS
    // ────────────────────────────────────────────────────────────────────────

    public sealed class TheDeliveredCase {
        [Fact]
        public void Constructor_WithStatusCodeOnly_SetsDefaultValues() {
            WebhookDeliveryResult.Delivered result = new(200);

            Assert.True(result.IsSuccess);
            Assert.Equal(200, result.StatusCode);
            Assert.Null(result.ResponseBody);
        }

        [Fact]
        public void Constructor_WithStatusCodeAndBody_SetsPropertiesCorrectly() {
            WebhookDeliveryResult.Delivered result = new(204, "{\"status\":\"ok\"}");

            Assert.True(result.IsSuccess);
            Assert.Equal(204, result.StatusCode);
            Assert.Equal("{\"status\":\"ok\"}", result.ResponseBody);
        }

        [Fact]
        public void Equality_ComparesByValue() {
            WebhookDeliveryResult.Delivered result1 = new(200, "body");
            WebhookDeliveryResult.Delivered result2 = new(200, "body");
            WebhookDeliveryResult.Delivered different = new(201, "body");

            Assert.Equal(result1, result2);
            Assert.NotEqual(result1, different);
            Assert.Equal(result1.GetHashCode(), result2.GetHashCode());
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    // 2. DEDUPLICATED SUBTYPE TESTS
    // ────────────────────────────────────────────────────────────────────────

    public sealed class TheDeduplicatedCase {
        [Fact]
        public void Constructor_SetsPropertiesCorrectly() {
            WebhookDeliveryResult.Deduplicated result = new("dedup_key_123");

            Assert.True(result.IsSuccess);
            Assert.Equal("dedup_key_123", result.DeduplicationKey);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Constructor_Throws_WhenKeyIsInvalid(string? invalidKey) {
            Assert.ThrowsAny<ArgumentException>(() => new WebhookDeliveryResult.Deduplicated(invalidKey!));
        }

        [Fact]
        public void Equality_ComparesByValue() {
            WebhookDeliveryResult.Deduplicated result1 = new("key_a");
            WebhookDeliveryResult.Deduplicated result2 = new("key_a");
            WebhookDeliveryResult.Deduplicated different = new("key_b");

            Assert.Equal(result1, result2);
            Assert.NotEqual(result1, different);
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    // 3. TRANSIENT FAILURE SUBTYPE TESTS
    // ────────────────────────────────────────────────────────────────────────

    public sealed class TheTransientFailureCase {
        [Fact]
        public void Constructor_WithErrorMessageOnly_DefaultsToGeneralReason() {
            WebhookDeliveryResult.TransientFailure result = new("Connection timed out");

            Assert.False(result.IsSuccess);
            Assert.Equal("Connection timed out", result.ErrorMessage);
            Assert.Null(result.StatusCode);
            Assert.Null(result.RetryAfter);
            Assert.Equal(TransientFailureReason.General, result.Reason);
            Assert.Null(result.Exception);
        }

        [Fact]
        public void Constructor_WithAllParameters_SetsPropertiesCorrectly() {
            HttpRequestException ex = new("Gateway timeout");
            TimeSpan retryAfter = TimeSpan.FromSeconds(30);

            WebhookDeliveryResult.TransientFailure result = new(
                "Gateway timeout",
                statusCode: 504,
                retryAfter: retryAfter,
                reason: TransientFailureReason.ServerUnavailable,
                exception: ex);

            Assert.False(result.IsSuccess);
            Assert.Equal("Gateway timeout", result.ErrorMessage);
            Assert.Equal(504, result.StatusCode);
            Assert.Equal(retryAfter, result.RetryAfter);
            Assert.Equal(TransientFailureReason.ServerUnavailable, result.Reason);
            Assert.Same(ex, result.Exception);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Constructor_Throws_WhenErrorMessageIsInvalid(string? invalidMessage) {
            Assert.ThrowsAny<ArgumentException>(() => new WebhookDeliveryResult.TransientFailure(invalidMessage!));
            Assert.ThrowsAny<ArgumentException>(() => new WebhookDeliveryResult.TransientFailure(invalidMessage!, 500));
            Assert.ThrowsAny<ArgumentException>(() => new WebhookDeliveryResult.TransientFailure(invalidMessage!, TransientFailureReason.General));
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    // 4. PERMANENT FAILURE SUBTYPE TESTS
    // ────────────────────────────────────────────────────────────────────────

    public sealed class ThePermanentFailureCase {
        [Fact]
        public void Constructor_WithErrorMessageOnly_DefaultsToGeneralReason() {
            WebhookDeliveryResult.PermanentFailure result = new("Endpoint disabled");

            Assert.False(result.IsSuccess);
            Assert.Equal("Endpoint disabled", result.ErrorMessage);
            Assert.Null(result.StatusCode);
            Assert.Equal(PermanentFailureReason.General, result.Reason);
        }

        [Fact]
        public void Constructor_WithAllParameters_SetsPropertiesCorrectly() {
            WebhookDeliveryResult.PermanentFailure result = new(
                "Unauthorized access",
                statusCode: 401,
                reason: PermanentFailureReason.DestinationRejected);

            Assert.False(result.IsSuccess);
            Assert.Equal("Unauthorized access", result.ErrorMessage);
            Assert.Equal(401, result.StatusCode);
            Assert.Equal(PermanentFailureReason.DestinationRejected, result.Reason);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Constructor_Throws_WhenErrorMessageIsInvalid(string? invalidMessage) {
            Assert.ThrowsAny<ArgumentException>(() => new WebhookDeliveryResult.PermanentFailure(invalidMessage!));
            Assert.ThrowsAny<ArgumentException>(() => new WebhookDeliveryResult.PermanentFailure(invalidMessage!, 400));
            Assert.ThrowsAny<ArgumentException>(() => new WebhookDeliveryResult.PermanentFailure(invalidMessage!, PermanentFailureReason.EndpointNotFound));
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    // 5. STATIC FACTORY METHODS OVERLOAD TESTS
    // ────────────────────────────────────────────────────────────────────────

    public sealed class TheFactoryMethods {
        [Fact]
        public void Success_Produces_DeliveredInstance() {
            WebhookDeliveryResult r1 = WebhookDeliveryResult.Success(200);
            WebhookDeliveryResult r2 = WebhookDeliveryResult.Success(201, "{\"id\":1}");

            Assert.IsType<WebhookDeliveryResult.Delivered>(r1);
            Assert.IsType<WebhookDeliveryResult.Delivered>(r2);
            Assert.Equal(201, ((WebhookDeliveryResult.Delivered)r2).StatusCode);
            Assert.Equal("{\"id\":1}", ((WebhookDeliveryResult.Delivered)r2).ResponseBody);
        }

        [Fact]
        public void Duplicate_Produces_DeduplicatedInstance() {
            WebhookDeliveryResult result = WebhookDeliveryResult.Duplicate("order_99");

            WebhookDeliveryResult.Deduplicated dedup = Assert.IsType<WebhookDeliveryResult.Deduplicated>(result);
            Assert.Equal("order_99", dedup.DeduplicationKey);
        }

        [Fact]
        public void Transient_Produces_TransientFailureInstance() {
            WebhookDeliveryResult r1 = WebhookDeliveryResult.Transient("Error 1");
            WebhookDeliveryResult r2 = WebhookDeliveryResult.Transient("Error 2", 503);
            WebhookDeliveryResult r3 = WebhookDeliveryResult.Transient("Error 3", 503, TimeSpan.FromSeconds(10));

            Assert.IsType<WebhookDeliveryResult.TransientFailure>(r1);
            Assert.Equal(503, ((WebhookDeliveryResult.TransientFailure)r2).StatusCode);
            Assert.Equal(TimeSpan.FromSeconds(10), ((WebhookDeliveryResult.TransientFailure)r3).RetryAfter);
        }

        [Fact]
        public void SpecializedTransientFactories_ProduceCorrectReasonAndStatusCode() {
            // CircuitBroken
            WebhookDeliveryResult cbResult = WebhookDeliveryResult.CircuitBroken("ep-1", TimeSpan.FromSeconds(45));
            WebhookDeliveryResult.TransientFailure cb = Assert.IsType<WebhookDeliveryResult.TransientFailure>(cbResult);
            Assert.Equal(503, cb.StatusCode);
            Assert.Equal(TimeSpan.FromSeconds(45), cb.RetryAfter);
            Assert.Equal(TransientFailureReason.CircuitBreakerOpen, cb.Reason);

            // RateLimited
            WebhookDeliveryResult rlResult = WebhookDeliveryResult.RateLimited("ep-2", TimeSpan.FromSeconds(15));
            WebhookDeliveryResult.TransientFailure rl = Assert.IsType<WebhookDeliveryResult.TransientFailure>(rlResult);
            Assert.Equal(429, rl.StatusCode);
            Assert.Equal(TimeSpan.FromSeconds(15), rl.RetryAfter);
            Assert.Equal(TransientFailureReason.RateLimitThrottled, rl.Reason);

            // Timeout
            WebhookDeliveryResult toResult = WebhookDeliveryResult.Timeout("Request timed out");
            WebhookDeliveryResult.TransientFailure to = Assert.IsType<WebhookDeliveryResult.TransientFailure>(toResult);
            Assert.Equal(408, to.StatusCode);
            Assert.Equal(TransientFailureReason.Timeout, to.Reason);

            // NetworkFailure
            HttpRequestException ex = new("Socket reset");
            WebhookDeliveryResult nfResult = WebhookDeliveryResult.NetworkFailure("Network failed", ex);
            WebhookDeliveryResult.TransientFailure nf = Assert.IsType<WebhookDeliveryResult.TransientFailure>(nfResult);
            Assert.Null(nf.StatusCode);
            Assert.Equal(TransientFailureReason.NetworkGlitch, nf.Reason);
            Assert.Same(ex, nf.Exception);
        }

        [Fact]
        public void Permanent_Produces_PermanentFailureInstance() {
            WebhookDeliveryResult r1 = WebhookDeliveryResult.Permanent("Error 1");
            WebhookDeliveryResult r2 = WebhookDeliveryResult.Permanent("Error 2", 404);
            WebhookDeliveryResult r3 = WebhookDeliveryResult.Permanent("Error 3", PermanentFailureReason.EndpointDisabled);
            WebhookDeliveryResult r4 = WebhookDeliveryResult.Permanent("Error 4", 403, PermanentFailureReason.DestinationRejected);

            Assert.IsType<WebhookDeliveryResult.PermanentFailure>(r1);
            Assert.Equal(404, ((WebhookDeliveryResult.PermanentFailure)r2).StatusCode);
            Assert.Equal(PermanentFailureReason.EndpointDisabled, ((WebhookDeliveryResult.PermanentFailure)r3).Reason);
            Assert.Equal(PermanentFailureReason.DestinationRejected, ((WebhookDeliveryResult.PermanentFailure)r4).Reason);
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    // 6. PATTERN MATCHING & DISCRIMINATION EXHAUSTIVENESS
    // ────────────────────────────────────────────────────────────────────────

    public sealed class PatternMatchingBehavior {
        [Fact]
        public void PatternMatching_CorrectlyDiscriminatesAllSubtypesAndReasons() {
            WebhookDeliveryResult[] results = [
                WebhookDeliveryResult.Success(200),
                WebhookDeliveryResult.Duplicate("k1"),
                WebhookDeliveryResult.CircuitBroken("ep-1", TimeSpan.FromSeconds(30)),
                WebhookDeliveryResult.RateLimited("ep-2", TimeSpan.FromSeconds(10)),
                WebhookDeliveryResult.Permanent("terminal", 404, PermanentFailureReason.EndpointNotFound)
            ];

            List<string> actionsTaken = [];

            foreach(WebhookDeliveryResult result in results) {
                string action = result switch {
                    WebhookDeliveryResult.Delivered d => $"Delivered:{d.StatusCode}",
                    WebhookDeliveryResult.Deduplicated dedup => $"Deduplicated:{dedup.DeduplicationKey}",
                    WebhookDeliveryResult.TransientFailure { Reason: TransientFailureReason.CircuitBreakerOpen } cb => $"CircuitOpen:{cb.RetryAfter?.TotalSeconds}s",
                    WebhookDeliveryResult.TransientFailure { Reason: TransientFailureReason.RateLimitThrottled } rl => $"RateLimited:{rl.RetryAfter?.TotalSeconds}s",
                    WebhookDeliveryResult.TransientFailure tf => $"Retry:{tf.StatusCode}",
                    WebhookDeliveryResult.PermanentFailure pf => $"DeadLetter:{pf.Reason}",
                    _ => throw new InvalidOperationException("Unreachable case in closed hierarchy")
                };
                actionsTaken.Add(action);
            }

            Assert.Equal([
                "Delivered:200",
                "Deduplicated:k1",
                "CircuitOpen:30s",
                "RateLimited:10s",
                "DeadLetter:EndpointNotFound"
            ], actionsTaken);
        }
    }
}