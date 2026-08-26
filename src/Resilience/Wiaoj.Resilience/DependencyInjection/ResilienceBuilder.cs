using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Wiaoj.DistributedCounter;

namespace Wiaoj.Resilience.DependencyInjection;

internal sealed class ResilienceBuilder : IResilienceBuilder {
    public IServiceCollection Services { get; }

    public ResilienceBuilder(IServiceCollection services) {
        Preca.ThrowIfNull(services);
        this.Services = services;

        // Automatically enforce that the "CircuitBreaker" counter category uses Immediate strategy
        services.Configure<DistributedCounterOptions>(static options => {
            options.AddImmediateCounter("CircuitBreaker");
        });
    }

    public IResilienceBuilder AddConsecutiveBreaker(string policyName, Action<CircuitBreakerOptions> configure) {
        Preca.ThrowIfNullOrWhiteSpace(policyName);
        Preca.ThrowIfNull(configure);

        CircuitBreakerOptions options = new();
        configure(options);
        options.Validate();

        this.Services.Configure<ResilienceOptions>(opt => {
            opt.Policies[policyName] = sp => {
                IDistributedCounterFactory counterFactory = sp.GetRequiredService<IDistributedCounterFactory>();
                TimeProvider timeProvider = sp.GetRequiredService<TimeProvider>();
                ILogger<ConsecutiveFailuresCircuitBreaker> logger = sp.GetService<ILogger<ConsecutiveFailuresCircuitBreaker>>()
                    ?? NullLogger<ConsecutiveFailuresCircuitBreaker>.Instance;
                return new ConsecutiveFailuresCircuitBreaker(counterFactory, options, timeProvider, logger);
            };
        });

        return this;
    }

    public IResilienceBuilder AddConsecutiveBreaker<TPolicy>(Action<CircuitBreakerOptions> configure) where TPolicy : notnull {
        return AddConsecutiveBreaker(typeof(TPolicy).Name, configure);
    }

    public IResilienceBuilder AddSamplingBreaker(string policyName, Action<SamplingWindowCircuitBreakerOptions> configure) {
        Preca.ThrowIfNullOrWhiteSpace(policyName);
        Preca.ThrowIfNull(configure);

        SamplingWindowCircuitBreakerOptions options = new();
        configure(options);
        options.Validate();

        this.Services.Configure<ResilienceOptions>(opt => {
            opt.Policies[policyName] = sp => {
                IDistributedCounterFactory counterFactory = sp.GetRequiredService<IDistributedCounterFactory>();
                TimeProvider timeProvider = sp.GetRequiredService<TimeProvider>();
                ILogger<SamplingWindowCircuitBreaker> logger = sp.GetService<ILogger<SamplingWindowCircuitBreaker>>()
                    ?? NullLogger<SamplingWindowCircuitBreaker>.Instance;
                return new SamplingWindowCircuitBreaker(counterFactory, options, timeProvider, logger);
            };
        });

        return this;
    }

    public IResilienceBuilder AddSamplingBreaker<TPolicy>(Action<SamplingWindowCircuitBreakerOptions> configure) where TPolicy : notnull {
        return AddSamplingBreaker(typeof(TPolicy).Name, configure);
    }

    public IResilienceBuilder UseDefaultConsecutiveBreaker(Action<CircuitBreakerOptions> configure) {
        Preca.ThrowIfNull(configure);

        CircuitBreakerOptions options = new();
        configure(options);
        options.Validate();

        this.Services.Configure<ResilienceOptions>(opt => {
            opt.DefaultPolicy = sp => {
                IDistributedCounterFactory counterFactory = sp.GetRequiredService<IDistributedCounterFactory>();
                TimeProvider timeProvider = sp.GetRequiredService<TimeProvider>();
                ILogger<ConsecutiveFailuresCircuitBreaker> logger = sp.GetService<ILogger<ConsecutiveFailuresCircuitBreaker>>()
                    ?? NullLogger<ConsecutiveFailuresCircuitBreaker>.Instance;
                return new ConsecutiveFailuresCircuitBreaker(counterFactory, options, timeProvider, logger);
            };
        });

        return this;
    }

    public IResilienceBuilder UseDefaultSamplingBreaker(Action<SamplingWindowCircuitBreakerOptions> configure) {
        Preca.ThrowIfNull(configure);

        SamplingWindowCircuitBreakerOptions options = new();
        configure(options);
        options.Validate();

        this.Services.Configure<ResilienceOptions>(opt => {
            opt.DefaultPolicy = sp => {
                IDistributedCounterFactory counterFactory = sp.GetRequiredService<IDistributedCounterFactory>();
                TimeProvider timeProvider = sp.GetRequiredService<TimeProvider>();
                ILogger<SamplingWindowCircuitBreaker> logger = sp.GetService<ILogger<SamplingWindowCircuitBreaker>>()
                    ?? NullLogger<SamplingWindowCircuitBreaker>.Instance;
                return new SamplingWindowCircuitBreaker(counterFactory, options, timeProvider, logger);
            };
        });

        return this;
    }
}
