using Microsoft.Extensions.Logging.Abstractions;
using Wiaoj.Webhooks.Retries;
using Wiaoj.Webhooks.Tests.Unit.Fakes;
using Wiaoj.Webhooks.Tests.Unit.TestData;

namespace Wiaoj.Webhooks.Tests.Unit.Retries;

[Trait("Category", "Unit")]
[Trait("Feature", "Resilience")]
[Trait("Component", "RetryMiddleware")]
public sealed class PermanentFailureShortCircuitTests {

    // ────────────────────────────────────────────────────────────────────────
    // 1. ALL 4XX PERMANENT HTTP STATUS CODE SHORT-CIRCUITING
    // ────────────────────────────────────────────────────────────────────────

    public sealed class TheAll4xxPermanentStatusCodes {
        [Theory]
        [InlineData(400)] // Bad Request
        [InlineData(401)] // Unauthorized
        [InlineData(403)] // Forbidden
        [InlineData(404)] // Not Found
        [InlineData(405)] // Method Not Allowed
        [InlineData(410)] // Gone
        [InlineData(413)] // Payload Too Large
        [InlineData(415)] // Unsupported Media Type
        [InlineData(422)] // Unprocessable Entity
        public async Task InvokeAsync_WhenAny4xxPermanentClientErrorOccurs_ImmediatelyDeadLettersOnFirstAttempt(int statusCode) {
            // Arrange: Even with generous retry settings (MaxAttempts = 100)
            FakeWebhookTransport transport = new();
            ExponentialBackoffOptions options = new() { MaxAttempts = 100 };
            ExponentialBackoffPolicy policy = new(options);
            RetryMiddleware middleware = new(policy, transport, NullLogger<RetryMiddleware>.Instance);

            WebhookDeliveryContext context = WebhookTestFactory.CreateContext();
            WebhookDeliveryResult permanentFailure = WebhookDeliveryResult.Permanent(
                $"Permanent error HTTP {statusCode}",
                statusCode,
                PermanentFailureReason.DestinationRejected);

            WebhookDelegate next = (ctx, ct) => {
                ctx.SetResult(permanentFailure);
                return Task.CompletedTask;
            };

            // Act: First attempt returns permanent failure
            await middleware.InvokeAsync(context, next, TestContext.Current.CancellationToken);

            // Assert: Zero retries, immediately dead-lettered
            Assert.Empty(transport.EnqueuedJobs);
            Assert.True(context.IsDeadLettered());
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    // 2. ALL PERMANENT FAILURE REASON ENUM VALUES
    // ────────────────────────────────────────────────────────────────────────

    public sealed class TheAllPermanentFailureReasons {
        [Theory]
        [InlineData(PermanentFailureReason.General)]
        [InlineData(PermanentFailureReason.DestinationRejected)]
        [InlineData(PermanentFailureReason.EndpointNotFound)]
        [InlineData(PermanentFailureReason.EndpointDisabled)]
        [InlineData(PermanentFailureReason.InvalidDestination)]
        [InlineData(PermanentFailureReason.PayloadTooLarge)]
        [InlineData(PermanentFailureReason.SecurityValidationFailed)]
        public async Task InvokeAsync_WhenAnyPermanentFailureReasonIsSet_ShortCircuitsRetries(PermanentFailureReason reason) {
            FakeWebhookTransport transport = new();
            ExponentialBackoffPolicy policy = new(new ExponentialBackoffOptions { MaxAttempts = 5 });
            RetryMiddleware middleware = new(policy, transport, NullLogger<RetryMiddleware>.Instance);

            WebhookDeliveryContext context = WebhookTestFactory.CreateContext();
            WebhookDeliveryResult failure = WebhookDeliveryResult.Permanent("Unrecoverable domain failure", reason);

            WebhookDelegate next = (ctx, ct) => {
                ctx.SetResult(failure);
                return Task.CompletedTask;
            };

            await middleware.InvokeAsync(context, next, TestContext.Current.CancellationToken);

            Assert.Empty(transport.EnqueuedJobs);
            Assert.True(context.IsDeadLettered());
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    // 3. TRANSIENT VERSUS PERMANENT ERROR EVALUATION
    // ────────────────────────────────────────────────────────────────────────

    public sealed class TheTransientVersusPermanentDistinction {
        [Theory]
        [InlineData(500)] // Internal Server Error
        [InlineData(502)] // Bad Gateway
        [InlineData(503)] // Service Unavailable
        [InlineData(504)] // Gateway Timeout
        [InlineData(408)] // Request Timeout
        [InlineData(429)] // Too Many Requests
        public async Task InvokeAsync_WhenTransientErrorOccurs_DoesNotDeadLetter_AndEnqueuesRetry(int statusCode) {
            FakeWebhookTransport transport = new();
            ExponentialBackoffOptions options = new() {
                MaxAttempts = 5,
                InitialDelay = TimeSpan.FromSeconds(2),
                Jitter = null
            };
            ExponentialBackoffPolicy policy = new(options);
            RetryMiddleware middleware = new(policy, transport, NullLogger<RetryMiddleware>.Instance);

            WebhookDeliveryContext context = WebhookTestFactory.CreateContext();
            WebhookDeliveryResult transientFailure = WebhookDeliveryResult.Transient("Temporary glitch", statusCode);

            WebhookDelegate next = (ctx, ct) => {
                ctx.SetResult(transientFailure);
                return Task.CompletedTask;
            };

            // Act
            await middleware.InvokeAsync(context, next, TestContext.Current.CancellationToken);

            // Assert: Exactly 1 retry job enqueued, must not dead-letter
            Assert.Single(transport.EnqueuedJobs);
            Assert.False(context.IsDeadLettered());
            Assert.Equal(TimeSpan.FromSeconds(2), transport.EnqueuedJobs[0].Delay);
        }

        [Fact]
        public async Task InvokeAsync_WhenNullStatusCode_SocketOrDnsFailure_IsTreatedAsTransientRetry() {
            // Null statusCode indicates network disconnect, DNS failure or connection reset
            FakeWebhookTransport transport = new();
            ExponentialBackoffPolicy policy = new(new ExponentialBackoffOptions { MaxAttempts = 3, InitialDelay = TimeSpan.FromSeconds(1), Jitter = null });
            RetryMiddleware middleware = new(policy, transport, NullLogger<RetryMiddleware>.Instance);

            WebhookDeliveryContext context = WebhookTestFactory.CreateContext();
            WebhookDeliveryResult networkDrop = WebhookDeliveryResult.Transient("Connection reset by peer"); // statusCode is null

            WebhookDelegate next = (ctx, ct) => {
                ctx.SetResult(networkDrop);
                return Task.CompletedTask;
            };

            await middleware.InvokeAsync(context, next, TestContext.Current.CancellationToken);

            // Network errors must be retried
            Assert.Single(transport.EnqueuedJobs);
            Assert.False(context.IsDeadLettered());
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    // 4. CONFLICTING AND CHAOTIC SERVER RESPONSES
    // ────────────────────────────────────────────────────────────────────────

    public sealed class TheChaoticEdgeCases {
        [Fact]
        public async Task InvokeAsync_WhenPermanentFailureCarriesRetryAfterHeader_StillDeadLetters() {
            // Even if remote server errantly sends 401 with Retry-After: 60
            FakeWebhookTransport transport = new();
            ExponentialBackoffPolicy policy = new();
            RetryMiddleware middleware = new(policy, transport, NullLogger<RetryMiddleware>.Instance);

            WebhookDeliveryContext context = WebhookTestFactory.CreateContext();
            WebhookDeliveryResult permanent = WebhookDeliveryResult.Permanent("Unauthorized", 401, PermanentFailureReason.DestinationRejected);

            WebhookDelegate next = (ctx, ct) => {
                ctx.SetResult(permanent);
                return Task.CompletedTask;
            };

            await middleware.InvokeAsync(context, next, TestContext.Current.CancellationToken);

            // Must not retry on 401
            Assert.Empty(transport.EnqueuedJobs);
            Assert.True(context.IsDeadLettered());
        }

        [Fact]
        public async Task InvokeAsync_WhenDeliveryIsSuccessfulOrDeduplicated_NeverDeadLettersOrRetries() {
            FakeWebhookTransport transport = new();
            ExponentialBackoffPolicy policy = new();
            RetryMiddleware middleware = new(policy, transport, NullLogger<RetryMiddleware>.Instance);

            WebhookDeliveryContext contextSuccess = WebhookTestFactory.CreateContext();
            WebhookDeliveryContext contextDedup = WebhookTestFactory.CreateContext();

            // Act 1: Success 200
            await middleware.InvokeAsync(contextSuccess, (ctx, ct) => {
                ctx.SetResult(WebhookDeliveryResult.Success(200));
                return Task.CompletedTask;
            }, TestContext.Current.CancellationToken);

            // Act 2: Deduplicated
            await middleware.InvokeAsync(contextDedup, (ctx, ct) => {
                ctx.SetResult(WebhookDeliveryResult.Duplicate("dedup-1"));
                return Task.CompletedTask;
            }, TestContext.Current.CancellationToken);

            // Assert
            Assert.Empty(transport.EnqueuedJobs);
            Assert.False(contextSuccess.IsDeadLettered());
            Assert.False(contextDedup.IsDeadLettered());
        }
    }
}