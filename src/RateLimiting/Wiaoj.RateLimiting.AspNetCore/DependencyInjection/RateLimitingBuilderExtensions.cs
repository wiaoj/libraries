using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Wiaoj.Preconditions;
using Wiaoj.RateLimiting.AspNetCore;

namespace Wiaoj.RateLimiting;

/// <summary>
/// ASP.NET Core extension methods for <see cref="IRateLimitingBuilder"/>.
/// </summary>
public static class RateLimitingBuilderExtensions {
    /// <summary>
    /// Configures ASP.NET Core rate limiting options directly within the rate limiting builder.
    /// </summary>
    /// <param name="builder">The rate limiting builder instance.</param>
    /// <param name="configure">The delegate to configure <see cref="RateLimiterAspNetCoreOptions"/>.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    public static IRateLimitingBuilder WithAspNetCore(
        this IRateLimitingBuilder builder,
        Action<RateLimiterAspNetCoreOptions> configure) {
        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNull(configure);

        builder.Services.Configure(configure);
        return builder;
    }

    /// <summary>
    /// Configures ASP.NET Core rate limiting options bound from an <see cref="IConfigurationSection"/> (e.g. appsettings.json).
    /// </summary>
    /// <param name="builder">The rate limiting builder instance.</param>
    /// <param name="configurationSection">The configuration section to bind options from.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    public static IRateLimitingBuilder WithAspNetCore(
        this IRateLimitingBuilder builder,
        IConfigurationSection configurationSection) {
        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNull(configurationSection);

        builder.Services.Configure<RateLimiterAspNetCoreOptions>(configurationSection);
        return builder;
    }
}