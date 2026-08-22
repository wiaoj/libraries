using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using System.Net;
using System.Text;
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
                new SigningMiddleware(null!, protector, NullLogger<SigningMiddleware>.Instance));
        }

        [Fact]
        public void Constructor_Throws_WhenSecretProtectorIsNull() {
            HmacSha256WebhookSigner signer = new();
            Assert.ThrowsAny<ArgumentException>(() =>
                new SigningMiddleware(signer, null!, NullLogger<SigningMiddleware>.Instance));
        }

        [Fact]
        public void Constructor_Throws_WhenLoggerIsNull() {
            HmacSha256WebhookSigner signer = new();
            FakeSecretProtector<WebhookSigningContext> protector = new();
            Assert.ThrowsAny<ArgumentException>(() =>
                new SigningMiddleware(signer, protector, null!));
        }
    }

    public sealed class TheInvokeAsyncGuards {
        [Fact]
        public async Task InvokeAsync_Throws_WhenContextIsNull() {
            HmacSha256WebhookSigner signer = new();
            FakeSecretProtector<WebhookSigningContext> protector = new();
            SigningMiddleware middleware = new(signer, protector, NullLogger<SigningMiddleware>.Instance);

            await Assert.ThrowsAnyAsync<ArgumentException>(() =>
                middleware.InvokeAsync(null!, (ctx, ct) => Task.CompletedTask, CancellationToken.None));
        }

        [Fact]
        public async Task InvokeAsync_Throws_WhenNextIsNull() {
            HmacSha256WebhookSigner signer = new();
            FakeSecretProtector<WebhookSigningContext> protector = new();
            SigningMiddleware middleware = new(signer, protector, NullLogger<SigningMiddleware>.Instance);
            WebhookDeliveryContext context = WebhookTestFactory.CreateContext();

            await Assert.ThrowsAnyAsync<ArgumentException>(() =>
                middleware.InvokeAsync(context, null!, CancellationToken.None));
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
            SigningMiddleware middleware = new(signer, protector, NullLogger<SigningMiddleware>.Instance);

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
            await middleware.InvokeAsync(context, next, CancellationToken.None);

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
            byte[] payloadBytes = Encoding.UTF8.GetBytes(context.SerializedPayload);
            byte[] secretBytes = Encoding.UTF8.GetBytes(rawSecret);
            bool isValid = signer.Verify(payloadBytes, signatureHeader, secretBytes, TimeSpan.FromMinutes(5));
            Assert.True(isValid);
        }

        [Fact]
        public async Task Pipeline_DeliversWebhook_WithSignatureHeaderOnHttpRequest() {
            // Arrange
            FakeSecretProtector<WebhookSigningContext> protector = new();
            HmacSha256WebhookSigner signer = new();
            SigningMiddleware signingMiddleware = new(signer, protector, NullLogger<SigningMiddleware>.Instance);

            FakeHttpMessageHandler httpHandler = new(HttpStatusCode.OK);
            HttpWebhookDeliverer deliverer = WebhookTestFactory.CreateDeliverer(httpHandler);
            FakeTimeProvider timeProvider = new();

            WebhookPipelineRunner runner = new([signingMiddleware], deliverer, timeProvider, NullLogger<WebhookPipelineRunner>.Instance);

            const string rawSecret = "e2e-webhook-secret-42-long-enough-for-aes-gcm-38chars";
            EncryptedSecret<WebhookSigningContext> encryptedSecret = protector.Protect(rawSecret);
            WebhookEndpoint endpoint = WebhookTestFactory.CreateEndpoint(encryptedSecret);
            WebhookDeliveryContext context = WebhookTestFactory.CreateContext(endpoint);

            // Act
            WebhookDeliveryAttempt attempt = await runner.RunAsync(context);

            // Assert
            Assert.True(attempt.IsSuccess);
            Assert.NotNull(httpHandler.LastRequest);
            Assert.True(httpHandler.LastRequest.Headers.Contains(signer.HeaderName));

            string headerValue = httpHandler.LastRequest.Headers.GetValues(signer.HeaderName).First();
            Assert.StartsWith("t=", headerValue);
            Assert.Contains(",v1=", headerValue);

            // Verify authenticity of received HTTP request header
            byte[] receivedPayloadBytes = Encoding.UTF8.GetBytes(httpHandler.LastRequestBody!);
            byte[] secretBytes = Encoding.UTF8.GetBytes(rawSecret);
            bool isValid = signer.Verify(receivedPayloadBytes, headerValue, secretBytes, TimeSpan.FromMinutes(5));
            Assert.True(isValid);
        }
    }
}