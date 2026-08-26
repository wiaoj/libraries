using Microsoft.Extensions.Options;

namespace Wiaoj.Resilience.Internal;

internal sealed class DefaultTimeoutStrategyFactory : ITimeoutStrategyFactory {
    private readonly IServiceProvider _serviceProvider;
    private readonly ResilienceOptions _options;

    public DefaultTimeoutStrategyFactory(
        IServiceProvider serviceProvider,
        IOptions<ResilienceOptions> options) {
        Preca.ThrowIfNull(serviceProvider);
        Preca.ThrowIfNull(options);

        this._serviceProvider = serviceProvider;
        this._options = options.Value;
    }

    public ITimeoutStrategy Create<TPolicy>() where TPolicy : notnull {
        return Create(typeof(TPolicy).Name);
    }

    public ITimeoutStrategy Create(string policyName) {
        Preca.ThrowIfNullOrWhiteSpace(policyName);

        if(this._options.TimeoutPolicies.TryGetValue(policyName, out Func<IServiceProvider, ITimeoutStrategy>? factory)) {
            return factory(this._serviceProvider);
        }

        if(this._options.DefaultTimeoutPolicy is not null) {
            return this._options.DefaultTimeoutPolicy(this._serviceProvider);
        }

        throw new InvalidOperationException($"No timeout policy named '{policyName}' was registered and no default timeout policy was configured.");
    }
}