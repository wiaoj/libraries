using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
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
    /// <param name="builder">The webhook builder being configured.</param>
    /// <returns>The <see cref="IWebhookBuilder"/> instance for fluent method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> is <see langword="null"/>.</exception>
    public static IWebhookBuilder UseCircuitBreaker(this IWebhookBuilder builder) {
        return UseCircuitBreaker(builder, new CircuitBreakerOptions());
    }

    /// <summary>
    /// Enables consecutive failures circuit breaker protection with a configuration delegate.
    /// </summary>
    /// <param name="builder">The webhook builder being configured.</param>
    /// <param name="configure">The delegate used to configure <see cref="CircuitBreakerOptions"/>.</param>
    /// <returns>The <see cref="IWebhookBuilder"/> instance for fluent method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> or <paramref name="configure"/> is <see langword="null"/>.</exception>
    public static IWebhookBuilder UseCircuitBreaker(
        this IWebhookBuilder builder,
        Action<CircuitBreakerOptions> configure) {
        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNull(configure);

        CircuitBreakerOptions options = new();
        configure(options);
        return UseCircuitBreaker(builder, options);
    }

    /// <summary>
    /// Enables consecutive failures circuit breaker protection with explicit options.
    /// </summary>
    /// <param name="builder">The webhook builder being configured.</param>
    /// <param name="options">The configured circuit breaker options instance.</param>
    /// <returns>The <see cref="IWebhookBuilder"/> instance for fluent method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> or <paramref name="options"/> is <see langword="null"/>.</exception>
    public static IWebhookBuilder UseCircuitBreaker(
        this IWebhookBuilder builder,
        CircuitBreakerOptions options) {
        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNull(options);
        options.Validate();

        builder.Services.AddWiaojResilience(resilience => {
            resilience.UseDefaultConsecutiveBreaker(opt => {
                opt.KeyPrefix = options.KeyPrefix;
                opt.FailureThreshold = options.FailureThreshold;
                opt.BreakDuration = options.BreakDuration;
            });
        });

        builder.Services.TryAddSingleton<ICircuitBreaker>(static sp =>
            sp.GetRequiredService<ICircuitBreakerFactory>().Create());

        builder.AddMiddleware<CircuitBreakerMiddleware>();
        return builder;
    }

    /// <summary>
    /// Enables percentage-based sampling window circuit breaker protection with default options.
    /// </summary>
    /// <param name="builder">The webhook builder being configured.</param>
    /// <returns>The <see cref="IWebhookBuilder"/> instance for fluent method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> is <see langword="null"/>.</exception>
    public static IWebhookBuilder UseSamplingCircuitBreaker(this IWebhookBuilder builder) {
        return UseSamplingCircuitBreaker(builder, new SamplingWindowCircuitBreakerOptions());
    }

    /// <summary>
    /// Enables percentage-based sampling window circuit breaker protection with a configuration delegate.
    /// </summary>
    /// <param name="builder">The webhook builder being configured.</param>
    /// <param name="configure">The delegate used to configure <see cref="SamplingWindowCircuitBreakerOptions"/>.</param>
    /// <returns>The <see cref="IWebhookBuilder"/> instance for fluent method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> or <paramref name="configure"/> is <see langword="null"/>.</exception>
    public static IWebhookBuilder UseSamplingCircuitBreaker(
        this IWebhookBuilder builder,
        Action<SamplingWindowCircuitBreakerOptions> configure) {
        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNull(configure);

        SamplingWindowCircuitBreakerOptions options = new();
        configure(options);
        return UseSamplingCircuitBreaker(builder, options);
    }

    /// <summary>
    /// Enables percentage-based sampling window circuit breaker protection with explicit options.
    /// </summary>
    /// <param name="builder">The webhook builder being configured.</param>
    /// <param name="options">The configured sampling window options instance.</param>
    /// <returns>The <see cref="IWebhookBuilder"/> instance for fluent method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> or <paramref name="options"/> is <see langword="null"/>.</exception>
    public static IWebhookBuilder UseSamplingCircuitBreaker(
        this IWebhookBuilder builder,
        SamplingWindowCircuitBreakerOptions options) {
        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNull(options);
        options.Validate();

        builder.Services.AddWiaojResilience(resilience => {
            resilience.UseDefaultSamplingBreaker(opt => {
                opt.KeyPrefix = options.KeyPrefix;
                opt.FailureRateThreshold = options.FailureRateThreshold;
                opt.MinimumThroughput = options.MinimumThroughput;
                opt.SamplingWindow = options.SamplingWindow;
                opt.BreakDuration = options.BreakDuration;
                opt.PermittedNumberOfCallsInHalfOpenState = options.PermittedNumberOfCallsInHalfOpenState;
            });
        });

        builder.Services.TryAddSingleton<ICircuitBreaker>(static sp =>
            sp.GetRequiredService<ICircuitBreakerFactory>().Create());

        builder.AddMiddleware<CircuitBreakerMiddleware>();
        return builder;
    }
}