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
    // 1. TÜM 4XX KALICI HTTP KODLARI İLE SHORT-CIRCUIT
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
            // Arrange: MaxAttempts = 100 olan aşırı cömert bir politika bile olsa
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

            // Act: İlk denemede kalıcı hata döner
            await middleware.InvokeAsync(context, next, TestContext.Current.CancellationToken);

            // Assert: Sıfır retry işi, doğrudan Dead-Letter!
            Assert.Empty(transport.EnqueuedJobs);
            Assert.True(context.IsDeadLettered());
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    // 2. TÜM PERMANENT FAILURE REASON ENUM DEĞERLERİ
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
    // 3. TRANSIENT VS PERMANENT POZİTİF KONTROLLERİ
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

            // Assert: Retry kuyruğuna tam 1 iş atılmalı, Dead-Letter OLMAMALIDIR!
            Assert.Single(transport.EnqueuedJobs);
            Assert.False(context.IsDeadLettered());
            Assert.Equal(TimeSpan.FromSeconds(2), transport.EnqueuedJobs[0].Delay);
        }

        [Fact]
        public async Task InvokeAsync_WhenNullStatusCode_SocketOrDnsFailure_IsTreatedAsTransientRetry() {
            // Null statusCode = Socket koptu, DNS çözülemedi veya bağlantı reddedildi
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

            // Ağ kopmaları kalıcı sanılmamalı, yeniden denenmelidir!
            Assert.Single(transport.EnqueuedJobs);
            Assert.False(context.IsDeadLettered());
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    // 4. ÇELİŞKİLİ & KAOTİK SERVER YANITLARI
    // ────────────────────────────────────────────────────────────────────────

    public sealed class TheChaoticEdgeCases {
        [Fact]
        public async Task InvokeAsync_WhenPermanentFailureCarriesRetryAfterHeader_StillDeadLetters() {
            // Karşı sunucu hatalı/çelişkili şekilde 401 Unauthorized dönüp Retry-After: 60 verse bile
            FakeWebhookTransport transport = new();
            ExponentialBackoffPolicy policy = new();
            RetryMiddleware middleware = new(policy, transport, NullLogger<RetryMiddleware>.Instance);

            WebhookDeliveryContext context = WebhookTestFactory.CreateContext();
            // Permanent failure with statusCode 401
            WebhookDeliveryResult permanent = WebhookDeliveryResult.Permanent("Unauthorized", 401, PermanentFailureReason.DestinationRejected);

            WebhookDelegate next = (ctx, ct) => {
                ctx.SetResult(permanent);
                return Task.CompletedTask;
            };

            await middleware.InvokeAsync(context, next, TestContext.Current.CancellationToken);

            // 401 olduğu için Retry-After'a kanmamalı, retry atmamalıdır!
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