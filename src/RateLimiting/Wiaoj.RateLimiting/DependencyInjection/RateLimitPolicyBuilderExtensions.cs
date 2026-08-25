using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Wiaoj.DistributedCounter;
using Wiaoj.Preconditions;
using Wiaoj.RateLimiting.DependencyInjection;

namespace Wiaoj.RateLimiting;

/// <summary>
/// Extension methods for configuring built-in algorithms and resilience decorators on <see cref="IRateLimitPolicyBuilder"/>.
/// </summary>
public static class RateLimitPolicyBuilderExtensions {

    /// <summary>
    /// Enables Fail-Open resilience on this policy. If the underlying storage fails, requests are allowed through.
    /// </summary>
    public static IRateLimitPolicyBuilder WithFailOpen(this IRateLimitPolicyBuilder builder) {
        Preca.ThrowIfNull(builder);
        return builder.AddDecorator(static (sp, inner) => {
            ILogger<ResilientRateLimiter> logger = sp.GetService<ILogger<ResilientRateLimiter>>()
                ?? NullLogger<ResilientRateLimiter>.Instance;
            return new ResilientRateLimiter(inner, logger);
        });
    }

    /// <summary>
    /// Enables in-memory L1 negative caching to short-circuit denied requests without querying remote storage.
    /// </summary>
    public static IRateLimitPolicyBuilder WithNegativeCaching(this IRateLimitPolicyBuilder builder) {
        Preca.ThrowIfNull(builder);
        return builder.AddDecorator(static (sp, inner) => {
            TimeProvider timeProvider = sp.GetRequiredService<TimeProvider>();
            ILogger<NegativeCacheRateLimiter> logger = sp.GetService<ILogger<NegativeCacheRateLimiter>>()
                ?? NullLogger<NegativeCacheRateLimiter>.Instance;
            return new NegativeCacheRateLimiter(inner, timeProvider, logger);
        });
    }

    /// <summary>
    /// Configures a fixed-window algorithm backed by distributed counters.
    /// </summary>
    public static IRateLimitPolicyBuilder UseFixedWindow(
        this IRateLimitPolicyBuilder builder,
        int limit,
        TimeSpan window) {
        Preca.ThrowIfNull(builder);
        EnsureImmediateCounter(builder);

        return builder.UseAlgorithm(sp => {
            IDistributedCounterFactory factory = sp.GetRequiredService<IDistributedCounterFactory>();
            ILogger<FixedWindowRateLimiter> logger = sp.GetService<ILogger<FixedWindowRateLimiter>>()
                ?? NullLogger<FixedWindowRateLimiter>.Instance;
            return new FixedWindowRateLimiter(factory, builder.PolicyName, limit, window, logger);
        });
    }

    /// <summary>
    /// Configures a weighted sliding-window algorithm backed by distributed counters.
    /// </summary>
    public static IRateLimitPolicyBuilder UseSlidingWindow(
        this IRateLimitPolicyBuilder builder,
        int limit,
        TimeSpan window) {
        Preca.ThrowIfNull(builder);
        EnsureImmediateCounter(builder);

        return builder.UseAlgorithm(sp => {
            IDistributedCounterFactory factory = sp.GetRequiredService<IDistributedCounterFactory>();
            TimeProvider timeProvider = sp.GetRequiredService<TimeProvider>();
            ILogger<SlidingWindowRateLimiter> logger = sp.GetService<ILogger<SlidingWindowRateLimiter>>()
                ?? NullLogger<SlidingWindowRateLimiter>.Instance;
            return new SlidingWindowRateLimiter(factory, builder.PolicyName, limit, window, timeProvider, logger);
        });
    }

    /// <summary>
    /// Configures a Generic Cell Rate Algorithm (GCRA) backed by distributed counters with atomic CAS.
    /// </summary>
    public static IRateLimitPolicyBuilder UseGcra(
        this IRateLimitPolicyBuilder builder,
        int limit,
        TimeSpan period) {
        Preca.ThrowIfNull(builder);
        EnsureImmediateCounter(builder);

        return builder.UseAlgorithm(sp => {
            IDistributedCounterFactory factory = sp.GetRequiredService<IDistributedCounterFactory>();
            TimeProvider timeProvider = sp.GetRequiredService<TimeProvider>();
            ILogger<GcraRateLimiter> logger = sp.GetService<ILogger<GcraRateLimiter>>()
                ?? NullLogger<GcraRateLimiter>.Instance;
            return new GcraRateLimiter(factory, builder.PolicyName, limit, period, timeProvider, logger);
        });
    }

    /// <summary>
    /// Configures an in-memory token bucket algorithm.
    /// </summary>
    public static IRateLimitPolicyBuilder UseTokenBucket(
        this IRateLimitPolicyBuilder builder,
        int capacity,
        TimeSpan window) {
        Preca.ThrowIfNull(builder);

        return builder.UseAlgorithm(sp => {
            TimeProvider timeProvider = sp.GetRequiredService<TimeProvider>();
            ILogger<TokenBucketRateLimiter> logger = sp.GetService<ILogger<TokenBucketRateLimiter>>()
                ?? NullLogger<TokenBucketRateLimiter>.Instance;
            return new TokenBucketRateLimiter(capacity, window, timeProvider, logger);
        });
    }

    /// <summary>
    /// Configures an in-memory exact sliding-window log algorithm.
    /// </summary>
    public static IRateLimitPolicyBuilder UseSlidingWindowLog(
        this IRateLimitPolicyBuilder builder,
        int limit,
        TimeSpan window) {
        Preca.ThrowIfNull(builder);

        return builder.UseAlgorithm(sp => {
            TimeProvider timeProvider = sp.GetRequiredService<TimeProvider>();
            ILogger<SlidingWindowLogRateLimiter> logger = sp.GetService<ILogger<SlidingWindowLogRateLimiter>>()
                ?? NullLogger<SlidingWindowLogRateLimiter>.Instance;
            return new SlidingWindowLogRateLimiter(limit, window, timeProvider, logger);
        });
    }

    /// <summary>
    /// Configures an in-memory leaky-bucket queue traffic shaping algorithm.
    /// </summary>
    public static IRateLimitPolicyBuilder UseLeakyBucketQueue(
        this IRateLimitPolicyBuilder builder,
        int capacity,
        TimeSpan period) {
        Preca.ThrowIfNull(builder);

        return builder.UseAlgorithm(sp => {
            TimeProvider timeProvider = sp.GetRequiredService<TimeProvider>();
            ILogger<LeakyBucketQueueRateLimiter> logger = sp.GetService<ILogger<LeakyBucketQueueRateLimiter>>()
                ?? NullLogger<LeakyBucketQueueRateLimiter>.Instance;
            return new LeakyBucketQueueRateLimiter(capacity, period, timeProvider, logger);
        });
    }

    /// <summary>
    /// Configures a custom rate limiting algorithm implementation from dependency injection.
    /// </summary>
    public static IRateLimitPolicyBuilder UseAlgorithm<TAlgorithm>(this IRateLimitPolicyBuilder builder)
        where TAlgorithm : class, IRateLimitAlgorithm {
        Preca.ThrowIfNull(builder);
        return builder.UseAlgorithm(static sp => ActivatorUtilities.GetServiceOrCreateInstance<TAlgorithm>(sp));
    }

    /// <summary>
    /// Configures a multi-tier composite rate limiting policy by evaluating multiple algorithms in sequence.
    /// All configured tiers must permit the request for it to be allowed.
    /// </summary>
    /// <param name="builder">The policy builder.</param>
    /// <param name="tierConfigurators">The sequence of child policy configurations representing each tier.</param>
    /// <returns>The policy builder instance for fluent chaining.</returns>
    public static IRateLimitPolicyBuilder UseComposite(
        this IRateLimitPolicyBuilder builder,
        params Action<IRateLimitPolicyBuilder>[] tierConfigurators) {
        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNull(tierConfigurators);

        if(tierConfigurators.Length == 0) {
            throw new ArgumentException("At least one rate limiting tier must be configured for a composite policy.", nameof(tierConfigurators));
        }

        return builder.UseAlgorithm(sp => {
            IRateLimitAlgorithm[] algorithms = new IRateLimitAlgorithm[tierConfigurators.Length];

            for(int i = 0; i < tierConfigurators.Length; i++) {
                // Creates a sub-builder for each tier sharing the same policy namespace
                RateLimitPolicyBuilder tierBuilder = new(builder.Services, $"{builder.PolicyName}:tier_{i + 1}");
                tierConfigurators[i](tierBuilder);
                algorithms[i] = tierBuilder.Build()(sp);
            }

            ILogger<CompositeRateLimiter> logger = sp.GetService<ILogger<CompositeRateLimiter>>()
                ?? NullLogger<CompositeRateLimiter>.Instance;

            return new CompositeRateLimiter(algorithms, logger);
        });
    }

    private static void EnsureImmediateCounter(IRateLimitPolicyBuilder builder) {
        builder.Services.Configure<DistributedCounterOptions>(options => {
            options.AddImmediateCounter(builder.PolicyName);
        });
    }
}