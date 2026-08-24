using Microsoft.Extensions.DependencyInjection;
using Wiaoj.DistributedCounter;

namespace Wiaoj.RateLimiting.DependencyInjection;

/// <summary>
/// A fluent builder for configuring rate limiting algorithms and dependencies.
/// </summary>
public interface IRateLimitingBuilder {
    /// <summary>
    /// Gets the application service collection being configured.
    /// </summary>
    IServiceCollection Services { get; } 

    /// <summary>
    /// Enables Fail-Open resilience on the configured algorithm. If the distributed storage fails or times out,
    /// requests are safely allowed through and errors are logged, preventing API outages.
    /// </summary>
    IRateLimitingBuilder WithFailOpen();

    /// <summary>
    /// Enables local L1 negative caching (denial short-circuiting). Rate-limited keys are remembered in memory
    /// for the duration of their <see cref="RateLimitDecision.RetryAfter"/> window, deflecting repeated spam
    /// without making network round-trips to distributed storage (Redis).
    /// </summary>
    IRateLimitingBuilder WithNegativeCaching();

    /// <summary>
    /// Configures a distributed fixed-window algorithm using the ambient <see cref="IDistributedCounterFactory"/> from DI.
    /// </summary>
    IRateLimitingBuilder UseFixedWindow(int limit, TimeSpan window);

    /// <summary>
    /// Configures a distributed fixed-window algorithm using an explicit/isolated <see cref="IDistributedCounterFactory"/>.
    /// </summary>
    IRateLimitingBuilder UseFixedWindow(Func<IServiceProvider, IDistributedCounterFactory> factoryResolver, int limit, TimeSpan window);

    /// <summary>
    /// Configures a distributed weighted sliding-window algorithm using the ambient <see cref="IDistributedCounterFactory"/> from DI.
    /// </summary>
    IRateLimitingBuilder UseSlidingWindow(int limit, TimeSpan window);

    /// <summary>
    /// Configures a distributed weighted sliding-window algorithm using an explicit/isolated <see cref="IDistributedCounterFactory"/>.
    /// </summary>
    IRateLimitingBuilder UseSlidingWindow(Func<IServiceProvider, IDistributedCounterFactory> factoryResolver, int limit, TimeSpan window);

    /// <summary>
    /// Configures a distributed GCRA (burst-tolerant) algorithm using the ambient <see cref="IDistributedCounterFactory"/> from DI.
    /// </summary>
    IRateLimitingBuilder UseDistributedGcra(int limit, TimeSpan period);

    /// <summary>
    /// Configures a self-contained in-memory token bucket algorithm.
    /// </summary>
    IRateLimitingBuilder UseTokenBucket(int capacity, TimeSpan window);

    /// <summary>
    /// Configures a self-contained in-memory GCRA algorithm.
    /// </summary>
    IRateLimitingBuilder UseGcra(int limit, TimeSpan period);

    /// <summary>
    /// Configures a self-contained in-memory exact sliding-window log algorithm.
    /// </summary>
    IRateLimitingBuilder UseSlidingWindowLog(int limit, TimeSpan window);

    /// <summary>
    /// Configures a self-contained in-memory leaky-bucket queue (traffic-shaping) algorithm.
    /// </summary>
    IRateLimitingBuilder UseLeakyBucketQueue(int capacity, TimeSpan period);

    /// <summary>
    /// Registers a custom <see cref="IRateLimitAlgorithm"/> implementation.
    /// </summary>
    IRateLimitingBuilder UseAlgorithm<TAlgorithm>() where TAlgorithm : class, IRateLimitAlgorithm;

    /// <summary>
    /// Registers a custom <see cref="IRateLimitAlgorithm"/> factory delegate.
    /// </summary>
    IRateLimitingBuilder UseAlgorithm(Func<IServiceProvider, IRateLimitAlgorithm> factory);
}