using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Wiaoj.DistributedCounter;
using Wiaoj.DistributedCounter.DependencyInjection;
using Wiaoj.Preconditions;
using Wiaoj.Resilience;
using Wiaoj.Webhooks.Resilience;

#pragma warning disable IDE0130
namespace Wiaoj.Webhooks;
#pragma warning restore IDE0130

/// <summary>
/// Extension methods for configuring circuit breaker resilience in the webhook delivery pipeline.
/// </summary>
public static class WebhookBuilderResilienceExtensions {
    /// <summary>
    /// Enables consecutive failures circuit breaker protection with default options (5 failures, 1 minute break).
    /// </summary>
    /// <param name="builder">The webhook builder.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    public static IWebhookBuilder UseCircuitBreaker(this IWebhookBuilder builder) {
        return UseCircuitBreaker(builder, new CircuitBreakerOptions());
    }

    /// <summary>
    /// Enables consecutive failures circuit breaker protection with a configuration delegate.
    /// </summary>
    /// <param name="builder">The webhook builder.</param>
    /// <param name="configure">The configuration delegate.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    public static IWebhookBuilder UseCircuitBreaker(this IWebhookBuilder builder, Action<CircuitBreakerOptions> configure) {
        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNull(configure);

        CircuitBreakerOptions options = new();
        configure(options);
        return UseCircuitBreaker(builder, options);
    }

    /// <summary>
    /// Enables consecutive failures circuit breaker protection with explicit options.
    /// </summary>
    /// <param name="builder">The webhook builder.</param>
    /// <param name="options">The circuit breaker options.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    public static IWebhookBuilder UseCircuitBreaker(this IWebhookBuilder builder, CircuitBreakerOptions options) {
        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNull(options);
        options.Validate();

        builder.Services.AddDistributedCounter(dc => dc.UseInMemory());

        builder.Services.TryAddSingleton<ICircuitBreaker>(sp => new ConsecutiveFailuresCircuitBreaker(
            sp.GetRequiredService<IDistributedCounterFactory>(),
            options,
            sp.GetService<TimeProvider>() ?? TimeProvider.System,
            sp.GetService<ILogger<ConsecutiveFailuresCircuitBreaker>>() ?? NullLogger<ConsecutiveFailuresCircuitBreaker>.Instance));

        builder.AddMiddleware<CircuitBreakerMiddleware>();
        return builder;
    }

    /// <summary>
    /// Enables percentage-based sampling window circuit breaker protection with explicit options.
    /// </summary>
    /// <param name="builder">The webhook builder.</param>
    /// <param name="options">The sampling window options.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    public static IWebhookBuilder UseSamplingCircuitBreaker(this IWebhookBuilder builder, SamplingWindowCircuitBreakerOptions options) {
        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNull(options);
        options.Validate();

        builder.Services.AddDistributedCounter(dc => dc.UseInMemory());

        builder.Services.TryAddSingleton<ICircuitBreaker>(sp => new SamplingWindowCircuitBreaker(
            sp.GetRequiredService<IDistributedCounterFactory>(),
            options,
            sp.GetService<TimeProvider>() ?? TimeProvider.System,
            sp.GetService<ILogger<SamplingWindowCircuitBreaker>>() ?? NullLogger<SamplingWindowCircuitBreaker>.Instance));

        builder.AddMiddleware<CircuitBreakerMiddleware>();
        return builder;
    }
}