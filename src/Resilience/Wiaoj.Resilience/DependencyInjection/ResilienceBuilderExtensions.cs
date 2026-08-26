using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Wiaoj.DistributedCounter;
using Wiaoj.Resilience.DependencyInjection;

namespace Wiaoj.Resilience;

/// <summary>
/// Extension methods for configuring built-in circuit breaker policies on <see cref="IResilienceBuilder"/>.
/// </summary>
public static class ResilienceBuilderExtensions {
    // ── Consecutive Failures ──────────────────────────────────────────────────

    /// <summary>Registers a consecutive failures circuit breaker policy by name.</summary>
    public static IResilienceBuilder AddConsecutiveBreaker(
        this IResilienceBuilder builder,
        string policyName,
        Action<CircuitBreakerOptions> configure) {
        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNullOrWhiteSpace(policyName);
        Preca.ThrowIfNull(configure);

        CircuitBreakerOptions options = new();
        configure(options);
        options.Validate();

        return builder.AddPolicy(policyName, sp => {
            IDistributedCounterFactory counterFactory = sp.GetRequiredService<IDistributedCounterFactory>();
            TimeProvider timeProvider = sp.GetRequiredService<TimeProvider>();
            ILogger<ConsecutiveFailuresCircuitBreaker> logger = sp.GetService<ILogger<ConsecutiveFailuresCircuitBreaker>>()
                ?? NullLogger<ConsecutiveFailuresCircuitBreaker>.Instance;
            return new ConsecutiveFailuresCircuitBreaker(counterFactory, options, timeProvider, logger);
        });
    }

    /// <summary>Registers a consecutive failures circuit breaker policy with a strongly-typed tag.</summary>
    public static IResilienceBuilder AddConsecutiveBreaker<TPolicy>(
        this IResilienceBuilder builder,
        Action<CircuitBreakerOptions> configure) where TPolicy : notnull {
        return builder.AddConsecutiveBreaker(typeof(TPolicy).Name, configure);
    }

    /// <summary>Configures the default consecutive failures circuit breaker.</summary>
    public static IResilienceBuilder UseDefaultConsecutiveBreaker(
        this IResilienceBuilder builder,
        Action<CircuitBreakerOptions> configure) {
        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNull(configure);

        CircuitBreakerOptions options = new();
        configure(options);
        options.Validate();

        return builder.UseDefaultPolicy(sp => {
            IDistributedCounterFactory counterFactory = sp.GetRequiredService<IDistributedCounterFactory>();
            TimeProvider timeProvider = sp.GetRequiredService<TimeProvider>();
            ILogger<ConsecutiveFailuresCircuitBreaker> logger = sp.GetService<ILogger<ConsecutiveFailuresCircuitBreaker>>()
                ?? NullLogger<ConsecutiveFailuresCircuitBreaker>.Instance;
            return new ConsecutiveFailuresCircuitBreaker(counterFactory, options, timeProvider, logger);
        });
    }

    // ── Sampling Window ───────────────────────────────────────────────────────

    /// <summary>Registers a percentage-based sampling window circuit breaker policy by name.</summary>
    public static IResilienceBuilder AddSamplingBreaker(
        this IResilienceBuilder builder,
        string policyName,
        Action<SamplingWindowCircuitBreakerOptions> configure) {
        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNullOrWhiteSpace(policyName);
        Preca.ThrowIfNull(configure);

        SamplingWindowCircuitBreakerOptions options = new();
        configure(options);
        options.Validate();

        return builder.AddPolicy(policyName, sp => {
            IDistributedCounterFactory counterFactory = sp.GetRequiredService<IDistributedCounterFactory>();
            TimeProvider timeProvider = sp.GetRequiredService<TimeProvider>();
            ILogger<SamplingWindowCircuitBreaker> logger = sp.GetService<ILogger<SamplingWindowCircuitBreaker>>()
                ?? NullLogger<SamplingWindowCircuitBreaker>.Instance;
            return new SamplingWindowCircuitBreaker(counterFactory, options, timeProvider, logger);
        });
    }

    /// <summary>Registers a percentage-based sampling window circuit breaker policy with a strongly-typed tag.</summary>
    public static IResilienceBuilder AddSamplingBreaker<TPolicy>(
        this IResilienceBuilder builder,
        Action<SamplingWindowCircuitBreakerOptions> configure) where TPolicy : notnull {
        return builder.AddSamplingBreaker(typeof(TPolicy).Name, configure);
    }

    /// <summary>Configures the default sampling window circuit breaker.</summary>
    public static IResilienceBuilder UseDefaultSamplingBreaker(
        this IResilienceBuilder builder,
        Action<SamplingWindowCircuitBreakerOptions> configure) {
        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNull(configure);

        SamplingWindowCircuitBreakerOptions options = new();
        configure(options);
        options.Validate();

        return builder.UseDefaultPolicy(sp => {
            IDistributedCounterFactory counterFactory = sp.GetRequiredService<IDistributedCounterFactory>();
            TimeProvider timeProvider = sp.GetRequiredService<TimeProvider>();
            ILogger<SamplingWindowCircuitBreaker> logger = sp.GetService<ILogger<SamplingWindowCircuitBreaker>>()
                ?? NullLogger<SamplingWindowCircuitBreaker>.Instance;
            return new SamplingWindowCircuitBreaker(counterFactory, options, timeProvider, logger);
        });
    }

    // ── Composite Circuit Breaker ─────────────────────────────────────────────

    /// <summary>Registers a multi-tier composite circuit breaker policy evaluating multiple child breaker configurations in sequence.</summary>
    public static IResilienceBuilder AddCompositeBreaker(
        this IResilienceBuilder builder,
        string policyName,
        params Action<IResilienceBuilder>[] tierConfigurators) {
        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNullOrWhiteSpace(policyName);
        Preca.ThrowIfNull(tierConfigurators);

        if(tierConfigurators.Length == 0) {
            throw new ArgumentException("Composite circuit breaker requires at least one breaker tier.", nameof(tierConfigurators));
        }

        return builder.AddPolicy(policyName, sp => {
            ICircuitBreaker[] breakers = new ICircuitBreaker[tierConfigurators.Length];

            for(int i = 0; i < tierConfigurators.Length; i++) {
                string subPolicyName = $"{policyName}:tier_{i + 1}";
                ResilienceBuilder subBuilder = new(builder.Services);
                tierConfigurators[i](subBuilder);

                IOptions<ResilienceOptions> subOptions = sp.GetRequiredService<IOptions<ResilienceOptions>>();
                breakers[i] = subOptions.Value.Policies[subPolicyName](sp);
            }

            ILogger<CompositeCircuitBreaker> logger = sp.GetService<ILogger<CompositeCircuitBreaker>>()
                ?? NullLogger<CompositeCircuitBreaker>.Instance;

            return new CompositeCircuitBreaker(breakers, logger);
        });
    }
}