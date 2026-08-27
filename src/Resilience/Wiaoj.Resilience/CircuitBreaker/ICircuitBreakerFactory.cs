namespace Wiaoj.Resilience;

/// <summary>
/// Factory contract for creating and resolving circuit breaker instances by policy name.
/// </summary>
public interface ICircuitBreakerFactory {
    /// <summary>Resolves the circuit breaker instance registered under the specified policy name.</summary>
    ICircuitBreaker Create(string policyName);

    /// <summary>Resolves the default fallback circuit breaker instance.</summary>
    ICircuitBreaker Create();
}