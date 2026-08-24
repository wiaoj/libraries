using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using System.Text;
using Wiaoj.Serialization;
using Wiaoj.Serialization.SystemTextJson;
using Wiaoj.Webhooks.AspNetCore;
using Wiaoj.Webhooks.AspNetCore.Authentication;
using Wiaoj.Webhooks.AspNetCore.Context;
using Wiaoj.Webhooks.AspNetCore.Filters;
using Wiaoj.Webhooks.AspNetCore.Metadata;
using Wiaoj.Webhooks.Idempotency;
using Wiaoj.Webhooks.Internal;
using Wiaoj.Webhooks.Signing;
using Wiaoj.Webhooks.Tests.Unit.TestData;

namespace Wiaoj.Webhooks.Tests.Unit.AspNetCore;

[Trait("Category", "Unit")]
[Trait("Feature", "InboundReceiver")]
[Trait("Component", "EndpointFilter")]
public sealed class WebhookReceiverEndpointFilterTests {
    private const string SecretKey = "whsec_super_secret_test_key_1234567890";
    private readonly HmacSha256WebhookSigner _signer = new();

    private static (DefaultHttpContext HttpContext, ServiceProvider ServiceProvider, InMemoryIdempotencyStore IdempotencyStore) CreateContext(
        string? body,
        string? signatureHeader = null,
        TimeProvider? timeProvider = null,
        Action<ServiceCollection>? configureServices = null,
        bool setContentLength = true) {

        ServiceCollection services = new();
        services.AddLogging();
        services.AddSingleton(timeProvider ?? TimeProvider.System);
        services.AddSingleton<IWebhookEventRegistry>(new WebhookEventRegistry(new WebhookEventRegistryOptions()));
        services.AddSingleton<ISerializer<WebhookSerializerKey>, SystemTextJsonSerializer<WebhookSerializerKey>>();
        services.AddSingleton<IIdempotencyStore, InMemoryIdempotencyStore>();
        services.AddOptions<WebhookInboundOptions>();

        configureServices?.Invoke(services);

        ServiceProvider sp = services.BuildServiceProvider();

        DefaultHttpContext httpContext = new() {
            RequestServices = sp
        };
        httpContext.Request.Path = "/api/webhooks/orders";
        httpContext.Request.Method = "POST";

        if(body is not null) {
            byte[] bodyBytes = Encoding.UTF8.GetBytes(body);
            httpContext.Request.Body = new MemoryStream(bodyBytes);
            if(setContentLength) {
                httpContext.Request.ContentLength = bodyBytes.Length;
            }
        }
        else {
            httpContext.Request.Body = new MemoryStream([]);
            if(setContentLength) {
                httpContext.Request.ContentLength = 0;
            }
        }

        if(!string.IsNullOrWhiteSpace(signatureHeader)) {
            httpContext.Request.Headers[WebhookHeaderNames.WebhookSignature] = signatureHeader;
        }

        return (httpContext, sp, (InMemoryIdempotencyStore)sp.GetRequiredService<IIdempotencyStore>());
    }

    // ────────────────────────────────────────────────────────────────────────
    // 1. SIGNATURE VERIFICATION & REPLAY PROTECTION
    // ────────────────────────────────────────────────────────────────────────

    public sealed class TheSignatureVerification {
        private readonly HmacSha256WebhookSigner _signer = new();

        [Fact]
        public async Task InvokeAsync_WhenSignatureIsMissing_Returns401Unauthorized() {
            // Arrange
            (DefaultHttpContext httpContext, _, _) = CreateContext("{\"OrderId\":\"ORD-1\"}");
            WebhookReceiverEndpointMetadata metadata = new() {
                SecretResolver = new SecretWebhookSecretResolver(Secret.From(SecretKey))
            };
            WebhookReceiverEndpointFilter<OrderCreatedWebhookEvent> filter = new(metadata);
            EndpointFilterInvocationContext invocationContext = new DefaultEndpointFilterInvocationContext(httpContext);

            // Act
            object? result = await filter.InvokeAsync(invocationContext, static ctx => ValueTask.FromResult<object?>(Results.Ok()));

            // Assert
            IStatusCodeHttpResult statusResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
            Assert.Equal(StatusCodes.Status401Unauthorized, statusResult.StatusCode);
        }

        [Fact]
        public async Task InvokeAsync_WhenPayloadIsTampered_Returns401Unauthorized() {
            // Arrange
            const string originalBody = "{\"OrderId\":\"ORD-1\"}";
            const string tamperedBody = "{\"OrderId\":\"ORD-HACKED\"}";
            UnixTimestamp now = UnixTimestamp.Now;

            WebhookSignature sig = this._signer.Sign(Encoding.UTF8.GetBytes(originalBody), Encoding.UTF8.GetBytes(SecretKey), now);

            (DefaultHttpContext httpContext, _, _) = CreateContext(tamperedBody, sig.HeaderValue);
            WebhookReceiverEndpointMetadata metadata = new() {
                SecretResolver = new SecretWebhookSecretResolver(Secret.From(SecretKey))
            };
            WebhookReceiverEndpointFilter<OrderCreatedWebhookEvent> filter = new(metadata);
            EndpointFilterInvocationContext invocationContext = new DefaultEndpointFilterInvocationContext(httpContext);

            // Act
            object? result = await filter.InvokeAsync(invocationContext, static ctx => ValueTask.FromResult<object?>(Results.Ok()));

            // Assert
            IStatusCodeHttpResult statusResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
            Assert.Equal(StatusCodes.Status401Unauthorized, statusResult.StatusCode);
        }

        [Fact]
        public async Task InvokeAsync_WhenTimestampExceedsClockSkewTolerance_Returns401Unauthorized() {
            // Arrange: Timestamp 6 minutes in the past with 5-minute tolerance
            FakeTimeProvider timeProvider = new();
            DateTimeOffset now = new(2026, 8, 22, 12, 0, 0, TimeSpan.Zero);
            timeProvider.SetUtcNow(now);

            const string body = "{\"OrderId\":\"ORD-1\"}";
            UnixTimestamp expiredTimestamp = UnixTimestamp.FromSeconds(now.AddMinutes(-6).ToUnixTimeSeconds());
            WebhookSignature sig = this._signer.Sign(Encoding.UTF8.GetBytes(body), Encoding.UTF8.GetBytes(SecretKey), expiredTimestamp);

            (DefaultHttpContext httpContext, _, _) = CreateContext(body, sig.HeaderValue, timeProvider);
            WebhookReceiverEndpointMetadata metadata = new() {
                SecretResolver = new SecretWebhookSecretResolver(Secret.From(SecretKey)),
                Tolerance = TimeSpan.FromMinutes(5)
            };
            WebhookReceiverEndpointFilter<OrderCreatedWebhookEvent> filter = new(metadata);
            EndpointFilterInvocationContext invocationContext = new DefaultEndpointFilterInvocationContext(httpContext);

            // Act
            object? result = await filter.InvokeAsync(invocationContext, static ctx => ValueTask.FromResult<object?>(Results.Ok()));

            // Assert
            IStatusCodeHttpResult statusResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
            Assert.Equal(StatusCodes.Status401Unauthorized, statusResult.StatusCode);
        }

        [Fact]
        public async Task InvokeAsync_WhenTimestampIsWithinTolerance_SucceedsWith200Ok() {
            // Arrange
            FakeTimeProvider timeProvider = new();
            DateTimeOffset now = new(2026, 8, 22, 12, 0, 0, TimeSpan.Zero);
            timeProvider.SetUtcNow(now);

            const string body = "{\"OrderId\":\"ORD-VALID\"}";
            UnixTimestamp validTimestamp = UnixTimestamp.FromSeconds(now.AddMinutes(-4).ToUnixTimeSeconds());
            WebhookSignature sig = this._signer.Sign(Encoding.UTF8.GetBytes(body), Encoding.UTF8.GetBytes(SecretKey), validTimestamp);

            (DefaultHttpContext httpContext, _, _) = CreateContext(body, sig.HeaderValue, timeProvider);
            WebhookReceiverEndpointMetadata metadata = new() {
                SecretResolver = new SecretWebhookSecretResolver(Secret.From(SecretKey)),
                Tolerance = TimeSpan.FromMinutes(5)
            };
            WebhookReceiverEndpointFilter<OrderCreatedWebhookEvent> filter = new(metadata, static () => Results.Ok());
            EndpointFilterInvocationContext invocationContext = new DefaultEndpointFilterInvocationContext(httpContext);

            // Act
            object? result = await filter.InvokeAsync(invocationContext, static ctx => ValueTask.FromResult<object?>(Results.Ok()));

            // Assert
            IStatusCodeHttpResult statusResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
            Assert.Equal(StatusCodes.Status200OK, statusResult.StatusCode);
        }
        [Fact]
        public async Task InvokeAsync_WhenRequireSignatureIsFalse_AllowsUnsignedRequests() {
            // Arrange
            (DefaultHttpContext httpContext, _, _) = CreateContext("{\"OrderId\":\"ORD-UNSIGNED\"}");
            WebhookReceiverEndpointMetadata metadata = new() {
                RequireSignature = false
            };
            WebhookReceiverEndpointFilter<OrderCreatedWebhookEvent> filter = new(metadata, static () => Results.Ok());
            EndpointFilterInvocationContext invocationContext = new DefaultEndpointFilterInvocationContext(httpContext);

            // Act
            object? result = await filter.InvokeAsync(invocationContext, static ctx => ValueTask.FromResult<object?>(Results.Ok()));

            // Assert
            IStatusCodeHttpResult statusResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
            Assert.Equal(StatusCodes.Status200OK, statusResult.StatusCode);
        }

        [Fact]
        public async Task InvokeAsync_WhenSignatureIsRequired_ButNoResolverConfigured_ThrowsInvalidOperationException() {
            // Arrange
            (DefaultHttpContext httpContext, _, _) = CreateContext("{\"OrderId\":\"ORD-1\"}", "t=1700000000,v1=abc");
            WebhookReceiverEndpointMetadata metadata = new() {
                RequireSignature = true,
                SecretResolver = null
            };
            WebhookReceiverEndpointFilter<OrderCreatedWebhookEvent> filter = new(metadata);
            EndpointFilterInvocationContext invocationContext = new DefaultEndpointFilterInvocationContext(httpContext);

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                filter.InvokeAsync(invocationContext, static ctx => ValueTask.FromResult<object?>(Results.Ok())).AsTask());
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    // 2. BODY INSPECTION, DOS PROTECTION & DESERIALIZATION
    // ────────────────────────────────────────────────────────────────────────

    public sealed class TheBodyInspectionAndDoS {
        [Fact]
        public async Task InvokeAsync_WhenBodyIsEmpty_Returns400BadRequest() {
            // Arrange
            (DefaultHttpContext httpContext, _, _) = CreateContext(string.Empty);
            WebhookReceiverEndpointMetadata metadata = new() { RequireSignature = false };
            WebhookReceiverEndpointFilter<OrderCreatedWebhookEvent> filter = new(metadata);
            EndpointFilterInvocationContext invocationContext = new DefaultEndpointFilterInvocationContext(httpContext);

            // Act
            object? result = await filter.InvokeAsync(invocationContext, static ctx => ValueTask.FromResult<object?>(Results.Ok()));

            // Assert
            IStatusCodeHttpResult statusResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
            Assert.Equal(StatusCodes.Status400BadRequest, statusResult.StatusCode);
        }

        [Fact]
        public async Task InvokeAsync_WhenJsonIsMalformed_Returns400BadRequest() {
            // Arrange
            (DefaultHttpContext httpContext, _, _) = CreateContext("{ invalid-json-payload ");
            WebhookReceiverEndpointMetadata metadata = new() { RequireSignature = false };
            WebhookReceiverEndpointFilter<OrderCreatedWebhookEvent> filter = new(metadata);
            EndpointFilterInvocationContext invocationContext = new DefaultEndpointFilterInvocationContext(httpContext);

            // Act
            object? result = await filter.InvokeAsync(invocationContext, static ctx => ValueTask.FromResult<object?>(Results.Ok()));

            // Assert
            IStatusCodeHttpResult statusResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
            Assert.Equal(StatusCodes.Status400BadRequest, statusResult.StatusCode);
        }

        [Fact]
        public async Task InvokeAsync_WhenContentLengthHeaderExceedsMaxRequestBodyBytes_Returns413PayloadTooLarge() {
            // Arrange: Content-Length header indicates oversized payload
            string largeBody = new('x', 200);
            (DefaultHttpContext httpContext, _, _) = CreateContext(largeBody);
            WebhookReceiverEndpointMetadata metadata = new() {
                MaxRequestBodyBytes = 100,
                RequireSignature = false
            };
            WebhookReceiverEndpointFilter<OrderCreatedWebhookEvent> filter = new(metadata);
            EndpointFilterInvocationContext invocationContext = new DefaultEndpointFilterInvocationContext(httpContext);

            // Act
            object? result = await filter.InvokeAsync(invocationContext, static ctx => ValueTask.FromResult<object?>(Results.Ok()));

            // Assert
            IStatusCodeHttpResult statusResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
            Assert.Equal(StatusCodes.Status413PayloadTooLarge, statusResult.StatusCode);
        }

        [Fact]
        public async Task InvokeAsync_WhenChunkedTransferStreamExceedsMaxRequestBodyBytes_Returns413PayloadTooLarge() {
            // Arrange: Chunked transfer without Content-Length header
            string largeBody = new('x', 500);
            (DefaultHttpContext httpContext, _, _) = CreateContext(largeBody, setContentLength: false);
            WebhookReceiverEndpointMetadata metadata = new() {
                MaxRequestBodyBytes = 128,
                RequireSignature = false
            };
            WebhookReceiverEndpointFilter<OrderCreatedWebhookEvent> filter = new(metadata);
            EndpointFilterInvocationContext invocationContext = new DefaultEndpointFilterInvocationContext(httpContext);

            // Act
            object? result = await filter.InvokeAsync(invocationContext, static ctx => ValueTask.FromResult<object?>(Results.Ok()));

            // Assert
            IStatusCodeHttpResult statusResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
            Assert.Equal(StatusCodes.Status413PayloadTooLarge, statusResult.StatusCode);
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    // 3. IDEMPOTENCY & DEDUPLICATION LIFECYCLE
    // ────────────────────────────────────────────────────────────────────────

    public sealed class TheIdempotencyAndDeduplication {
        private readonly HmacSha256WebhookSigner _signer = new();

        [Fact]
        public async Task InvokeAsync_WhenDuplicateRequestArrives_ShortCircuitsWith200Ok_WithoutInvokingHandler() {
            // Arrange
            const string body = "{\"OrderId\":\"ORD-DUP\"}";
            UnixTimestamp now = UnixTimestamp.Now;
            WebhookSignature sig = this._signer.Sign(Encoding.UTF8.GetBytes(body), Encoding.UTF8.GetBytes(SecretKey), now);

            (DefaultHttpContext httpContext, _, _) = CreateContext(body, sig.HeaderValue);

            int handlerInvocationCount = 0;
            Delegate handler = () => { handlerInvocationCount++; return Results.Ok(); };

            WebhookReceiverEndpointMetadata metadata = new() {
                SecretResolver = new SecretWebhookSecretResolver(Secret.From(SecretKey)),
                EnforceIdempotency = true
            };
            WebhookReceiverEndpointFilter<OrderCreatedWebhookEvent> filter = new(metadata, handler);
            EndpointFilterInvocationContext invocationContext = new DefaultEndpointFilterInvocationContext(httpContext);

            // Act 1: Initial delivery
            object? result1 = await filter.InvokeAsync(invocationContext, static ctx => ValueTask.FromResult<object?>(Results.Ok()));
            Assert.Equal(1, handlerInvocationCount);

            // Act 2: Duplicate delivery
            httpContext.Request.Body.Position = 0;
            object? result2 = await filter.InvokeAsync(invocationContext, static ctx => ValueTask.FromResult<object?>(Results.Ok()));

            // Assert: Handler is not invoked a second time, 200 OK returned
            Assert.Equal(1, handlerInvocationCount);
            IStatusCodeHttpResult statusResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result2);
            Assert.Equal(StatusCodes.Status200OK, statusResult.StatusCode);
        }

        [Fact]
        public async Task InvokeAsync_WhenHandlerThrowsException_DoesNotCommitIdempotencyKey() {
            // Arrange
            const string body = "{\"OrderId\":\"ORD-FAIL\"}";
            UnixTimestamp now = UnixTimestamp.Now;
            WebhookSignature sig = this._signer.Sign(Encoding.UTF8.GetBytes(body), Encoding.UTF8.GetBytes(SecretKey), now);

            (DefaultHttpContext httpContext, _, InMemoryIdempotencyStore store) = CreateContext(body, sig.HeaderValue);

            Action failingHandler = static () => throw new InvalidOperationException("Simulated database failure");

            WebhookReceiverEndpointMetadata metadata = new() {
                SecretResolver = new SecretWebhookSecretResolver(Secret.From(SecretKey)),
                EnforceIdempotency = true
            };
            WebhookReceiverEndpointFilter<OrderCreatedWebhookEvent> filter = new(metadata, failingHandler);
            EndpointFilterInvocationContext invocationContext = new DefaultEndpointFilterInvocationContext(httpContext);

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                filter.InvokeAsync(invocationContext, static ctx => ValueTask.FromResult<object?>(Results.Ok())).AsTask());

            // Assert: Idempotency key must not be marked as processed
            IdempotencyKey? expectedKey = WebhookReceiverPolicy.DefaultIdempotencyKeyExtractor(httpContext, Encoding.UTF8.GetBytes(body));
            Assert.NotNull(expectedKey);
            bool isCommitted = await store.ContainsAsync(expectedKey.Value);
            Assert.False(isCommitted);
        }

        [Fact]
        public async Task InvokeAsync_WhenIdempotencyWindowExpires_ProcessesEventAgain() {
            // Arrange
            FakeTimeProvider timeProvider = new();
            DateTimeOffset now = new(2026, 8, 22, 10, 0, 0, TimeSpan.Zero);
            timeProvider.SetUtcNow(now);

            const string body = "{\"OrderId\":\"ORD-WINDOW\"}";
            UnixTimestamp initialTimestamp = UnixTimestamp.FromSeconds(now.ToUnixTimeSeconds());
            WebhookSignature sig1 = this._signer.Sign(Encoding.UTF8.GetBytes(body), Encoding.UTF8.GetBytes(SecretKey), initialTimestamp);

            (DefaultHttpContext httpContext, _, _) = CreateContext(body, sig1.HeaderValue, timeProvider);

            int handlerInvocationCount = 0;
            Delegate handler = () => { handlerInvocationCount++; return Results.Ok(); };

            WebhookReceiverEndpointMetadata metadata = new() {
                SecretResolver = new SecretWebhookSecretResolver(Secret.From(SecretKey)),
                EnforceIdempotency = true,
                IdempotencyWindow = TimeSpan.FromMinutes(10)
            };
            WebhookReceiverEndpointFilter<OrderCreatedWebhookEvent> filter = new(metadata, handler);
            EndpointFilterInvocationContext invocationContext = new DefaultEndpointFilterInvocationContext(httpContext);

            // 1. Initial attempt
            await filter.InvokeAsync(invocationContext, static ctx => ValueTask.FromResult<object?>(Results.Ok()));
            Assert.Equal(1, handlerInvocationCount);

            // Advance time past the 10-minute idempotency window
            timeProvider.Advance(TimeSpan.FromMinutes(11));

            // Re-sign with current timestamp
            UnixTimestamp renewedTimestamp = UnixTimestamp.FromSeconds(timeProvider.GetUtcNow().ToUnixTimeSeconds());
            WebhookSignature sig2 = this._signer.Sign(Encoding.UTF8.GetBytes(body), Encoding.UTF8.GetBytes(SecretKey), renewedTimestamp);
            httpContext.Request.Headers[WebhookHeaderNames.WebhookSignature] = sig2.HeaderValue;
            httpContext.Request.Body.Position = 0;

            // 2. Attempt after window expiry
            await filter.InvokeAsync(invocationContext, static ctx => ValueTask.FromResult<object?>(Results.Ok()));

            // Assert: Handler is invoked again because the window expired
            Assert.Equal(2, handlerInvocationCount);
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    // 4. HANDLER INVOCATION & DI DISPATCH
    // ────────────────────────────────────────────────────────────────────────

    public sealed class TheHandlerInvocationAndDispatch {
        [Fact]
        public async Task InvokeAsync_WhenDelegateHandlerHasMultipleDiParameters_ResolvesAndInvokesSuccessfully() {
            // Arrange
            const string body = "{\"OrderId\":\"ORD-MULTI-DI\"}";
            OrderCreatedWebhookEvent? capturedEvent = null;
            WebhookReceiverContext<OrderCreatedWebhookEvent>? capturedContext = null;
            CustomAuditService? capturedService = null;

            Action<ServiceCollection> registerCustomService = static sc => sc.AddSingleton<CustomAuditService>();

            (DefaultHttpContext httpContext, _, _) = CreateContext(body, configureServices: registerCustomService);

            Delegate handler = (
                OrderCreatedWebhookEvent @event,
                WebhookReceiverContext<OrderCreatedWebhookEvent> ctx,
                CustomAuditService service,
                CancellationToken ct) => {
                    capturedEvent = @event;
                    capturedContext = ctx;
                    capturedService = service;
                    return Results.Ok();
                };

            WebhookReceiverEndpointMetadata metadata = new() { RequireSignature = false };
            WebhookReceiverEndpointFilter<OrderCreatedWebhookEvent> filter = new(metadata, handler);
            EndpointFilterInvocationContext invocationContext = new DefaultEndpointFilterInvocationContext(httpContext);

            // Act
            object? result = await filter.InvokeAsync(invocationContext, static ctx => ValueTask.FromResult<object?>(Results.Ok()));

            // Assert
            Assert.NotNull(capturedEvent);
            Assert.Equal("ORD-MULTI-DI", capturedEvent.OrderId);
            Assert.NotNull(capturedContext);
            Assert.NotNull(capturedService);
            IStatusCodeHttpResult statusResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
            Assert.Equal(StatusCodes.Status200OK, statusResult.StatusCode);
        }

        [Fact]
        public async Task InvokeAsync_WhenNoDelegateProvided_DispatchesToClassBasedHandlerFromDi() {
            // Arrange
            const string body = "{\"OrderId\":\"ORD-CLASS-HANDLER\"}";
            Action<ServiceCollection> registerClassHandler = static sc => sc.AddScoped<IWebhookReceiverHandler<OrderCreatedWebhookEvent>, StubOrderCreatedHandler>();

            (DefaultHttpContext httpContext, _, _) = CreateContext(body, configureServices: registerClassHandler);

            WebhookReceiverEndpointMetadata metadata = new() { RequireSignature = false };
            WebhookReceiverEndpointFilter<OrderCreatedWebhookEvent> filter = new(metadata, delegateHandler: null);
            EndpointFilterInvocationContext invocationContext = new DefaultEndpointFilterInvocationContext(httpContext);

            // Act
            object? result = await filter.InvokeAsync(invocationContext, static ctx => ValueTask.FromResult<object?>(Results.Ok()));

            // Assert
            StubOrderCreatedHandler handlerInstance = (StubOrderCreatedHandler)httpContext.RequestServices.GetRequiredService<IWebhookReceiverHandler<OrderCreatedWebhookEvent>>();
            Assert.True(handlerInstance.WasInvoked);
            Assert.Equal("ORD-CLASS-HANDLER", handlerInstance.LastHandledEvent?.OrderId);
        }

        [Fact]
        public async Task InvokeAsync_WhenNoDelegateAndNoDiHandlerRegistered_ThrowsInvalidOperationException() {
            // Arrange: No handler delegate and no IWebhookReceiverHandler in DI
            (DefaultHttpContext httpContext, _, _) = CreateContext("{\"OrderId\":\"ORD-1\"}");
            WebhookReceiverEndpointMetadata metadata = new() { RequireSignature = false };
            WebhookReceiverEndpointFilter<OrderCreatedWebhookEvent> filter = new(metadata, delegateHandler: null);
            EndpointFilterInvocationContext invocationContext = new DefaultEndpointFilterInvocationContext(httpContext);

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                filter.InvokeAsync(invocationContext, static ctx => ValueTask.FromResult<object?>(Results.Ok())).AsTask());
        }

        [Fact]
        public async Task InvokeAsync_WhenDelegateReturnsValueTask_AwaitsAndReturnsResult() {
            // Arrange
            (DefaultHttpContext httpContext, _, _) = CreateContext("{\"OrderId\":\"ORD-VALUETASK\"}");

            Delegate asyncValueTaskHandler = static (OrderCreatedWebhookEvent e) => {
                return ValueTask.FromResult(Results.Accepted());
            };

            WebhookReceiverEndpointMetadata metadata = new() { RequireSignature = false };
            WebhookReceiverEndpointFilter<OrderCreatedWebhookEvent> filter = new(metadata, asyncValueTaskHandler);
            EndpointFilterInvocationContext invocationContext = new DefaultEndpointFilterInvocationContext(httpContext);

            // Act
            object? result = await filter.InvokeAsync(invocationContext, static ctx => ValueTask.FromResult<object?>(Results.Ok()));

            // Assert
            IStatusCodeHttpResult statusResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
            Assert.Equal(StatusCodes.Status202Accepted, statusResult.StatusCode);
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    // 5. POLICY RESOLUTION & HIERARCHY
    // ────────────────────────────────────────────────────────────────────────

    public sealed class ThePolicyResolution {
        [Fact]
        public async Task InvokeAsync_WhenNamedPolicyConfigured_InheritsPolicySettings() {
            // Arrange
            const string body = "{\"OrderId\":\"ORD-POLICY\"}";
            Action<ServiceCollection> configureOptions = static sc => {
                sc.Configure<WebhookInboundOptions>(options => {
                    options.Policies["Stripe"] = new WebhookReceiverPolicy {
                        Name = "Stripe",
                        HeaderName = "Stripe-Signature",
                        MaxRequestBodyBytes = 128 * 1024,
                        RequireSignature = false
                    };
                });
            };

            (DefaultHttpContext httpContext, _, _) = CreateContext(body, configureServices: configureOptions);

            WebhookReceiverEndpointMetadata metadata = new() {
                PolicyName = "Stripe"
            };
            WebhookReceiverEndpointFilter<OrderCreatedWebhookEvent> filter = new(metadata, static () => Results.Ok());
            EndpointFilterInvocationContext invocationContext = new DefaultEndpointFilterInvocationContext(httpContext);

            // Act
            object? result = await filter.InvokeAsync(invocationContext, static ctx => ValueTask.FromResult<object?>(Results.Ok()));

            // Assert
            IStatusCodeHttpResult statusResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
            Assert.Equal(StatusCodes.Status200OK, statusResult.StatusCode);
        }
    }

    // ── Test Doubles ──

    private sealed class CustomAuditService;

    private sealed class StubOrderCreatedHandler : IWebhookReceiverHandler<OrderCreatedWebhookEvent> {
        public bool WasInvoked { get; private set; }
        public OrderCreatedWebhookEvent? LastHandledEvent { get; private set; }

        public Task HandleAsync(WebhookReceiverContext<OrderCreatedWebhookEvent> context, CancellationToken cancellationToken = default) {
            this.WasInvoked = true;
            this.LastHandledEvent = context.Payload;
            return Task.CompletedTask;
        }
    }
}