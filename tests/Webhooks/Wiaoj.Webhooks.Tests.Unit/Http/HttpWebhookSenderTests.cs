using Microsoft.Extensions.Logging.Abstractions;
using System.Collections.ObjectModel;
using System.Net;
using Wiaoj.Webhooks.Internal;
using Wiaoj.Webhooks.Tests.Unit.Fakes;
using Wiaoj.Webhooks.Tests.Unit.TestData;

namespace Wiaoj.Webhooks.Tests.Unit.Http;

[Trait("Category", "Unit")]
[Trait("Component", "HttpSender")]
public sealed class HttpWebhookSenderTests {
    private static readonly IReadOnlyDictionary<string, string> EmptyHeaders = ReadOnlyDictionary<string, string>.Empty;

    private static HttpWebhookSender CreateSender(HttpMessageHandler handler) {
        return new HttpWebhookSender(new HttpClient(handler), NullLogger<HttpWebhookSender>.Instance);
    }

    // ────────────────────────────────────────────────────────────────────────
    // 1. CONSTRUCTOR GUARDS
    // ────────────────────────────────────────────────────────────────────────

    public sealed class TheConstructor {
        [Fact]
        public void Constructor_Throws_WhenHttpClientIsNull() {
            Assert.ThrowsAny<ArgumentException>(() =>
                new HttpWebhookSender(null!, NullLogger<HttpWebhookSender>.Instance));
        }

        [Fact]
        public void Constructor_Throws_WhenLoggerIsNull() {
            Assert.ThrowsAny<ArgumentException>(() =>
                new HttpWebhookSender(new HttpClient(new FakeHttpMessageHandler(HttpStatusCode.OK)), null!));
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    // 2. SEND ASYNC ARGUMENT GUARDS
    // ────────────────────────────────────────────────────────────────────────

    public sealed class TheSendAsyncGuards {
        [Fact]
        public async Task SendAsync_Throws_WhenTargetUrlIsNull() {
            HttpWebhookSender sender = CreateSender(new FakeHttpMessageHandler(HttpStatusCode.OK));

            await Assert.ThrowsAnyAsync<ArgumentException>(() =>
                sender.SendAsync(null!, WebhookTestConstants.PayloadJson, EmptyHeaders, TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task SendAsync_Throws_WhenPayloadIsNull() {
            HttpWebhookSender sender = CreateSender(new FakeHttpMessageHandler(HttpStatusCode.OK));

            await Assert.ThrowsAnyAsync<ArgumentException>(() =>
                sender.SendAsync(WebhookTestFactory.CreateTargetUrl(), null!, EmptyHeaders, TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task SendAsync_Throws_WhenHeadersDictionaryIsNull() {
            HttpWebhookSender sender = CreateSender(new FakeHttpMessageHandler(HttpStatusCode.OK));

            await Assert.ThrowsAnyAsync<ArgumentException>(() =>
                sender.SendAsync(WebhookTestFactory.CreateTargetUrl(), WebhookTestConstants.PayloadJson, null!, TestContext.Current.CancellationToken));
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    // 3. HTTP DISPATCH & PROTOCOL BEHAVIOR
    // ────────────────────────────────────────────────────────────────────────

    public sealed class WhenSendingPayloads {
        [Fact]
        public async Task SendAsync_IssuesHttpPostRequest_ToTargetUrl() {
            FakeHttpMessageHandler handler = new(HttpStatusCode.OK);
            HttpWebhookSender sender = CreateSender(handler);

            await sender.SendAsync(WebhookTestFactory.CreateTargetUrl(), WebhookTestConstants.PayloadJson, EmptyHeaders, TestContext.Current.CancellationToken);

            Assert.NotNull(handler.LastRequest);
            Assert.Equal(HttpMethod.Post, handler.LastRequest.Method);
            Assert.Equal(WebhookTestFactory.CreateTargetUrl(), handler.LastRequest.RequestUri);
        }

        [Fact]
        public async Task SendAsync_SetsJsonContentType_AndPreservesRawPayloadBody() {
            FakeHttpMessageHandler handler = new(HttpStatusCode.OK);
            HttpWebhookSender sender = CreateSender(handler);

            await sender.SendAsync(WebhookTestFactory.CreateTargetUrl(), WebhookTestConstants.PayloadJson, EmptyHeaders, TestContext.Current.CancellationToken);

            Assert.NotNull(handler.LastRequest);
            Assert.Equal(WebhookTestConstants.PayloadJson, handler.LastRequestBody);
            Assert.Equal("application/json", handler.LastRequest.Content?.Headers.ContentType?.MediaType);
        }

        [Fact]
        public async Task SendAsync_AttachesCustomHeaders_ToHttpRequest() {
            FakeHttpMessageHandler handler = new(HttpStatusCode.OK);
            HttpWebhookSender sender = CreateSender(handler);
            Dictionary<string, string> customHeaders = new() {
                { "Wiaoj-Signature", "t=123,v1=abc" },
                { "X-Custom-Trace-Id", "trace-999" }
            };

            await sender.SendAsync(WebhookTestFactory.CreateTargetUrl(), WebhookTestConstants.PayloadJson, customHeaders, TestContext.Current.CancellationToken);

            Assert.NotNull(handler.LastRequest);
            Assert.True(handler.LastRequest.Headers.Contains("Wiaoj-Signature"));
            Assert.True(handler.LastRequest.Headers.Contains("X-Custom-Trace-Id"));
            Assert.Equal("t=123,v1=abc", handler.LastRequest.Headers.GetValues("Wiaoj-Signature").First());
        }

        [Fact]
        public async Task SendAsync_ReturnsRawHttpResponse_RegardlessOfStatusCode() {
            FakeHttpMessageHandler handler = new(HttpStatusCode.InternalServerError);
            HttpWebhookSender sender = CreateSender(handler);

            HttpResponseMessage response = await sender.SendAsync(
                WebhookTestFactory.CreateTargetUrl(),
                WebhookTestConstants.PayloadJson,
                EmptyHeaders,
                TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        }

        [Fact]
        public async Task SendAsync_ThrowsOperationCanceledException_WhenCancelled() {
            HttpWebhookSender sender = CreateSender(new HangingHttpMessageHandler());
            using CancellationTokenSource cts = new();
            cts.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                sender.SendAsync(WebhookTestFactory.CreateTargetUrl(), WebhookTestConstants.PayloadJson, EmptyHeaders, cts.Token));
        }
    }
}