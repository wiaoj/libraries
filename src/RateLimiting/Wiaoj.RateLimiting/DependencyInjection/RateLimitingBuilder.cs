using Microsoft.Extensions.DependencyInjection;
using Wiaoj.Preconditions;

namespace Wiaoj.RateLimiting.DependencyInjection;

internal sealed class RateLimitingBuilder : IRateLimitingBuilder {
    public IServiceCollection Services { get; }

    public RateLimitingBuilder(IServiceCollection services) {
        Preca.ThrowIfNull(services);
        this.Services = services;
    }

    public IRateLimitingBuilder AddPolicy(string policyName, Action<IRateLimitPolicyBuilder> configure) {
        Preca.ThrowIfNullOrWhiteSpace(policyName);
        Preca.ThrowIfNull(configure);

        RateLimitPolicyBuilder policyBuilder = new(this.Services, policyName);
        configure(policyBuilder);

        this.Services.Configure<RateLimitingOptions>(options => {
            options.Policies[policyName] = policyBuilder.Build();
        });

        return this;
    }

    public IRateLimitingBuilder AddPolicy<TPolicy>(Action<IRateLimitPolicyBuilder> configure) where TPolicy : notnull {
        return AddPolicy(typeof(TPolicy).Name, configure);
    }

    public IRateLimitingBuilder UseDefaultPolicy(Action<IRateLimitPolicyBuilder> configure) {
        Preca.ThrowIfNull(configure);

        RateLimitPolicyBuilder policyBuilder = new(this.Services, "__Default__");
        configure(policyBuilder);

        this.Services.Configure<RateLimitingOptions>(options => {
            options.DefaultPolicy = policyBuilder.Build();
        });

        return this;
    }
}