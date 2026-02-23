namespace Wiaoj.DistributedCounter; 
/// <summary>
/// Defines a contract for generating formatted storage keys for counters.
/// Supports simple names, generic keys, and tagged counters.
/// </summary>
public interface ICounterKeyBuilder {
    /// <summary>
    /// Builds a key for a simple named counter.
    /// </summary>
    CounterKey Build(string name, DistributedCounterOptions options);

    /// <summary>
    /// Builds a key for a named counter with a dynamic key (e.g., "visits:user_123").
    /// </summary>
    CounterKey Build<TKey>(string name, TKey key, DistributedCounterOptions options);

    /// <summary>
    /// Builds a key based on a marker type (Tag).
    /// </summary>
    CounterKey Build<TTag>(string name, DistributedCounterOptions options);

    /// <summary>
    /// Builds a key based on both a marker type and a dynamic key.
    /// </summary>
    CounterKey Build<TTag, TKey>(TKey key, DistributedCounterOptions options);
}