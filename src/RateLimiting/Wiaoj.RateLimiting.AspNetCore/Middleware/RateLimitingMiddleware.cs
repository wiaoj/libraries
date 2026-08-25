using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Text.Json;
using Wiaoj.Preconditions;

namespace Wiaoj.RateLimiting.AspNetCore.Middleware;

/// <summary>
/// ASP.NET Core middleware that enforces rate limiting using endpoint metadata, named policies, and RFC standards.
/// </summary>
internal sealed class RateLimitingMiddleware {
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly RequestDelegate _next;
    private readonly IRateLimiter _rateLimiter;
    private readonly IOptionsMonitor<RateLimiterAspNetCoreOptions> _optionsMonitor;

    /// <summary>
    /// Initializes a new instance of the <see cref="RateLimitingMiddleware"/> class.
    /// </summary>
    public RateLimitingMiddleware(
        RequestDelegate next,
        IRateLimiter rateLimiter,
        IOptionsMonitor<RateLimiterAspNetCoreOptions> optionsMonitor) {
        Preca.ThrowIfNull(next);
        Preca.ThrowIfNull(rateLimiter);
        Preca.ThrowIfNull(optionsMonitor);

        this._next = next;
        this._rateLimiter = rateLimiter;
        this._optionsMonitor = optionsMonitor;
    }

    public async Task InvokeAsync(HttpContext context) {
        Preca.ThrowIfNull(context);

        RateLimiterAspNetCoreOptions options = this._optionsMonitor.CurrentValue;

        // 1. Endpoint Metadata Kontrolü
        Endpoint? endpoint = context.GetEndpoint();
        RateLimitMetadata? metadata = endpoint?.Metadata.GetMetadata<RateLimitMetadata>();
        DisableRateLimitingAttribute? disabledAttr = endpoint?.Metadata.GetMetadata<DisableRateLimitingAttribute>();

        if(metadata?.IsDisabled == true || disabledAttr is not null) {
            await this._next(context).ConfigureAwait(false);
            return;
        }

        // 2. Maliyet (Cost) Çözümleme
        int cost;
        if(metadata?.DynamicCostResolver is not null) {
            cost = Math.Max(1, metadata.DynamicCostResolver(context));
        }
        else if(metadata?.Cost is not null) {
            cost = metadata.Cost.Value;
        }
        else if(endpoint?.Metadata.GetMetadata<RateLimitCostAttribute>() is { } costAttr) {
            cost = costAttr.Cost;
        }
        else {
            cost = Math.Max(1, options.DefaultCostResolver(context));
        }

        string key = options.KeySelector.GetKey(context);

        // 3. Policy Seçimi ve Limit Kontrolü (Named vs Default)
        RateLimitDecision decision = metadata?.PolicyName is not null
            ? await this._rateLimiter.TryAcquireAsync(metadata.PolicyName, key, cost, context.RequestAborted).ConfigureAwait(false)
            : await this._rateLimiter.TryAcquireAsync(key, cost, context.RequestAborted).ConfigureAwait(false);

        // 4. İzin Verildi (200 OK Path)
        if(decision.IsAllowed) {
            if(options.EnableIetfHeaders && decision.Remaining.HasValue) {
                context.Response.Headers[RateLimitConstants.Headers.RateLimitRemaining] = decision.Remaining.Value.ToString(CultureInfo.InvariantCulture);
            }

            await this._next(context).ConfigureAwait(false);
            return;
        }

        // 5. Reddedildi (429 Too Many Requests)
        context.Response.StatusCode = options.StatusCode;

        int? retryAfterSeconds = null;
        if(decision.RetryAfter is { } retryAfter) {
            retryAfterSeconds = (int)Math.Ceiling(retryAfter.TotalSeconds);
            if(retryAfterSeconds < 0) retryAfterSeconds = 0;

            string secStr = retryAfterSeconds.Value.ToString(CultureInfo.InvariantCulture);
            context.Response.Headers[RateLimitConstants.Headers.RetryAfter] = secStr;

            if(options.EnableIetfHeaders) {
                context.Response.Headers[RateLimitConstants.Headers.RateLimitReset] = secStr;
            }
        }

        if(options.EnableIetfHeaders && decision.Remaining.HasValue) {
            context.Response.Headers[RateLimitConstants.Headers.RateLimitRemaining] = decision.Remaining.Value.ToString(CultureInfo.InvariantCulture);
        }

        if(options.OnRejectedAsync is not null) {
            await options.OnRejectedAsync(context, decision).ConfigureAwait(false);
            return;
        }

        if(options.UseProblemDetails) {
            await WriteProblemDetailsResponseAsync(context, options, decision, retryAfterSeconds).ConfigureAwait(false);
            return;
        }

        context.Response.ContentType = RateLimitConstants.ContentTypes.PlainText;
        await context.Response.WriteAsync("Too Many Requests. Please try again later.", context.RequestAborted).ConfigureAwait(false);
    }

    private static async Task WriteProblemDetailsResponseAsync(
        HttpContext context,
        RateLimiterAspNetCoreOptions options,
        RateLimitDecision decision,
        int? retryAfterSeconds) {

        ProblemDetails problemDetails = new() {
            Type = RateLimitConstants.Uris.Rfc6585,
            Title = "Too Many Requests",
            Status = options.StatusCode,
            Detail = retryAfterSeconds.HasValue
                ? $"Rate limit exceeded. Quota will be available in {retryAfterSeconds.Value} seconds."
                : "Rate limit exceeded. Please retry later.",
            Instance = context.Request.Path
        };

        if(retryAfterSeconds.HasValue) {
            problemDetails.Extensions["retryAfter"] = retryAfterSeconds.Value;
        }
        if(decision.Remaining.HasValue) {
            problemDetails.Extensions["remaining"] = decision.Remaining.Value;
        }

        options.ProblemDetailsCustomizer?.Invoke(problemDetails, context, decision);

        if(context.RequestServices?.GetService<IProblemDetailsService>() is { } problemDetailsService) {
            ProblemDetailsContext problemContext = new() {
                HttpContext = context,
                ProblemDetails = problemDetails
            };

            if(await problemDetailsService.TryWriteAsync(problemContext).ConfigureAwait(false)) {
                return;
            }
        }

        context.Response.ContentType = RateLimitConstants.ContentTypes.ProblemJson;
        await JsonSerializer.SerializeAsync(context.Response.Body, problemDetails, JsonOptions, context.RequestAborted).ConfigureAwait(false);
    }
}