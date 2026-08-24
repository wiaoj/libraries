using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Text.Json;
using Wiaoj.Preconditions;

namespace Wiaoj.RateLimiting.AspNetCore.Middleware;

/// <summary>
/// ASP.NET Core middleware that enforces rate limiting using endpoint metadata, dynamic cost resolution, and RFC standards.
/// </summary>
internal sealed class RateLimitingMiddleware {
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly RequestDelegate _next;
    private readonly IRateLimitAlgorithm _algorithm;
    private readonly IOptionsMonitor<RateLimitingOptions> _optionsMonitor;

    /// <summary>
    /// Initializes a new instance of the <see cref="RateLimitingMiddleware"/> class.
    /// </summary>
    public RateLimitingMiddleware(
        RequestDelegate next,
        IRateLimitAlgorithm algorithm,
        IOptionsMonitor<RateLimitingOptions> optionsMonitor) {
        Preca.ThrowIfNull(next);
        Preca.ThrowIfNull(algorithm);
        Preca.ThrowIfNull(optionsMonitor);

        this._next = next;
        this._algorithm = algorithm;
        this._optionsMonitor = optionsMonitor;
    }

    /// <summary>
    /// Evaluates rate limits for the incoming request, considering endpoint metadata and dynamic costs.
    /// </summary>
    public async Task InvokeAsync(HttpContext context) {
        Preca.ThrowIfNull(context);

        RateLimitingOptions options = this._optionsMonitor.CurrentValue;

        // 1. Endpoint Metadata Kontrolü: Devre dışı bırakılmış mı?
        Endpoint? endpoint = context.GetEndpoint();
        RateLimitMetadata? metadata = endpoint?.Metadata.GetMetadata<RateLimitMetadata>();
        DisableRateLimitingAttribute? disabledAttr = endpoint?.Metadata.GetMetadata<DisableRateLimitingAttribute>();

        if(metadata?.IsDisabled == true || disabledAttr is not null) {
            await this._next(context).ConfigureAwait(false);
            return;
        }

        // 2. Dinamik Maliyet (Cost) Çözümleme:
        // Öncelik: Metadata Dynamic Resolver > Metadata Static Cost > Attribute Cost > Global Default Resolver
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

        RateLimitDecision decision = await this._algorithm
            .TryAcquireAsync(key, cost, context.RequestAborted)
            .ConfigureAwait(false);

        // 3. İzin Verildi (200 OK Path)
        if(decision.IsAllowed) {
            if(options.EnableIetfHeaders && decision.Remaining.HasValue) {
                context.Response.Headers[RateLimitConstants.Headers.RateLimitRemaining] = decision.Remaining.Value.ToString(CultureInfo.InvariantCulture);
            }

            await this._next(context).ConfigureAwait(false);
            return;
        }

        // 4. Reddedildi (429 Path & RFC Header Yazımı)
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
        RateLimitingOptions options,
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