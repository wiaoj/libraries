using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Wiaoj.DistributedCounter;
using Wiaoj.Preconditions;
using Wiaoj.Resilience.CircuitBreaker;
using Wiaoj.Webhooks.Resilience;

#pragma warning disable IDE0130
namespace Wiaoj.Webhooks;
#pragma warning restore IDE0130

/// <summary>
/// Extension methods for configuring circuit breaker resilience policies in the webhook delivery pipeline.
/// </summary>
public static class WebhookBuilderResilienceExtensions {
    /// <summary>
    /// Enables endpoint-scoped circuit breaker protection with default options (5 failures, 1 minute break duration).
    /// </summary>
    /// <param name="builder">The webhook builder.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    public static IWebhookBuilder UseCircuitBreaker(this IWebhookBuilder builder) {
        return UseCircuitBreaker(builder, new CircuitBreakerOptions());
    }

    /// <summary>
    /// Enables endpoint-scoped circuit breaker protection with a configuration delegate.
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
    /// Enables endpoint-scoped circuit breaker protection with explicit options.
    /// </summary>
    /// <param name="builder">The webhook builder.</param>
    /// <param name="options">The circuit breaker options.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    public static IWebhookBuilder UseCircuitBreaker(this IWebhookBuilder builder, CircuitBreakerOptions options) {
        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNull(options);
        options.Validate();

        builder.Services.AddDistributedCounter(dc => dc.UseInMemory());

        builder.Services.TryAddSingleton<ICircuitBreakerStore>(sp => new DistributedCircuitBreakerStore(
            sp.GetRequiredService<IDistributedCounterFactory>(),
            sp.GetService<TimeProvider>() ?? TimeProvider.System,
            sp.GetService<ILogger<DistributedCircuitBreakerStore>>() ?? NullLogger<DistributedCircuitBreakerStore>.Instance));

        builder.Services.AddSingleton(options);
        builder.AddMiddleware<CircuitBreakerMiddleware>();

        return builder;
    }
}