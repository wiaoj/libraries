using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using System.Text.Json;
using Wiaoj.RateLimiting.AspNetCore;
using Wiaoj.RateLimiting.AspNetCore.Middleware;

namespace Wiaoj.RateLimiting.Tests.Unit.AspNetCore;

[Trait("Category", "Unit")]
[Trait("Component", "AspNetCore")]
[Trait("Feature", "Middleware")]
public sealed class RateLimitingMiddlewareTests {

    public sealed class TheAllowedPipelineExecution {

        [Fact]
        public async Task InvokeAsync_WhenRequestAllowed_SetsRemainingHeaderAndExecutesNextDelegate() {
            // Arrange
            bool nextInvoked = false;
            MockRateLimiter limiter = new(RateLimitDecision.Allowed(remaining: 9));

            RateLimiterAspNetCoreOptions options = new() { EnableIetfHeaders = true };
            RateLimitingMiddleware middleware = new(
                next: _ => {
                    nextInvoked = true;
                    return Task.CompletedTask;
                },
                rateLimiter: limiter,
                optionsMonitor: new TestOptionsMonitor<RateLimiterAspNetCoreOptions>(options));

            DefaultHttpContext context = new();

            // Act
            await middleware.InvokeAsync(context);

            // Assert
            Assert.True(nextInvoked);
            Assert.Equal("9", context.Response.Headers[RateLimitConstants.Headers.RateLimitRemaining]);
            Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        }
    }

    public sealed class TheRejectionAndRfcHeaders {

        [Fact]
        public async Task InvokeAsync_WhenRequestDenied_Emits429WithRfcHeadersAndProblemDetailsJson() {
            // Arrange
            MockRateLimiter limiter = new(RateLimitDecision.Denied(TimeSpan.FromSeconds(3.2), remaining: 0));

            RateLimiterAspNetCoreOptions options = new() {
                UseProblemDetails = true,
                EnableIetfHeaders = true
            };

            RateLimitingMiddleware middleware = new(
                next: static _ => Task.CompletedTask,
                rateLimiter: limiter,
                optionsMonitor: new TestOptionsMonitor<RateLimiterAspNetCoreOptions>(options));

            DefaultHttpContext context = new();
            context.Response.Body = new MemoryStream();

            // Act
            await middleware.InvokeAsync(context);

            // Assert: Status & RFC Headers
            Assert.Equal(StatusCodes.Status429TooManyRequests, context.Response.StatusCode);
            Assert.Equal("4", context.Response.Headers[RateLimitConstants.Headers.RetryAfter]); // Ceil(3.2s) = 4s
            Assert.Equal("4", context.Response.Headers[RateLimitConstants.Headers.RateLimitReset]);
            Assert.Equal("0", context.Response.Headers[RateLimitConstants.Headers.RateLimitRemaining]);
            Assert.Equal(RateLimitConstants.ContentTypes.ProblemJson, context.Response.ContentType);

            // Assert: RFC 7807/9457 ProblemDetails Payload
            context.Response.Body.Seek(0, SeekOrigin.Begin);
            ProblemDetails? problem = await JsonSerializer.DeserializeAsync<ProblemDetails>(
                context.Response.Body,
                cancellationToken: TestContext.Current.CancellationToken);

            Assert.NotNull(problem);
            Assert.Equal(429, problem.Status);
            Assert.Equal(RateLimitConstants.Uris.Rfc6585, problem.Type);
            Assert.True(problem.Extensions.TryGetValue("retryAfter", out object? rawRetryAfter));

            JsonElement retryAfterElement = Assert.IsType<JsonElement>(rawRetryAfter);
            Assert.Equal(4, retryAfterElement.GetInt32());
        }

        [Fact]
        public async Task InvokeAsync_WithCustomStatusCode_EmitsConfiguredStatusCode() {
            // Arrange
            MockRateLimiter limiter = new(RateLimitDecision.Denied(TimeSpan.FromSeconds(10)));
            RateLimiterAspNetCoreOptions options = new() {
                StatusCode = StatusCodes.Status503ServiceUnavailable,
                UseProblemDetails = false
            };

            RateLimitingMiddleware middleware = new(
                next: static _ => Task.CompletedTask,
                rateLimiter: limiter,
                optionsMonitor: new TestOptionsMonitor<RateLimiterAspNetCoreOptions>(options));

            DefaultHttpContext context = new();
            context.Response.Body = new MemoryStream();

            // Act
            await middleware.InvokeAsync(context);

            // Assert
            Assert.Equal(StatusCodes.Status503ServiceUnavailable, context.Response.StatusCode);
        }

        [Fact]
        public async Task InvokeAsync_WithProblemDetailsCustomizer_EnrichesJsonPayload() {
            // Arrange
            MockRateLimiter limiter = new(RateLimitDecision.Denied(TimeSpan.FromSeconds(10)));
            RateLimiterAspNetCoreOptions options = new() {
                UseProblemDetails = true,
                ProblemDetailsCustomizer = (problem, _, _) => {
                    problem.Extensions["securityViolation"] = true;
                    problem.Extensions["traceId"] = "trace_abc_999";
                }
            };

            RateLimitingMiddleware middleware = new(
                next: static _ => Task.CompletedTask,
                rateLimiter: limiter,
                optionsMonitor: new TestOptionsMonitor<RateLimiterAspNetCoreOptions>(options));

            DefaultHttpContext context = new();
            context.Response.Body = new MemoryStream();

            // Act
            await middleware.InvokeAsync(context);

            // Assert
            context.Response.Body.Seek(0, SeekOrigin.Begin);
            ProblemDetails? problem = await JsonSerializer.DeserializeAsync<ProblemDetails>(
                context.Response.Body,
                cancellationToken: TestContext.Current.CancellationToken);

            Assert.NotNull(problem);
            Assert.True(problem.Extensions.TryGetValue("securityViolation", out object? violation));
            Assert.True(Assert.IsType<JsonElement>(violation).GetBoolean());

            Assert.True(problem.Extensions.TryGetValue("traceId", out object? traceId));
            Assert.Equal("trace_abc_999", Assert.IsType<JsonElement>(traceId).GetString());
        }

        [Fact]
        public async Task InvokeAsync_WithCustomOnRejectedAsync_ExecutesCallbackAndBypassesProblemDetails() {
            // Arrange
            bool customCallbackExecuted = false;
            MockRateLimiter limiter = new(RateLimitDecision.Denied(TimeSpan.FromSeconds(10)));
            RateLimiterAspNetCoreOptions options = new() {
                OnRejectedAsync = (ctx, _) => {
                    customCallbackExecuted = true;
                    ctx.Response.StatusCode = StatusCodes.Status418ImATeapot;
                    return ctx.Response.WriteAsync("Custom Teapot Body");
                }
            };

            RateLimitingMiddleware middleware = new(
                next: static _ => Task.CompletedTask,
                rateLimiter: limiter,
                optionsMonitor: new TestOptionsMonitor<RateLimiterAspNetCoreOptions>(options));

            DefaultHttpContext context = new();
            context.Response.Body = new MemoryStream();

            // Act
            await middleware.InvokeAsync(context);

            // Assert
            Assert.True(customCallbackExecuted);
            Assert.Equal(StatusCodes.Status418ImATeapot, context.Response.StatusCode);

            context.Response.Body.Seek(0, SeekOrigin.Begin);
            using StreamReader reader = new(context.Response.Body);
            string body = await reader.ReadToEndAsync(TestContext.Current.CancellationToken);
            Assert.Equal("Custom Teapot Body", body);
        }
    }

    public sealed class TheEndpointMetadataRouting {

        [Fact]
        public async Task InvokeAsync_WhenEndpointHasDisableRateLimiting_BypassesLimiterEntirely() {
            // Arrange
            MockRateLimiter limiter = new(RateLimitDecision.Denied(TimeSpan.FromMinutes(1)));
            RateLimiterAspNetCoreOptions options = new();

            RateLimitingMiddleware middleware = new(
                next: static _ => Task.CompletedTask,
                rateLimiter: limiter,
                optionsMonitor: new TestOptionsMonitor<RateLimiterAspNetCoreOptions>(options));

            DefaultHttpContext context = new();
            EndpointMetadataCollection metadata = new(new DisableRateLimitingAttribute());
            context.Features.Set<IEndpointFeature>(new EndpointFeature {
                Endpoint = new Endpoint(static _ => Task.CompletedTask, metadata, "DisabledEndpoint")
            });

            // Act
            await middleware.InvokeAsync(context);

            // Assert: Rate limiter was bypassed, next executed
            Assert.Equal(0, limiter.CallCount);
            Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        }

        [Fact]
        public async Task InvokeAsync_WhenEndpointHasStaticCost_PassesCostToLimiter() {
            // Arrange
            MockRateLimiter limiter = new(RateLimitDecision.Allowed(10));
            RateLimiterAspNetCoreOptions options = new();

            RateLimitingMiddleware middleware = new(
                next: static _ => Task.CompletedTask,
                rateLimiter: limiter,
                optionsMonitor: new TestOptionsMonitor<RateLimiterAspNetCoreOptions>(options));

            DefaultHttpContext context = new();
            EndpointMetadataCollection metadata = new(new RateLimitCostAttribute(5));
            context.Features.Set<IEndpointFeature>(new EndpointFeature {
                Endpoint = new Endpoint(static _ => Task.CompletedTask, metadata, "Cost5Endpoint")
            });

            // Act
            await middleware.InvokeAsync(context);

            // Assert
            Assert.Equal(5, limiter.LastCostAcquired);
        }

        [Fact]
        public async Task InvokeAsync_WhenEndpointHasDynamicCostResolver_ComputesCostFromQueryBatch() {
            // Arrange
            MockRateLimiter limiter = new(RateLimitDecision.Allowed(10));
            RateLimiterAspNetCoreOptions options = new();

            RateLimitingMiddleware middleware = new(
                next: static _ => Task.CompletedTask,
                rateLimiter: limiter,
                optionsMonitor: new TestOptionsMonitor<RateLimiterAspNetCoreOptions>(options));

            DefaultHttpContext context = new();
            context.Request.QueryString = new QueryString("?batchSize=17");

            EndpointMetadataCollection metadata = new(new RateLimitMetadata {
                DynamicCostResolver = ctx => ctx.Request.Query.TryGetValue("batchSize", out StringValues val) && int.TryParse(val, out int count) ? count : 1
            });
            context.Features.Set<IEndpointFeature>(new EndpointFeature {
                Endpoint = new Endpoint(static _ => Task.CompletedTask, metadata, "DynamicBatchEndpoint")
            });

            // Act
            await middleware.InvokeAsync(context);

            // Assert: Exactly 17 units deducted
            Assert.Equal(17, limiter.LastCostAcquired);
        }

        [Fact]
        public async Task InvokeAsync_WhenEndpointHasNamedPolicy_InvokesMatchingPolicy() {
            // Arrange
            MockRateLimiter limiter = new(RateLimitDecision.Allowed(10));
            RateLimiterAspNetCoreOptions options = new();

            RateLimitingMiddleware middleware = new(
                next: static _ => Task.CompletedTask,
                rateLimiter: limiter,
                optionsMonitor: new TestOptionsMonitor<RateLimiterAspNetCoreOptions>(options));

            DefaultHttpContext context = new();
            EndpointMetadataCollection metadata = new(new RateLimitMetadata { PolicyName = "strict_auth" });
            context.Features.Set<IEndpointFeature>(new EndpointFeature {
                Endpoint = new Endpoint(static _ => Task.CompletedTask, metadata, "StrictAuthEndpoint")
            });

            // Act
            await middleware.InvokeAsync(context);

            // Assert: Targeted named policy was evaluated
            Assert.Equal("strict_auth", limiter.LastPolicyAcquired);
        }
    }

    private sealed class MockRateLimiter(RateLimitDecision outcome) : IRateLimiter {
        public int CallCount { get; private set; }
        public int LastCostAcquired { get; private set; }
        public string? LastKeyAcquired { get; private set; }
        public string? LastPolicyAcquired { get; private set; }

        public ValueTask<RateLimitDecision> TryAcquireAsync(string key, int cost = 1, CancellationToken cancellationToken = default) {
            this.CallCount++;
            this.LastKeyAcquired = key;
            this.LastCostAcquired = cost;
            this.LastPolicyAcquired = null;
            return ValueTask.FromResult(outcome);
        }

        public ValueTask<RateLimitDecision> TryAcquireAsync(string policyName, string key, int cost = 1, CancellationToken cancellationToken = default) {
            this.CallCount++;
            this.LastKeyAcquired = key;
            this.LastCostAcquired = cost;
            this.LastPolicyAcquired = policyName;
            return ValueTask.FromResult(outcome);
        }

        public IRateLimitAlgorithm GetPolicy(string policyName) {
            throw new NotImplementedException();
        }
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

    private sealed class EndpointFeature : IEndpointFeature {
        public Endpoint? Endpoint { get; set; }
    }
}