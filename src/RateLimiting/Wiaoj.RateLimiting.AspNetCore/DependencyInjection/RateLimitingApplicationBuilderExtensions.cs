using Wiaoj.Preconditions;
using Wiaoj.RateLimiting.AspNetCore;
using Wiaoj.RateLimiting.AspNetCore.Middleware;

#pragma warning disable IDE0130 // Namespace matches standard ASP.NET Core pipeline convention
namespace Microsoft.AspNetCore.Builder;
#pragma warning restore IDE0130

/// <summary>
/// Extension methods for enabling Wiaoj rate limiting middleware.
/// </summary>
public static class RateLimitingApplicationBuilderExtensions {
    /// <summary>
    /// Adds the Wiaoj rate limiting middleware to the ASP.NET Core pipeline using options resolved from DI.
    /// </summary>
    public static IApplicationBuilder UseWiaojRateLimiting(this IApplicationBuilder app) {
        Preca.ThrowIfNull(app);
        return app.UseMiddleware<RateLimitingMiddleware>();
    }

    /// <summary>
    /// Adds the Wiaoj rate limiting middleware to the ASP.NET Core pipeline with custom inline options.
    /// </summary>
    public static IApplicationBuilder UseWiaojRateLimiting(
        this IApplicationBuilder app,
        Action<RateLimiterAspNetCoreOptions> configureOptions) {
        Preca.ThrowIfNull(app);
        Preca.ThrowIfNull(configureOptions);

        RateLimiterAspNetCoreOptions options = new();
        configureOptions(options);

        return app.UseMiddleware<RateLimitingMiddleware>(Microsoft.Extensions.Options.Options.Create(options));
    }
}