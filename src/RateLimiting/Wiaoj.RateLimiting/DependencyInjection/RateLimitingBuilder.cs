using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Wiaoj.DistributedCounter;
using Wiaoj.Preconditions;
using Wiaoj.RateLimiting.Resilience;

namespace Wiaoj.RateLimiting.DependencyInjection;

/// <summary>
/// Default implementation of <see cref="IRateLimitingBuilder"/>.
/// </summary>
internal sealed class RateLimitingBuilder : IRateLimitingBuilder {
    public IServiceCollection Services { get; }

    public RateLimitingBuilder(IServiceCollection services) {
        Preca.ThrowIfNull(services);
        this.Services = services;
    }

    public IRateLimitingBuilder WithFailOpen() {
        // Decorate the registered IRateLimitAlgorithm with ResilientRateLimiter
        ServiceDescriptor? existing = this.Services.FirstOrDefault(s => s.ServiceType == typeof(IRateLimitAlgorithm));
        if(existing is not null) {
            this.Services.Remove(existing);
            this.Services.AddSingleton<IRateLimitAlgorithm>(sp => {
                IRateLimitAlgorithm inner = (IRateLimitAlgorithm)(existing.ImplementationFactory?.Invoke(sp)
                    ?? ActivatorUtilities.GetServiceOrCreateInstance(sp, existing.ImplementationType!));
                ILogger<ResilientRateLimiter> logger = sp.GetService<ILogger<ResilientRateLimiter>>()
                    ?? NullLogger<ResilientRateLimiter>.Instance;
                return new ResilientRateLimiter(inner, logger);
            });
        }
        return this;
    }

    public IRateLimitingBuilder WithNegativeCaching() {
        ServiceDescriptor? existing = this.Services.FirstOrDefault(s => s.ServiceType == typeof(IRateLimitAlgorithm));
        if(existing is not null) {
            this.Services.Remove(existing);
            this.Services.AddSingleton<IRateLimitAlgorithm>(sp => {
                IRateLimitAlgorithm inner = (IRateLimitAlgorithm)(existing.ImplementationFactory?.Invoke(sp)
                    ?? ActivatorUtilities.GetServiceOrCreateInstance(sp, existing.ImplementationType!));
                TimeProvider timeProvider = sp.GetRequiredService<TimeProvider>();
                ILogger<NegativeCacheRateLimiter> logger = sp.GetService<ILogger<NegativeCacheRateLimiter>>()
                    ?? NullLogger<NegativeCacheRateLimiter>.Instance;
                return new NegativeCacheRateLimiter(inner, timeProvider, logger);
            });
        }
        return this;
    }

    public IRateLimitingBuilder UseFixedWindow(int limit, TimeSpan window) {
        return UseFixedWindow(static sp => sp.GetRequiredService<IDistributedCounterFactory>(), limit, window);
    }

    public IRateLimitingBuilder UseFixedWindow(Func<IServiceProvider, IDistributedCounterFactory> factoryResolver, int limit, TimeSpan window) {
        Preca.ThrowIfNull(factoryResolver);
        this.Services.RemoveAll<IRateLimitAlgorithm>();
        this.Services.AddSingleton<IRateLimitAlgorithm>(sp => {
            IDistributedCounterFactory factory = factoryResolver(sp);
            ILogger<FixedWindowRateLimiter> logger = sp.GetService<ILogger<FixedWindowRateLimiter>>()
                ?? NullLogger<FixedWindowRateLimiter>.Instance;
            return new FixedWindowRateLimiter(factory, limit, window, logger);
        });
        return this;
    }

    public IRateLimitingBuilder UseSlidingWindow(int limit, TimeSpan window) {
        return UseSlidingWindow(static sp => sp.GetRequiredService<IDistributedCounterFactory>(), limit, window);
    }

    public IRateLimitingBuilder UseSlidingWindow(Func<IServiceProvider, IDistributedCounterFactory> factoryResolver, int limit, TimeSpan window) {
        Preca.ThrowIfNull(factoryResolver);
        this.Services.RemoveAll<IRateLimitAlgorithm>();
        this.Services.AddSingleton<IRateLimitAlgorithm>(sp => {
            IDistributedCounterFactory factory = factoryResolver(sp);
            TimeProvider timeProvider = sp.GetRequiredService<TimeProvider>();
            ILogger<SlidingWindowRateLimiter> logger = sp.GetService<ILogger<SlidingWindowRateLimiter>>()
                ?? NullLogger<SlidingWindowRateLimiter>.Instance;
            return new SlidingWindowRateLimiter(factory, limit, window, timeProvider, logger);
        });
        return this;
    }

    public IRateLimitingBuilder UseDistributedGcra(int limit, TimeSpan period) {
        this.Services.RemoveAll<IRateLimitAlgorithm>();
        this.Services.AddSingleton<IRateLimitAlgorithm>(sp => {
            IDistributedCounterFactory factory = sp.GetRequiredService<IDistributedCounterFactory>();
            TimeProvider timeProvider = sp.GetRequiredService<TimeProvider>();
            ILogger<DistributedGcraRateLimiter> logger = sp.GetService<ILogger<DistributedGcraRateLimiter>>()
                ?? NullLogger<DistributedGcraRateLimiter>.Instance;
            return new DistributedGcraRateLimiter(factory, limit, period, timeProvider, logger);
        });
        return this;
    }

    public IRateLimitingBuilder UseTokenBucket(int capacity, TimeSpan window) {
        this.Services.RemoveAll<IRateLimitAlgorithm>();
        this.Services.AddSingleton<IRateLimitAlgorithm>(sp => {
            TimeProvider timeProvider = sp.GetRequiredService<TimeProvider>();
            ILogger<TokenBucketRateLimiter> logger = sp.GetService<ILogger<TokenBucketRateLimiter>>()
                ?? NullLogger<TokenBucketRateLimiter>.Instance;
            return new TokenBucketRateLimiter(capacity, window, timeProvider, logger);
        });
        return this;
    }

    public IRateLimitingBuilder UseGcra(int limit, TimeSpan period) {
        this.Services.RemoveAll<IRateLimitAlgorithm>();
        this.Services.AddSingleton<IRateLimitAlgorithm>(sp => {
            TimeProvider timeProvider = sp.GetRequiredService<TimeProvider>();
            ILogger<GcraRateLimiter> logger = sp.GetService<ILogger<GcraRateLimiter>>()
                ?? NullLogger<GcraRateLimiter>.Instance;
            return new GcraRateLimiter(limit, period, timeProvider, logger);
        });
        return this;
    }

    public IRateLimitingBuilder UseSlidingWindowLog(int limit, TimeSpan window) {
        this.Services.RemoveAll<IRateLimitAlgorithm>();
        this.Services.AddSingleton<IRateLimitAlgorithm>(sp => {
            TimeProvider timeProvider = sp.GetRequiredService<TimeProvider>();
            ILogger<SlidingWindowLogRateLimiter> logger = sp.GetService<ILogger<SlidingWindowLogRateLimiter>>()
                ?? NullLogger<SlidingWindowLogRateLimiter>.Instance;
            return new SlidingWindowLogRateLimiter(limit, window, timeProvider, logger);
        });
        return this;
    }

    public IRateLimitingBuilder UseLeakyBucketQueue(int capacity, TimeSpan period) {
        this.Services.RemoveAll<IRateLimitAlgorithm>();
        this.Services.AddSingleton<IRateLimitAlgorithm>(sp => {
            TimeProvider timeProvider = sp.GetRequiredService<TimeProvider>();
            ILogger<LeakyBucketQueueRateLimiter> logger = sp.GetService<ILogger<LeakyBucketQueueRateLimiter>>()
                ?? NullLogger<LeakyBucketQueueRateLimiter>.Instance;
            return new LeakyBucketQueueRateLimiter(capacity, period, timeProvider, logger);
        });
        return this;
    }

    public IRateLimitingBuilder UseAlgorithm<TAlgorithm>() where TAlgorithm : class, IRateLimitAlgorithm {
        this.Services.RemoveAll<IRateLimitAlgorithm>();
        this.Services.AddSingleton<IRateLimitAlgorithm, TAlgorithm>();
        return this;
    }

    public IRateLimitingBuilder UseAlgorithm(Func<IServiceProvider, IRateLimitAlgorithm> factory) {
        Preca.ThrowIfNull(factory);
        this.Services.RemoveAll<IRateLimitAlgorithm>();
        this.Services.AddSingleton<IRateLimitAlgorithm>(factory);
        return this;
    }
}