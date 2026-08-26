using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Wiaoj.DistributedCounter;

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
        params string[] subPolicyNames) {
        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNullOrWhiteSpace(policyName);
        Preca.ThrowIfNull(subPolicyNames);

        if(subPolicyNames.Length == 0) {
            throw new ArgumentException("Composite circuit breaker requires at least one sub-policy name.", nameof(subPolicyNames));
        }

        return builder.AddPolicy(policyName, sp => {
            ICircuitBreakerFactory factory = sp.GetRequiredService<ICircuitBreakerFactory>();
            ICircuitBreaker[] breakers = new ICircuitBreaker[subPolicyNames.Length];

            for(int i = 0; i < subPolicyNames.Length; i++) {
                breakers[i] = factory.Create(subPolicyNames[i]);
            }

            ILogger<CompositeCircuitBreaker> logger = sp.GetService<ILogger<CompositeCircuitBreaker>>()
                ?? NullLogger<CompositeCircuitBreaker>.Instance;

            return new CompositeCircuitBreaker(breakers, logger);
        });
    }


    /// <summary>Registers a fixed timeout policy by name.</summary>
    public static IResilienceBuilder AddFixedTimeout(
        this IResilienceBuilder builder,
        string policyName,
        TimeSpan timeout) {
        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNullOrWhiteSpace(policyName);
        Preca.ThrowIfNegativeOrZero(timeout);

        return builder.AddTimeoutPolicy(policyName, sp => {
            TimeProvider timeProvider = sp.GetRequiredService<TimeProvider>();
            ILogger<FixedTimeoutStrategy> logger = sp.GetService<ILogger<FixedTimeoutStrategy>>()
                ?? NullLogger<FixedTimeoutStrategy>.Instance;
            return new FixedTimeoutStrategy(timeout, timeProvider, logger);
        });
    }

    /// <summary>Registers a fixed timeout policy with a strongly-typed tag.</summary>
    public static IResilienceBuilder AddFixedTimeout<TPolicy>(
        this IResilienceBuilder builder,
        TimeSpan timeout) where TPolicy : notnull {
        return builder.AddFixedTimeout(typeof(TPolicy).Name, timeout);
    }

    /// <summary>Configures the default fixed timeout strategy.</summary>
    public static IResilienceBuilder UseDefaultFixedTimeout(
        this IResilienceBuilder builder,
        TimeSpan timeout) {
        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNegativeOrZero(timeout);

        return builder.UseDefaultTimeoutPolicy(sp => {
            TimeProvider timeProvider = sp.GetRequiredService<TimeProvider>();
            ILogger<FixedTimeoutStrategy> logger = sp.GetService<ILogger<FixedTimeoutStrategy>>()
                ?? NullLogger<FixedTimeoutStrategy>.Instance;
            return new FixedTimeoutStrategy(timeout, timeProvider, logger);
        });
    }
}