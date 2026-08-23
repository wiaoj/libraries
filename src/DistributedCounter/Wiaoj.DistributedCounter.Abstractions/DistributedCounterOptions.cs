namespace Wiaoj.DistributedCounter;

/// <summary>
/// Root configuration options for the distributed counter engine.
/// </summary>
public sealed class DistributedCounterOptions {
    /// <summary>
    /// The default configuration section name in application settings (e.g. appsettings.json).
    /// </summary>
    public const string SectionName = "DistributedCounter";

    /// <summary>
    /// Gets or sets the default counter synchronization strategy when not overridden per counter.
    /// Default is <see cref="CounterStrategy.Buffered"/>.
    /// </summary>
    public CounterStrategy DefaultStrategy { get; set; } = CounterStrategy.Buffered;

    /// <summary>
    /// Gets or sets the interval at which buffered counters are flushed to remote storage.
    /// Default is 5 seconds.
    /// </summary>
    public TimeSpan AutoFlushInterval { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Gets or sets the global prefix prepended to all counter keys in storage (e.g., "wiaoj:counter:").
    /// </summary>
    public string GlobalKeyPrefix { get; set; } = "wiaoj:counter:";

    /// <summary>
    /// Gets the registered counter-specific configurations.
    /// </summary>
    public Dictionary<string, CounterConfiguration> Registrations { get; init; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Registers a specific counter with a customized synchronization strategy.
    /// </summary>
    /// <param name="name">The unique name of the counter.</param>
    /// <param name="strategy">The synchronization strategy to use for this counter.</param>
    public void AddCounter(string name, CounterStrategy strategy) {
        this.Registrations[name] = new CounterConfiguration {
            Name = name,
            Strategy = strategy
        };
    }
}

/// <summary>
/// Represents counter-specific configuration settings.
/// </summary>
public sealed class CounterConfiguration {
    /// <summary>
    /// Gets or sets the counter name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the synchronization strategy for this counter.
    /// </summary>
    public CounterStrategy Strategy { get; set; } = CounterStrategy.Buffered;

    /// <summary>
    /// Initializes a new instance of the <see cref="CounterConfiguration"/> class.
    /// </summary>
    public CounterConfiguration() { }

    /// <summary>
    /// Initializes a new instance of the <see cref="CounterConfiguration"/> class with specified parameters.
    /// </summary>
    /// <param name="name">The counter name.</param>
    /// <param name="strategy">The synchronization strategy.</param>
    public CounterConfiguration(string name, CounterStrategy strategy) {
        this.Name = name;
        this.Strategy = strategy;
    }
}