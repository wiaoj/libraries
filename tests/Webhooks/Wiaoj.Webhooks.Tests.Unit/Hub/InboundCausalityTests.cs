using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System.Text;
using Wiaoj.Serialization;
using Wiaoj.Serialization.SystemTextJson;
using Wiaoj.Webhooks.AspNetCore;
using Wiaoj.Webhooks.AspNetCore.Filters;
using Wiaoj.Webhooks.AspNetCore.Metadata;
using Wiaoj.Webhooks.Idempotency;
using Wiaoj.Webhooks.Internal;
using Wiaoj.Webhooks.Tests.Unit.TestData;

namespace Wiaoj.Webhooks.Tests.Unit.Hub;

/// <summary>
/// The inbound filters reject requests that went too far and hand the causality of accepted ones to the handler, so the
/// webhooks it publishes carry the hop count and chain forward.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Feature", "LoopDetection")]
[Trait("Component", "InboundReceiver")]
public sealed class InboundCausalityTests {
    private sealed record OrderPayload(string OrderId);

    private const string Body = """{"OrderId":"ORD-1"}""";

    private static DefaultHttpContext CreateContext(string? hops, string? chain, Action<WebhookInboundOptions>? configure = null) {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IWebhookEventRegistry>(new WebhookEventRegistry(new WebhookEventRegistryOptions()));
        services.AddSingleton<ISerializer<WebhookSerializerKey>, SystemTextJsonSerializer<WebhookSerializerKey>>();
        services.AddSingleton<IIdempotencyStore, InMemoryIdempotencyStore>();
        services.AddOptions<WebhookInboundOptions>();
        if(configure is not null) {
            services.Configure(configure);
        }

        DefaultHttpContext httpContext = new() {
            RequestServices = services.BuildServiceProvider()
        };
        httpContext.Request.Path = "/hooks";
        httpContext.Request.Method = "POST";

        byte[] bodyBytes = Encoding.UTF8.GetBytes(Body);
        httpContext.Request.Body = new MemoryStream(bodyBytes);
        httpContext.Request.ContentLength = bodyBytes.Length;
        httpContext.Request.Headers["X-Event"] = "order.created";

        if(hops is not null) {
            httpContext.Request.Headers[WebhookHeaderNames.WebhookHopCount] = hops;
        }

        if(chain is not null) {
            httpContext.Request.Headers[WebhookHeaderNames.WebhookCausalChain] = chain;
        }

        return httpContext;
    }

    private static (WebhookHubEndpointFilter Filter, Func<WebhookCausality?> Seen) CreateHub(string? policyName = null) {
        WebhookCausality? seen = null;

        WebhookHubMetadata metadata = new() {
            PolicyName = policyName,
            RequireSignature = false,
            EnforceIdempotency = false,
            EventExtractor = new HeaderEventDiscriminatorExtractor("X-Event")
        };
        metadata.AddRegistration(new WebhookHubRegistration("order.created", typeof(OrderPayload), (OrderPayload _) => {
            seen = WebhookCausalityScope.Current;
            return Results.Ok();
        }));

        return (new WebhookHubEndpointFilter(metadata), () => seen);
    }

    private static async Task<int> InvokeAsync(IEndpointFilter filter, DefaultHttpContext httpContext) {
        object? result = await filter.InvokeAsync(
            new DefaultEndpointFilterInvocationContext(httpContext),
            static _ => ValueTask.FromResult<object?>(Results.Ok()));

        return Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode!.Value;
    }

    public sealed class TheHubFilter {

        [Fact]
        public async Task InvokeAsync_RunsTheHandlerInsideTheRequestCausality() {
            (WebhookHubEndpointFilter filter, Func<WebhookCausality?> seen) = CreateHub();

            int status = await InvokeAsync(filter, CreateContext("2", "svc-a, svc-b"));

            Assert.Equal(StatusCodes.Status200OK, status);
            Assert.Equal(new WebhookCausality(2, "svc-a, svc-b"), seen());
            Assert.True(WebhookCausalityScope.Current.IsNone);
        }

        [Fact]
        public async Task InvokeAsync_ByDefault_RejectsAHopCountAboveMaxHops() {
            (WebhookHubEndpointFilter filter, Func<WebhookCausality?> seen) = CreateHub();

            int status = await InvokeAsync(filter, CreateContext("6", null));

            Assert.Equal(StatusCodes.Status422UnprocessableEntity, status);
            Assert.Null(seen());
        }

        [Fact]
        public async Task InvokeAsync_AcceptsAHopCountEqualToMaxHops() {
            // An outbound sender with the same MaxHops sends hop MaxHops; the receiver must not reject it.
            (WebhookHubEndpointFilter filter, Func<WebhookCausality?> seen) = CreateHub();

            int status = await InvokeAsync(filter, CreateContext("5", null));

            Assert.Equal(StatusCodes.Status200OK, status);
            Assert.Equal(5, seen()!.Value.HopCount);
        }

        [Fact]
        public async Task InvokeAsync_WhenTheNamedPolicySetsInstanceId_RejectsARequestWhoseChainContainsIt() {
            (WebhookHubEndpointFilter filter, Func<WebhookCausality?> seen) = CreateHub("Peers");

            int status = await InvokeAsync(filter, CreateContext("2", "svc-a, svc-b", options => {
                options.Policies["Peers"] = new WebhookReceiverPolicy {
                    Name = "Peers",
                    RequireSignature = false,
                    InstanceId = "svc-a"
                };
            }));

            Assert.Equal(StatusCodes.Status422UnprocessableEntity, status);
            Assert.Null(seen());
        }

        [Fact]
        public async Task InvokeAsync_WhenTheNamedPolicySetsMaxHops_UsesIt() {
            (WebhookHubEndpointFilter filter, _) = CreateHub("Strict");

            int status = await InvokeAsync(filter, CreateContext("3", null, options => {
                options.Policies["Strict"] = new WebhookReceiverPolicy {
                    Name = "Strict",
                    RequireSignature = false
                }.WithLoopDetection(maxHops: 2);
            }));

            Assert.Equal(StatusCodes.Status422UnprocessableEntity, status);
        }

        [Fact]
        public async Task InvokeAsync_WhenLoopDetectionIsOff_NeitherRejectsNorCapturesCausality() {
            (WebhookHubEndpointFilter filter, Func<WebhookCausality?> seen) = CreateHub();

            int status = await InvokeAsync(filter, CreateContext("99", "svc-a", options => options.DefaultPolicy.WithoutLoopDetection()));

            Assert.Equal(StatusCodes.Status200OK, status);
            Assert.True(seen()!.Value.IsNone);
        }
    }

    public sealed class TheReceiverFilter {

        [Fact]
        public async Task InvokeAsync_RunsTheHandlerInsideTheRequestCausality() {
            WebhookCausality? seen = null;
            WebhookReceiverEndpointFilter<OrderCreatedWebhookEvent> filter = new(
                new WebhookReceiverEndpointMetadata { RequireSignature = false, EnforceIdempotency = false },
                (OrderCreatedWebhookEvent _) => {
                    seen = WebhookCausalityScope.Current;
                    return Results.Ok();
                });

            DefaultHttpContext httpContext = CreateContext("3", "svc-a, svc-b, svc-c");
            httpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes("""{"OrderId":"ORD-1","Amount":1.0}"""));
            httpContext.Request.ContentLength = httpContext.Request.Body.Length;

            int status = await InvokeAsync(filter, httpContext);

            Assert.Equal(StatusCodes.Status200OK, status);
            Assert.Equal(new WebhookCausality(3, "svc-a, svc-b, svc-c"), seen);
        }
    }
}
