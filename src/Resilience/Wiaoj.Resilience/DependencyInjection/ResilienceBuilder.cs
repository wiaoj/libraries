using Microsoft.Extensions.DependencyInjection;
using Wiaoj.DistributedCounter;

namespace Wiaoj.Resilience.DependencyInjection;

internal sealed class ResilienceBuilder : IResilienceBuilder {
    public IServiceCollection Services { get; }

    public ResilienceBuilder(IServiceCollection services) {
        Preca.ThrowIfNull(services);
        this.Services = services;
         
        services.Configure<DistributedCounterOptions>(static options => {
            options.AddImmediateCounter("CircuitBreaker");
        });
    }

    public IResilienceBuilder AddPolicy(string policyName, Func<IServiceProvider, ICircuitBreaker> factory) {
        Preca.ThrowIfNullOrWhiteSpace(policyName);
        Preca.ThrowIfNull(factory);

        this.Services.Configure<ResilienceOptions>(opt => {
            opt.Policies[policyName] = factory;
        });

        return this;
    }

    public IResilienceBuilder UseDefaultPolicy(Func<IServiceProvider, ICircuitBreaker> factory) {
        Preca.ThrowIfNull(factory);

        this.Services.Configure<ResilienceOptions>(opt => {
            opt.DefaultPolicy = factory;
        });

        return this;
    }
}