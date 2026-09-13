using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using Wiaoj.Resilience.DependencyInjection;

namespace Wiaoj.Resilience.Internal;

internal sealed class DefaultCircuitBreakerFactory : ICircuitBreakerFactory {
    private readonly IServiceProvider _serviceProvider;
    private readonly ResilienceOptions _options;
    private readonly ConcurrentDictionary<string, ICircuitBreaker> _resolvedBreakers = new(StringComparer.Ordinal);
    private readonly Lazy<ICircuitBreaker?> _defaultBreaker;

    public DefaultCircuitBreakerFactory(
        IServiceProvider serviceProvider,
        IOptions<ResilienceOptions> options) {
        Preca.ThrowIfNull(serviceProvider);
        Preca.ThrowIfNull(options);

        this._serviceProvider = serviceProvider;
        this._options = options.Value;
        this._defaultBreaker = new Lazy<ICircuitBreaker?>(() => this._options.DefaultPolicy?.Invoke(this._serviceProvider) is { } breaker
            ? this.Guard(breaker)
            : null);
    }

    public ICircuitBreaker Create(string policyName) {
        Preca.ThrowIfNullOrWhiteSpace(policyName);

        return this._resolvedBreakers.GetOrAdd(policyName, name => {
            if(this._options.Policies.TryGetValue(name, out Func<IServiceProvider, ICircuitBreaker>? factory)) {
                return this.Guard(factory(this._serviceProvider));
            }

            throw new KeyNotFoundException($"Circuit breaker policy '{name}' was not found. Ensure it is registered via AddWiaojResilience.");
        });
    }

    public ICircuitBreaker Create() {
        ICircuitBreaker? breaker = this._defaultBreaker.Value;
        if(breaker is null) {
            throw new InvalidOperationException("No default circuit breaker policy is configured. Use UseDefault... during setup.");
        }
        return breaker;
    }

    /// <summary>Wraps a breaker so it fails open on store failure, when the application asked for that.</summary>
    private ICircuitBreaker Guard(ICircuitBreaker breaker) {
        if(this._options.FailOpenOnStorageFailure is not { } resilient || breaker is ResilientCircuitBreaker) {
            return breaker;
        }

        ILogger<ResilientCircuitBreaker> logger = this._serviceProvider.GetService<ILogger<ResilientCircuitBreaker>>()
            ?? NullLogger<ResilientCircuitBreaker>.Instance;

        return new ResilientCircuitBreaker(breaker, resilient, logger);
    }
}

internal sealed class TypedCircuitBreakerWrapper<TPolicy>(ICircuitBreakerFactory factory)
    : ICircuitBreaker<TPolicy> where TPolicy : notnull {

    private readonly Lazy<ICircuitBreaker> _inner = new(() => factory.Create(typeof(TPolicy).Name));

    public ValueTask<CircuitExecutionDecision> TryAcquireAsync(string key, CancellationToken cancellationToken = default) {
        return this._inner.Value.TryAcquireAsync(key, cancellationToken);
    }

    public ValueTask<CircuitState> GetStateAsync(string key, CancellationToken cancellationToken = default) {
        return this._inner.Value.GetStateAsync(key, cancellationToken);
    }

    public ValueTask OnSuccessAsync(string key, CancellationToken cancellationToken = default) {
        return this._inner.Value.OnSuccessAsync(key, cancellationToken);
    }

    public ValueTask OnFailureAsync(string key, CancellationToken cancellationToken = default) {
        return this._inner.Value.OnFailureAsync(key, cancellationToken);
    }
}