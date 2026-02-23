namespace Wiaoj.DistributedCounter;
/// <summary>
/// Configuration options for the distributed counter system.
/// </summary>
public sealed record DistributedCounterOptions {
    /// <summary>
    /// Gets or sets the global default strategy. 
    /// This can be overridden per specific counter registration.
    /// </summary>
    public CounterStrategy DefaultStrategy { get; set; } = CounterStrategy.Buffered;

    /// <summary>
    /// Gets or sets the interval at which buffered counters are flushed to the remote storage.
    /// </summary>
    public TimeSpan AutoFlushInterval { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Gets or sets the global prefix for all keys in the storage (e.g., "app:counters:").
    /// </summary>
    public string GlobalKeyPrefix { get; set; } = "wiaoj:counter:";

    /// <summary>
    /// Gets the registered counter configurations.
    /// </summary>
    public Dictionary<string, CounterConfiguration> Registrations { get; } = [];

    /// <summary>
    /// Registers a counter with a specific name and strategy.
    /// </summary>
    /// <param name="name">The name of the counter.</param>
    /// <param name="strategy">The synchronization strategy.</param>
    public void AddCounter(string name, CounterStrategy strategy) {
        this.Registrations[name] = new CounterConfiguration(name, strategy);
    }
}

/// <summary>
/// Represents the configuration for a specific counter.
/// </summary>
public record CounterConfiguration(string Name, CounterStrategy Strategy);