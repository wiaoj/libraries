//using Microsoft.AspNetCore.Http;
//using Microsoft.AspNetCore.Mvc;
//using Microsoft.Extensions.Options;
//using System.Text.Json;
//using Wiaoj.RateLimiting.AspNetCore;
//using Wiaoj.RateLimiting.AspNetCore.Middleware;

//namespace Wiaoj.RateLimiting.Tests.Unit.AspNetCore;

//public sealed class RateLimitingMiddlewareAdvancedTests {
//    private sealed class DenyingAlgorithm : IRateLimitAlgorithm {
//        public ValueTask<RateLimitDecision> TryAcquireAsync(string key, int cost = 1, CancellationToken cancellationToken = default) {
//            return ValueTask.FromResult(RateLimitDecision.Denied(TimeSpan.FromSeconds(10), remaining: 0));
//        }
//    }

//    private sealed class TestOptionsMonitor<T>(T currentValue) : IOptionsMonitor<T> where T : class {
//        public T CurrentValue => currentValue;
//        public T Get(string? name) {
//            return currentValue;
//        }

//        public IDisposable? OnChange(Action<T, string?> listener) {
//            return null;
//        }
//    }

//    [Fact]
//    public async Task InvokeAsync_WithCustomStatusCode_EmitsConfiguredStatusCode() {
//        RateLimiterAspNetCoreOptions options = new() {
//            StatusCode = StatusCodes.Status503ServiceUnavailable,
//            UseProblemDetails = false
//        };

//        RateLimitingMiddleware middleware = new(
//            _ => Task.CompletedTask,
//            new DenyingAlgorithm(),
//            new TestOptionsMonitor<RateLimiterAspNetCoreOptions>(options));

//        DefaultHttpContext context = new();
//        context.Response.Body = new MemoryStream();

//        await middleware.InvokeAsync(context);

//        Assert.Equal(StatusCodes.Status503ServiceUnavailable, context.Response.StatusCode);
//    }

//    [Fact]
//    public async Task InvokeAsync_WithProblemDetailsCustomizer_EnrichesJsonPayload() {
//        RateLimiterAspNetCoreOptions options = new() {
//            UseProblemDetails = true,
//            ProblemDetailsCustomizer = (problem, ctx, decision) => {
//                problem.Extensions["customField"] = "SecurityBlocked";
//                problem.Extensions["trackingId"] = "trace_abc_123";
//            }
//        };

//        RateLimitingMiddleware middleware = new(
//            _ => Task.CompletedTask,
//            new DenyingAlgorithm(),
//            new TestOptionsMonitor<RateLimitingOptions>(options));

//        DefaultHttpContext context = new();
//        context.Response.Body = new MemoryStream();

//        await middleware.InvokeAsync(context);

//        context.Response.Body.Seek(0, SeekOrigin.Begin);
//        ProblemDetails? problem = await JsonSerializer.DeserializeAsync<ProblemDetails>(
//            context.Response.Body,
//            cancellationToken: TestContext.Current.CancellationToken);

//        Assert.NotNull(problem);
//        Assert.True(problem.Extensions.TryGetValue("customField", out object? customField));
//        Assert.Equal("SecurityBlocked", customField?.ToString());
//        Assert.True(problem.Extensions.TryGetValue("trackingId", out object? trackingId));
//        Assert.Equal("trace_abc_123", trackingId?.ToString());
//    }

//    [Fact]
//    public async Task InvokeAsync_WithCustomOnRejectedAsync_ExecutesCallbackAndBypassesProblemDetails() {
//        bool customCallbackExecuted = false;
//        RateLimitingOptions options = new() {
//            OnRejectedAsync = (ctx, decision) => {
//                customCallbackExecuted = true;
//                ctx.Response.StatusCode = StatusCodes.Status418ImATeapot;
//                return ctx.Response.WriteAsync("Custom Rejection Message");
//            }
//        };

//        RateLimitingMiddleware middleware = new(
//            _ => Task.CompletedTask,
//            new DenyingAlgorithm(),
//            new TestOptionsMonitor<RateLimitingOptions>(options));

//        DefaultHttpContext context = new();
//        context.Response.Body = new MemoryStream();

//        await middleware.InvokeAsync(context);

//        Assert.True(customCallbackExecuted);
//        Assert.Equal(StatusCodes.Status418ImATeapot, context.Response.StatusCode);

//        context.Response.Body.Seek(0, SeekOrigin.Begin);
//        using StreamReader reader = new(context.Response.Body);
//        string body = await reader.ReadToEndAsync(TestContext.Current.CancellationToken);
//        Assert.Equal("Custom Rejection Message", body);
//    }
//}