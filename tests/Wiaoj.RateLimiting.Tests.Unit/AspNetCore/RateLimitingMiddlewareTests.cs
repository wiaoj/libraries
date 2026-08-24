using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using System.Text.Json;
using Wiaoj.RateLimiting.AspNetCore;
using Wiaoj.RateLimiting.AspNetCore.Middleware;

namespace Wiaoj.RateLimiting.Tests.Unit.AspNetCore;

public sealed class RateLimitingMiddlewareTests {
    private sealed class FakeAlgorithm : IRateLimitAlgorithm {
        public int LastCostAcquired { get; private set; }
        public string? LastKeyAcquired { get; private set; }
        public RateLimitDecision DecisionToReturn { get; set; } = RateLimitDecision.Allowed(5);

        public ValueTask<RateLimitDecision> TryAcquireAsync(string key, int cost = 1, CancellationToken cancellationToken = default) {
            this.LastKeyAcquired = key;
            this.LastCostAcquired = cost;
            return ValueTask.FromResult(this.DecisionToReturn);
        }
    }

    private static (RateLimitingMiddleware Middleware, FakeAlgorithm Algorithm, RateLimitingOptions Options) CreateMiddleware(
        RequestDelegate? next = null,
        Action<RateLimitingOptions>? configure = null) {

        RateLimitingOptions options = new();
        configure?.Invoke(options);

        IOptionsMonitor<RateLimitingOptions> optionsMonitor = new TestOptionsMonitor<RateLimitingOptions>(options);
        FakeAlgorithm algorithm = new();
        next ??= static _ => Task.CompletedTask;

        RateLimitingMiddleware middleware = new(next, algorithm, optionsMonitor);
        return (middleware, algorithm, options);
    }

    private sealed class TestOptionsMonitor<T>(T currentValue) : IOptionsMonitor<T> where T : class {
        public T CurrentValue => currentValue;
        public T Get(string? name) {
            return currentValue;
        }

        public IDisposable? OnChange(Action<T, string?> listener) {
            return null;
        }
    }

    [Fact]
    public async Task InvokeAsync_WhenRequestAllowed_SetsRemainingHeaderAndCallsNext() {
        bool nextCalled = false;
        (RateLimitingMiddleware middleware, FakeAlgorithm algorithm, _) = CreateMiddleware(
            next: _ => { nextCalled = true; return Task.CompletedTask; });

        DefaultHttpContext context = new();
        algorithm.DecisionToReturn = RateLimitDecision.Allowed(remaining: 9);

        await middleware.InvokeAsync(context);

        Assert.True(nextCalled);
        Assert.Equal("9", context.Response.Headers[RateLimitConstants.Headers.RateLimitRemaining]);
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_WhenRequestDenied_Emits429WithRfcHeadersAndProblemDetailsJson() {
        (RateLimitingMiddleware middleware, FakeAlgorithm algorithm, _) = CreateMiddleware(configure: opt => {
            opt.UseProblemDetails = true;
        });

        DefaultHttpContext context = new();
        context.Response.Body = new MemoryStream();
        algorithm.DecisionToReturn = RateLimitDecision.Denied(TimeSpan.FromSeconds(3.2), remaining: 0);

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status429TooManyRequests, context.Response.StatusCode);
        Assert.Equal("4", context.Response.Headers[RateLimitConstants.Headers.RetryAfter]); // Ceil(3.2s) = 4s
        Assert.Equal("4", context.Response.Headers[RateLimitConstants.Headers.RateLimitReset]);
        Assert.Equal("0", context.Response.Headers[RateLimitConstants.Headers.RateLimitRemaining]);
        Assert.Equal(RateLimitConstants.ContentTypes.ProblemJson, context.Response.ContentType);

        // Verify RFC 7807 ProblemDetails body
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        ProblemDetails? problem = await JsonSerializer.DeserializeAsync<ProblemDetails>(
            context.Response.Body,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotNull(problem);
        Assert.Equal(429, problem.Status);
        Assert.Equal(RateLimitConstants.Uris.Rfc6585, problem.Type);

        Assert.NotNull(problem.Extensions);
        Assert.True(problem.Extensions.TryGetValue("retryAfter", out object? rawRetryAfter));
        Assert.NotNull(rawRetryAfter);
        JsonElement typed = Assert.IsType<JsonElement>(rawRetryAfter);

        JsonElement retryAfterElement = typed;
        Assert.Equal(4, retryAfterElement.GetInt32());
    }

    [Fact]
    public async Task InvokeAsync_WhenEndpointHasDisableRateLimiting_BypassesRateLimiterEntirely() {
        (RateLimitingMiddleware middleware, FakeAlgorithm algorithm, _) = CreateMiddleware();

        DefaultHttpContext context = new();
        EndpointMetadataCollection metadata = new(new DisableRateLimitingAttribute());
        context.Features.Set<IEndpointFeature>(new EndpointFeature { Endpoint = new Endpoint(static _ => Task.CompletedTask, metadata, "DisabledEndpoint") });

        await middleware.InvokeAsync(context);

        Assert.Null(algorithm.LastKeyAcquired); // Algorithm was not even called
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_WhenEndpointHasStaticRateLimitCost_PassesStaticCostToAlgorithm() {
        (RateLimitingMiddleware middleware, FakeAlgorithm algorithm, _) = CreateMiddleware();

        DefaultHttpContext context = new();
        EndpointMetadataCollection metadata = new(new RateLimitMetadata { Cost = 5 });
        context.Features.Set<IEndpointFeature>(new EndpointFeature { Endpoint = new Endpoint(static _ => Task.CompletedTask, metadata, "StaticCostEndpoint") });

        await middleware.InvokeAsync(context);

        Assert.Equal(5, algorithm.LastCostAcquired);
    }

    [Fact]
    public async Task InvokeAsync_WhenEndpointHasDynamicCostResolver_ComputesCostFromRequestBatch() {
        (RateLimitingMiddleware middleware, FakeAlgorithm algorithm, _) = CreateMiddleware();

        DefaultHttpContext context = new();
        context.Request.QueryString = new QueryString("?count=17"); // Bulk batch count

        EndpointMetadataCollection metadata = new(new RateLimitMetadata {
            DynamicCostResolver = ctx => ctx.Request.Query.TryGetValue("count", out StringValues val) && int.TryParse(val, out int count) ? count : 1
        });
        context.Features.Set<IEndpointFeature>(new EndpointFeature { Endpoint = new Endpoint(static _ => Task.CompletedTask, metadata, "DynamicBulkEndpoint") });

        await middleware.InvokeAsync(context);

        Assert.Equal(17, algorithm.LastCostAcquired); // Exactly 17 units deducted from quota!
    }

    private sealed class EndpointFeature : IEndpointFeature {
        public Endpoint? Endpoint { get; set; }
    }
}