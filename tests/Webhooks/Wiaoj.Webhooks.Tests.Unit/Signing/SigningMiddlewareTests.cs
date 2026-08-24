using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using System.Net;
using System.Text;
using Wiaoj.Extensions;
using Wiaoj.Security;
using Wiaoj.Security.Testing;
using Wiaoj.Webhooks.Internal;
using Wiaoj.Webhooks.Signing;
using Wiaoj.Webhooks.Tests.Unit.Fakes;
using Wiaoj.Webhooks.Tests.Unit.TestData;

namespace Wiaoj.Webhooks.Tests.Unit.Signing;

[Trait("Category", "Unit")]
[Trait("Feature", "Signing")]
[Trait("Component", "Middleware")]
public sealed class SigningMiddlewareTests {

    // ────────────────────────────────────────────────────────────────────────
    // 1. CONSTRUCTOR & ARGUMENT GUARDS
    // ────────────────────────────────────────────────────────────────────────

    public sealed class TheConstructor {
        [Fact]
        public void Constructor_Throws_WhenSignerIsNull() {
            FakeSecretProtector<WebhookSigningContext> protector = new();
            Assert.ThrowsAny<ArgumentException>(() =>
                new SigningMiddleware(null!, protector, TimeProvider.System, NullLogger<SigningMiddleware>.Instance));
        }

        [Fact]
        public void Constructor_Throws_WhenSecretProtectorIsNull() {
            HmacSha256WebhookSigner signer = new();
            Assert.ThrowsAny<ArgumentException>(() =>
                new SigningMiddleware(signer, null!, TimeProvider.System, NullLogger<SigningMiddleware>.Instance));
        }

        [Fact]
        public void Constructor_Throws_WhenTimeProviderIsNull() {
            HmacSha256WebhookSigner signer = new();
            FakeSecretProtector<WebhookSigningContext> protector = new();
            Assert.ThrowsAny<ArgumentException>(() =>
                new SigningMiddleware(signer, protector, null!, NullLogger<SigningMiddleware>.Instance));
        }

        [Fact]
        public void Constructor_Throws_WhenLoggerIsNull() {
            HmacSha256WebhookSigner signer = new();
            FakeSecretProtector<WebhookSigningContext> protector = new();
            Assert.ThrowsAny<ArgumentException>(() =>
                new SigningMiddleware(signer, protector, TimeProvider.System, null!));
        }
    }

    public sealed class TheInvokeAsyncGuards {
        [Fact]
        public async Task InvokeAsync_Throws_WhenContextIsNull() {
            SigningMiddleware middleware = WebhookTestFactory.CreateSigningMiddleware();

            await Assert.ThrowsAnyAsync<ArgumentException>(() =>
                middleware.InvokeAsync(null!, static (ctx, ct) => Task.CompletedTask, TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task InvokeAsync_Throws_WhenNextIsNull() {
            SigningMiddleware middleware = WebhookTestFactory.CreateSigningMiddleware();
            WebhookDeliveryContext context = WebhookTestFactory.CreateContext();

            await Assert.ThrowsAnyAsync<ArgumentException>(() =>
                middleware.InvokeAsync(context, null!, TestContext.Current.CancellationToken));
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    // 2. SIGNING EXECUTION & VERIFICATION
    // ────────────────────────────────────────────────────────────────────────

    public sealed class WhenSigningPayload {
        [Fact]
        public async Task InvokeAsync_SignsPayload_AndInjectsSignatureHeaderIntoContext() {
            // Arrange
            FakeSecretProtector<WebhookSigningContext> protector = new();
            HmacSha256WebhookSigner signer = new();
            SigningMiddleware middleware = WebhookTestFactory.CreateSigningMiddleware(signer: signer, secretProtector: protector);

            const string rawSecret = "super-secret-signing-key-long-enough-for-aes-gcm-38chars";
            EncryptedSecret<WebhookSigningContext> encryptedSecret = protector.Protect(rawSecret);

            WebhookEndpoint endpoint = WebhookTestFactory.CreateEndpoint(encryptedSecret);
            WebhookDeliveryContext context = WebhookTestFactory.CreateContext(endpoint);

            bool nextCalled = false;
            WebhookDelegate next = (ctx, ct) => {
                nextCalled = true;
                return Task.CompletedTask;
            };

            // Act
            await middleware.InvokeAsync(context, next, TestContext.Current.CancellationToken);

            // Assert: Downstream next delegate invoked
            Assert.True(nextCalled);

            // Assert: Typed context extensions verify headers and signature
            IReadOnlyDictionary<string, string> headers = context.GetHeaders();
            Assert.True(headers.ContainsKey(signer.HeaderName));

            WebhookSignature? signature = context.GetSignature();
            Assert.NotNull(signature);

            string signatureHeader = headers[signer.HeaderName];
            Assert.StartsWith("t=", signatureHeader);
            Assert.Contains(",v1=", signatureHeader);

            // Verify authenticity using the signer
            byte[] payloadBytes = context.SerializedPayload.ToUtf8Bytes();
            byte[] secretBytes = rawSecret.ToUtf8Bytes();
            bool isValid = signer.Verify(payloadBytes, signatureHeader, secretBytes, TimeSpan.FromMinutes(5));
            Assert.True(isValid);
        }

        [Fact]
        public async Task Pipeline_DeliversWebhook_WithSignatureHeaderOnHttpRequest() {
            // Arrange
            FakeSecretProtector<WebhookSigningContext> protector = new();
            HmacSha256WebhookSigner signer = new();
            FakeTimeProvider timeProvider = new();
            SigningMiddleware signingMiddleware = WebhookTestFactory.CreateSigningMiddleware(
                signer: signer,
                secretProtector: protector,
                timeProvider: timeProvider);

            FakeHttpMessageHandler httpHandler = new(HttpStatusCode.OK);
            HttpWebhookDeliverer deliverer = WebhookTestFactory.CreateDeliverer(httpHandler, timeProvider: timeProvider);

            WebhookPipelineRunner runner = new([signingMiddleware], deliverer, timeProvider, NullLogger<WebhookPipelineRunner>.Instance);

            const string rawSecret = "e2e-webhook-secret-42-long-enough-for-aes-gcm-38chars";
            EncryptedSecret<WebhookSigningContext> encryptedSecret = protector.Protect(rawSecret);
            WebhookEndpoint endpoint = WebhookTestFactory.CreateEndpoint(encryptedSecret);
            WebhookDeliveryContext context = WebhookTestFactory.CreateContext(endpoint);

            // Act
            WebhookDeliveryAttempt attempt = await runner.RunAsync(context, TestContext.Current.CancellationToken);

            // Assert
            Assert.True(attempt.IsSuccess);
            Assert.NotNull(httpHandler.LastRequest);
            Assert.True(httpHandler.LastRequest.Headers.Contains(signer.HeaderName));

            string headerValue = httpHandler.LastRequest.Headers.GetValues(signer.HeaderName).First();
            Assert.StartsWith("t=", headerValue);
            Assert.Contains(",v1=", headerValue);

            // Verify authenticity using the matching time provider timestamp
            byte[] receivedPayloadBytes = Encoding.UTF8.GetBytes(httpHandler.LastRequestBody!);
            byte[] secretBytes = Encoding.UTF8.GetBytes(rawSecret);

            bool isValid = signer.Verify(
                receivedPayloadBytes,
                headerValue,
                secretBytes,
                TimeSpan.FromMinutes(5),
                timeProvider.GetUnixTimestamp());

            Assert.True(isValid);
        }

        [Fact]
        public async Task InvokeAsync_WhenEndpointHasCustomSigner_OverridesGlobalDefaultSigner() {
            // Arrange: Global default signer is HMAC-SHA256, Endpoint overrides with HMAC-SHA512
            FakeSecretProtector<WebhookSigningContext> protector = new();
            HmacSha256WebhookSigner globalSigner = new("Global-Signature");
            HmacSha512WebhookSigner customEndpointSigner = new("X-Enterprise-Sha512");
            SigningMiddleware middleware = WebhookTestFactory.CreateSigningMiddleware(signer: globalSigner, secretProtector: protector);

            const string rawSecret = "custom-endpoint-signing-secret-key-32bytes-long";
            EncryptedSecret<WebhookSigningContext> encryptedSecret = protector.Protect(rawSecret);

            WebhookEndpoint endpoint = new(
                WebhookTestFactory.CreateEndpointId("ep_custom_signer"),
                WebhookTestFactory.CreateTargetUrl(),
                encryptedSecret,
                customSigner: customEndpointSigner,
                customHeaders: null);

            WebhookDeliveryContext context = WebhookTestFactory.CreateContext(endpoint);

            // Act
            await middleware.InvokeAsync(context, static (ctx, ct) => Task.CompletedTask, TestContext.Current.CancellationToken);

            // Assert: Header name must match the custom signer ("X-Enterprise-Sha512") with SHA-512 length (128 hex chars)
            IReadOnlyDictionary<string, string> headers = context.GetHeaders();
            Assert.False(headers.ContainsKey("Global-Signature"));
            Assert.True(headers.ContainsKey("X-Enterprise-Sha512"));

            WebhookSignature? signature = context.GetSignature();
            Assert.NotNull(signature);
            Assert.Equal("v2", signature.Value.Scheme);
            Assert.Equal(128, signature.Value.Signature.Length);
        }
    }
}