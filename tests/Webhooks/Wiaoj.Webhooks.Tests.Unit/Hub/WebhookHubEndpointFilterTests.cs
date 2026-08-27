using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
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
using Wiaoj.Webhooks.Tests.Unit.Fakes;

namespace Wiaoj.Webhooks.Tests.Unit.Hub;

[Trait("Category", "Unit")]
[Trait("Feature", "InboundReceiver")]
[Trait("Component", "WebhookHub")]
public sealed class WebhookHubEndpointFilterTests {  
    private sealed record PingPayload(string Zen);
    private sealed record OrderPayload(string OrderId, decimal Total);

    private static (DefaultHttpContext HttpContext, ServiceProvider ServiceProvider) CreateContext(
        string body,
        string? signatureHeader = null,
        Action<ServiceCollection>? configureServices = null) {

        ServiceCollection services = new();
        services.AddLogging();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IWebhookEventRegistry>(new WebhookEventRegistry(new WebhookEventRegistryOptions()));
        services.AddSingleton<ISerializer<WebhookSerializerKey>, SystemTextJsonSerializer<WebhookSerializerKey>>();
        services.AddSingleton<IIdempotencyStore, InMemoryIdempotencyStore>();
        services.AddOptions<WebhookInboundOptions>();

        configureServices?.Invoke(services);

        ServiceProvider sp = services.BuildServiceProvider();

        DefaultHttpContext httpContext = new() {
            RequestServices = sp
        };
        httpContext.Request.Path = "/api/webhooks/github";
        httpContext.Request.Method = "POST";

        byte[] bodyBytes = Encoding.UTF8.GetBytes(body);
        httpContext.Request.Body = new MemoryStream(bodyBytes);
        httpContext.Request.ContentLength = bodyBytes.Length;

        if(!string.IsNullOrWhiteSpace(signatureHeader)) {
            httpContext.Request.Headers[WebhookHeaderNames.WebhookSignature] = signatureHeader;
        }

        return (httpContext, sp);
    }

    [Fact]
    public async Task InvokeAsync_RoutesToCorrectHandler_BasedOnDiscriminator() {
        WebhookHubMetadata metadata = new() {
            RequireSignature = false,
            EventExtractor = new HeaderEventDiscriminatorExtractor("X-GitHub-Event")
        };

        string? executedEvent = null;

        metadata.AddRegistration(new WebhookHubRegistration("ping", typeof(PingPayload), (PingPayload p) => {
            executedEvent = $"ping:{p.Zen}";
            return Results.Ok();
        }));

        metadata.AddRegistration(new WebhookHubRegistration("order.created", typeof(OrderPayload), (OrderPayload o) => {
            executedEvent = $"order:{o.OrderId}";
            return Results.Ok();
        }));

        WebhookHubEndpointFilter filter = new(metadata);

        // 1. Send Ping Request
        const string pingBody = """{"Zen":"Non-blocking is better than blocking."}""";
        (DefaultHttpContext pingCtx, _) = CreateContext(pingBody);
        pingCtx.Request.Headers["X-GitHub-Event"] = "ping";

        object? result1 = await filter.InvokeAsync(new DefaultEndpointFilterInvocationContext(pingCtx), static _ => ValueTask.FromResult<object?>(Results.Ok()));

        Assert.Equal("ping:Non-blocking is better than blocking.", executedEvent);
        IStatusCodeHttpResult status1 = Assert.IsType<IStatusCodeHttpResult>(result1, exactMatch: false);
        Assert.Equal(StatusCodes.Status200OK, status1.StatusCode);

        // 2. Send Order Request
        const string orderBody = """{"OrderId":"ORD-999","Total":199.90}""";
        (DefaultHttpContext orderCtx, _) = CreateContext(orderBody);
        orderCtx.Request.Headers["X-GitHub-Event"] = "order.created";

        object? result2 = await filter.InvokeAsync(new DefaultEndpointFilterInvocationContext(orderCtx), static _ => ValueTask.FromResult<object?>(Results.Ok()));

        Assert.Equal("order:ORD-999", executedEvent);
        IStatusCodeHttpResult status2 = Assert.IsType<IStatusCodeHttpResult>(result2, exactMatch: false);
        Assert.Equal(StatusCodes.Status200OK, status2.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_WhenEventUnhandledAndIgnoreTrue_Returns200Ok() {
        WebhookHubMetadata metadata = new() {
            RequireSignature = false,
            EventExtractor = new HeaderEventDiscriminatorExtractor("X-GitHub-Event"),
            IgnoreUnhandledEvents = true
        };

        metadata.AddRegistration(new WebhookHubRegistration("order.created", typeof(OrderPayload), static () => Results.Ok()));

        WebhookHubEndpointFilter filter = new(metadata);

        // Send unhandled "star" event
        const string body = """{"action":"created"}""";
        (DefaultHttpContext ctx, _) = CreateContext(body);
        ctx.Request.Headers["X-GitHub-Event"] = "star";

        object? result = await filter.InvokeAsync(new DefaultEndpointFilterInvocationContext(ctx), static _ => ValueTask.FromResult<object?>(Results.Ok()));

        IStatusCodeHttpResult status = Assert.IsType<IStatusCodeHttpResult>(result, exactMatch: false);
        Assert.Equal(StatusCodes.Status200OK, status.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_WhenEventUnhandledAndIgnoreFalse_Returns400BadRequest() {
        WebhookHubMetadata metadata = new() {
            RequireSignature = false,
            EventExtractor = new HeaderEventDiscriminatorExtractor("X-GitHub-Event"),
            IgnoreUnhandledEvents = false
        };

        metadata.AddRegistration(new WebhookHubRegistration("order.created", typeof(OrderPayload), static () => Results.Ok()));

        WebhookHubEndpointFilter filter = new(metadata);

        const string body = """{"action":"deleted"}""";
        (DefaultHttpContext ctx, _) = CreateContext(body);
        ctx.Request.Headers["X-GitHub-Event"] = "unregistered_event";

        object? result = await filter.InvokeAsync(new DefaultEndpointFilterInvocationContext(ctx), static _ => ValueTask.FromResult<object?>(Results.Ok()));

        IStatusCodeHttpResult status = Assert.IsType<IStatusCodeHttpResult>(result, exactMatch: false);
        Assert.Equal(StatusCodes.Status400BadRequest, status.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_DispatchesToClassBasedHandler_FromDi() {
        WebhookHubMetadata metadata = new() {
            RequireSignature = false,
            EventExtractor = new JsonPropertyEventDiscriminatorExtractor("type")
        };

        metadata.AddRegistration(new WebhookHubRegistration("order.created", typeof(OrderPayload), typeof(StubOrderHandler)));

        Action<ServiceCollection> registerHandler = static sc => {
            sc.AddScoped<StubOrderHandler>();
            sc.AddScoped<IWebhookReceiverHandler<OrderPayload>, StubOrderHandler>();
        };

        const string body = """{"type":"order.created","OrderId":"ORD-CLASS","Total":50.0}""";
        (DefaultHttpContext ctx, ServiceProvider sp) = CreateContext(body, configureServices: registerHandler);

        WebhookHubEndpointFilter filter = new(metadata);

        object? result = await filter.InvokeAsync(new DefaultEndpointFilterInvocationContext(ctx), static _ => ValueTask.FromResult<object?>(Results.Ok()));

        IStatusCodeHttpResult status = Assert.IsType<IStatusCodeHttpResult>(result, exactMatch: false);
        Assert.Equal(StatusCodes.Status200OK, status.StatusCode);
    }
    [Fact]
    public async Task InvokeAsync_WhenNamedPolicyConfigured_InheritsCustomSignerAndHeaderSuccessfully() {
        // Arrange: Configure named "GitHub" policy with custom signer, header, and secret
        const string githubSecret = "ghsec_named_policy_test_key_12345";
        FakeGitHubWebhookSigner githubSigner = new();

        Action<ServiceCollection> configureNamedPolicy = sc => {
            sc.Configure<WebhookInboundOptions>(options => {
                options.Policies["GitHub"] = new WebhookReceiverPolicy {
                    Name = "GitHub",
                    HeaderName = "X-Hub-Signature-256",
                    Signer = githubSigner,
                    SecretResolver = new SecretWebhookSecretResolver(Secret.From(githubSecret)),
                    EventExtractor = new HeaderEventDiscriminatorExtractor("X-GitHub-Event"),
                    RequireSignature = true
                };
            });
        };

        const string payload = """{"ref":"refs/heads/main","pusher":"bertan"}""";
        WebhookSignature sig = githubSigner.Sign(
            Encoding.UTF8.GetBytes(payload),
            Encoding.UTF8.GetBytes(githubSecret),
            UnixTimestamp.Now);

        (DefaultHttpContext httpContext, _) = CreateContext(payload, configureServices: configureNamedPolicy);
        httpContext.Request.Headers["X-GitHub-Event"] = "push";
        httpContext.Request.Headers["X-Hub-Signature-256"] = $"sha256={sig.Signature}";

        // WebhookHubMetadata populated with PolicyName = "GitHub" via .UsePolicy("GitHub")
        WebhookHubMetadata metadata = new() {
            PolicyName = "GitHub"
        };

        bool handlerInvoked = false;
        metadata.AddRegistration(new WebhookHubRegistration("push", typeof(PushPayload), (PushPayload p) => {
            handlerInvoked = true;
            return Results.Ok();
        }));

        WebhookHubEndpointFilter filter = new(metadata);

        // Act
        object? result = await filter.InvokeAsync(
            new DefaultEndpointFilterInvocationContext(httpContext),
            static _ => ValueTask.FromResult<object?>(Results.Ok()));

        // Assert: Filter must inherit policy and succeed with 200 OK
        Assert.True(handlerInvoked);
        IStatusCodeHttpResult statusResult = Assert.IsType<IStatusCodeHttpResult>(result, exactMatch: false);
        Assert.Equal(StatusCodes.Status200OK, statusResult.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_WhenNamedPolicyConfigured_AndSignatureIsTampered_Returns401Unauthorized() {
        // Arrange
        const string githubSecret = "ghsec_named_policy_test_key_12345";
        FakeGitHubWebhookSigner githubSigner = new();

        Action<ServiceCollection> configureNamedPolicy = sc => {
            sc.Configure<WebhookInboundOptions>(options => {
                options.Policies["GitHub"] = new WebhookReceiverPolicy {
                    Name = "GitHub",
                    HeaderName = "X-Hub-Signature-256",
                    Signer = githubSigner,
                    SecretResolver = new SecretWebhookSecretResolver(Secret.From(githubSecret)),
                    EventExtractor = new HeaderEventDiscriminatorExtractor("X-GitHub-Event"),
                    RequireSignature = true
                };
            });
        };

        const string payload = """{"ref":"refs/heads/main","pusher":"bertan"}""";

        (DefaultHttpContext httpContext, _) = CreateContext(payload, configureServices: configureNamedPolicy);
        httpContext.Request.Headers["X-GitHub-Event"] = "push";
        httpContext.Request.Headers["X-Hub-Signature-256"] = "sha256=invalid_tampered_hash_00000000000000000000000000000000000000000000";

        WebhookHubMetadata metadata = new() {
            PolicyName = "GitHub"
        };

        metadata.AddRegistration(new WebhookHubRegistration("push", typeof(PushPayload), static () => Results.Ok()));

        WebhookHubEndpointFilter filter = new(metadata);

        // Act
        object? result = await filter.InvokeAsync(
            new DefaultEndpointFilterInvocationContext(httpContext),
            static _ => ValueTask.FromResult<object?>(Results.Ok()));

        // Assert: Invalid signature must be rejected with 401 Unauthorized
        IStatusCodeHttpResult statusResult = Assert.IsType<IStatusCodeHttpResult>(result, exactMatch: false);
        Assert.Equal(StatusCodes.Status401Unauthorized, statusResult.StatusCode);
    }

    private sealed record PushPayload(string Ref, string Pusher);

    private sealed class StubOrderHandler : IWebhookReceiverHandler<OrderPayload> {
        public Task HandleAsync(WebhookReceiverContext<OrderPayload> context, CancellationToken cancellationToken = default) {
            Assert.Equal("ORD-CLASS", context.Payload.OrderId);
            return Task.CompletedTask;
        }
    }
}