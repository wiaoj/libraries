# Wiaoj.RateLimiting.AspNetCore

ASP.NET Core integration package for `Wiaoj.RateLimiting`, providing HTTP middleware, partition key extractors, endpoint metadata conventions, and RFC-compliant response handling.

---

## Installation

```bash
dotnet add package Wiaoj.RateLimiting.AspNetCore
```

---

## What This Package Contains

- **`RateLimitingMiddleware`:** ASP.NET Core middleware that evaluates incoming requests against configured named or default rate limiting policies, applies endpoint costs, and emits RFC-standard responses.
- **Key Selectors (`IRateLimitKeySelector`):**
  - `ClientIpKeySelector`: Formats remote IPv4/IPv6 addresses directly using stack-allocated buffers.
  - `ApiKeyHeaderKeySelector`: Extracts identity keys from configurable request headers (e.g. `X-Api-Key`), with configurable fallback selectors.
  - `UserClaimKeySelector`: Extracts identity keys from authenticated claims (`ClaimsPrincipal`), with configurable fallback selectors.
- **Endpoint Metadata and Route Conventions:**
  - `[DisableRateLimiting]` / `.DisableRateLimiting()`: Bypasses rate limiting for targeted endpoints.
  - `[RateLimitCost(int)]` / `.WithRateLimitCost(int)`: Statically overrides the quota cost for an endpoint.
  - `.WithRateLimitCost(Func<HttpContext, int>)`: Dynamically computes quota cost at runtime (e.g. from batch payload counts or query parameters).
- **RFC and Standards Compliance:**
  - RFC 6585: Emits HTTP `429 Too Many Requests`.
  - RFC 9110: Emits integer delta-seconds `Retry-After` header.
  - RFC 7807 / RFC 9457: Serializes `application/problem+json` `ProblemDetails` responses.
  - IETF Draft Headers: Writes `RateLimit-Remaining` and `RateLimit-Reset` headers.

---

## Setup and Configuration

### 1. Register Services in `Program.cs`

```csharp
using Wiaoj.DistributedCounter;
using Wiaoj.RateLimiting;
using Wiaoj.RateLimiting.AspNetCore;
using Wiaoj.RateLimiting.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

// Configure counter backend
builder.Services.AddDistributedCounter(dc => dc.UseInMemory());

// Configure rate limiting policies and ASP.NET Core options
builder.Services.AddWiaojRateLimiting(limiter => {
    limiter.AddPolicy("auth", policy => policy.UseFixedWindow(limit: 5, window: TimeSpan.FromMinutes(1)));
    limiter.UseDefaultPolicy(policy => policy.UseSlidingWindow(limit: 100, window: TimeSpan.FromMinutes(1)));

    limiter.WithAspNetCore(options => {
        options.KeySelector = new ClientIpKeySelector(prefix: "ip:");
        options.StatusCode = StatusCodes.Status429TooManyRequests;
        options.EnableIetfHeaders = true;
        options.UseProblemDetails = true;
    });
});

var app = builder.Build();

// Enable the middleware in the request pipeline
app.UseWiaojRateLimiting();
```

---

## Endpoint Routing Conventions

### Static Costing, Dynamic Costing, and Exemptions

```csharp
// 1. Endpoint with static custom cost (consumes 5 quota units per call)
app.MapPost("/api/export", () => Results.Ok())
   .WithRateLimitCost(5);

// 2. Endpoint with dynamic cost calculated from query parameters
app.MapPost("/api/batch", (int batchSize) => Results.Ok())
   .WithRateLimitCost(ctx => {
       if (ctx.Request.Query.TryGetValue("batchSize", out var val) && int.TryParse(val, out int count)) {
           return Math.Max(1, count);
       }
       return 1;
   });

// 3. Endpoint exempt from all rate limiting
app.MapGet("/healthz", () => Results.Ok("OK"))
   .DisableRateLimiting();
```

### Two limits these APIs cannot cross

Both are observable in the shipped code, and both bite as soon as one endpoint serves verbs
that do not cost the same.

**1. A resolved cost can never be zero.** The middleware floors it —
`cost = Math.Max(1, metadata.DynamicCostResolver(context))` and the same clamp on
`DefaultCostResolver` (`Middleware/RateLimitingMiddleware.cs:54` and `:63`). A resolver that returns
`0` to mean "this request is free" is silently recorded as `1`. The static path refuses the value even
earlier: `WithRateLimitCost(int cost)` throws `ArgumentOutOfRangeException` for `cost <= 0`
(`Metadata/RateLimitEndpointConventionBuilderExtensions.cs:18`). So "free" is unreachable through either
overload — note that example 2 above already defends itself with `Math.Max(1, count)` rather than
returning a raw count; that is the floor, not a style choice.

**2. Exemption is endpoint-wide, not per-verb.** `[DisableRateLimiting]` /
`RateLimitMetadata.IsDisabled` short-circuit the whole endpoint
(`Middleware/RateLimitingMiddleware.cs:45`). Minimal APIs let one endpoint answer several methods
(`app.MapMethods("/items", [HttpMethods.Get, HttpMethods.Query], handler)`), and those methods rarely cost
the same: a `GET` on a cacheable read model may be answered from cache semantics and return `304` without
producing a body, while `QUERY` (RFC 10008) parses a payload, validates it against a schema and runs
projections. On such a route the only available switches are "count both identically" or "limit neither" —
neither expresses "count only the non-`GET` traffic".

**What to do today** if you need verb-level accounting: consume `Wiaoj.RateLimiting`'s `IRateLimiter`
directly from an `IEndpointFilter` that returns early for the methods you want free and calls
`TryAcquireAsync(policy, key, cost, cancellationToken)` for the rest. Policies registered through
`AddWiaojRateLimiting` and the returned `RateLimitDecision` stay the package's, so only the trigger point
lives in your service — and it can move back into endpoint metadata as soon as the verb-level gap closes.

Tracked as [wiaoj/libraries#252](https://github.com/wiaoj/libraries/issues/252), which proposes either a
`Func<HttpContext, bool> ShouldEvaluate` on `RateLimiterAspNetCoreOptions`, or dropping the floors and
documenting `0` as "consumes no quota".

---

## Key Selectors

Key selectors define how client identity is extracted from `HttpContext`:

```csharp
limiter.WithAspNetCore(options => {
    // 1. Client IP address (IPv4 / IPv6)
    options.KeySelector = new ClientIpKeySelector(prefix: "ip:");

    // 2. API Key header with fallback to IP if missing
    options.KeySelector = new ApiKeyHeaderKeySelector(
        headerName: "X-Api-Key",
        prefix: "api_key:",
        fallbackSelector: new ClientIpKeySelector("anon_ip:"));

    // 3. User claim (Subject ID) with fallback to IP if unauthenticated
    options.KeySelector = new UserClaimKeySelector(
        claimType: ClaimTypes.NameIdentifier,
        prefix: "user:",
        fallbackSelector: new ClientIpKeySelector("anon_ip:"));
});
```

---

## Response Formatting & ProblemDetails

When a request is rejected, the middleware formats the response according to configured options:

### Standard RFC 7807 Response Body (`application/problem+json`)

```json
{
  "type": "https://tools.ietf.org/html/rfc6585#section-4",
  "title": "Too Many Requests",
  "status": 429,
  "detail": "Rate limit exceeded. Quota will be available in 12 seconds.",
  "instance": "/api/orders",
  "retryAfter": 12,
  "remaining": 0
}
```

### Customizing ProblemDetails

```csharp
limiter.WithAspNetCore(options => {
    options.UseProblemDetails = true;
    options.ProblemDetailsCustomizer = (problem, context, decision) => {
        problem.Extensions["traceId"] = context.TraceIdentifier;
        problem.Extensions["policy"] = "auth";
    };
});
```

### Custom Low-Level Rejection Callback

```csharp
limiter.WithAspNetCore(options => {
    options.OnRejectedAsync = (context, decision) => {
        context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        return context.Response.WriteAsync("Custom plain-text rate limit rejection.");
    };
});
```

---

## License

This project is licensed under the MIT License.