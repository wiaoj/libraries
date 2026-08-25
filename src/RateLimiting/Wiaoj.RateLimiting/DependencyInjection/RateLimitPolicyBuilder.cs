using Microsoft.Extensions.DependencyInjection;
using Wiaoj.Preconditions;

namespace Wiaoj.RateLimiting.DependencyInjection;

internal sealed class RateLimitPolicyBuilder : IRateLimitPolicyBuilder {
    private Func<IServiceProvider, IRateLimitAlgorithm>? _algorithmFactory;
    private readonly List<Func<IServiceProvider, IRateLimitAlgorithm, IRateLimitAlgorithm>> _decorators = [];

    public IServiceCollection Services { get; }
    public string PolicyName { get; }

    public RateLimitPolicyBuilder(IServiceCollection services, string policyName) {
        Preca.ThrowIfNull(services);
        Preca.ThrowIfNullOrWhiteSpace(policyName);

        this.Services = services;
        this.PolicyName = policyName;
    }

    public IRateLimitPolicyBuilder UseAlgorithm(Func<IServiceProvider, IRateLimitAlgorithm> factory) {
        Preca.ThrowIfNull(factory);
        this._algorithmFactory = factory;
        return this;
    }

    public IRateLimitPolicyBuilder AddDecorator(Func<IServiceProvider, IRateLimitAlgorithm, IRateLimitAlgorithm> decorator) {
        Preca.ThrowIfNull(decorator);
        this._decorators.Add(decorator);
        return this;
    }

    internal Func<IServiceProvider, IRateLimitAlgorithm> Build() {
        if(this._algorithmFactory is null) {
            throw new InvalidOperationException($"No rate limiting algorithm was configured for policy '{this.PolicyName}'.");
        }

        return sp => {
            IRateLimitAlgorithm algorithm = this._algorithmFactory(sp);
            for(int i = 0; i < this._decorators.Count; i++) {
                algorithm = this._decorators[i](sp, algorithm);
            }
            return algorithm;
        };
    }
}