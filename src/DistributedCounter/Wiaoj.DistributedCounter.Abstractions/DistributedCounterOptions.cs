using Wiaoj.Abstractions;
using Wiaoj.Preconditions;

namespace Wiaoj.DistributedCounter;

/// <summary>
/// Root configuration options for the distributed counter engine.
/// Supports deep cloning, merging, and fluent registration of typed and named counters.
/// </summary>
public sealed class DistributedCounterOptions : IDeepCloneable<DistributedCounterOptions>, IMergeable<DistributedCounterOptions> {

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
    /// Registers a specific named counter with a customized synchronization strategy.
    /// </summary>
    /// <param name="name">The unique name of the counter.</param>
    /// <param name="strategy">The synchronization strategy to use for this counter.</param>
    public void AddCounter(string name, CounterStrategy strategy) {
        Preca.ThrowIfNullOrWhiteSpace(name);

        this.Registrations[name] = new CounterConfiguration(name, strategy);
    }

    /// <summary>
    /// Registers a specific named counter using a configuration action.
    /// </summary>
    /// <param name="name">The unique name of the counter.</param>
    /// <param name="configure">The configuration delegate.</param>
    public void AddCounter(string name, Action<CounterConfiguration> configure) {
        Preca.ThrowIfNullOrWhiteSpace(name);
        Preca.ThrowIfNull(configure);

        CounterConfiguration config = new(name, this.DefaultStrategy);
        configure(config);
        this.Registrations[name] = config;
    }

    /// <summary>
    /// Registers a strongly-typed counter tag with default buffered synchronization strategy.
    /// </summary>
    /// <typeparam name="TTag">The marker type representing the counter category.</typeparam>
    public void AddCounter<TTag>() where TTag : notnull {
        AddCounter(typeof(TTag).Name, CounterStrategy.Buffered);
    }

    /// <summary>
    /// Registers a strongly-typed counter tag with a specific synchronization strategy.
    /// </summary>
    /// <typeparam name="TTag">The marker type representing the counter category.</typeparam>
    /// <param name="strategy">The synchronization strategy.</param>
    public void AddCounter<TTag>(CounterStrategy strategy) where TTag : notnull {
        AddCounter(typeof(TTag).Name, strategy);
    }

    /// <summary>
    /// Registers a strongly-typed counter tag using a configuration action.
    /// </summary>
    /// <typeparam name="TTag">The marker type representing the counter category.</typeparam>
    /// <param name="configure">The configuration delegate.</param>
    public void AddCounter<TTag>(Action<CounterConfiguration> configure) where TTag : notnull {
        AddCounter(typeof(TTag).Name, configure);
    }

    /// <summary>
    /// Registers a strongly-typed counter enforced with <see cref="CounterStrategy.Immediate"/> strategy.
    /// </summary>
    /// <typeparam name="TTag">The marker type representing the counter category.</typeparam>
    public void AddImmediateCounter<TTag>() where TTag : notnull {
        AddCounter<TTag>(CounterStrategy.Immediate);
    }

    /// <summary>
    /// Registers a strongly-typed counter enforced with <see cref="CounterStrategy.Immediate"/> strategy and custom configuration.
    /// </summary>
    /// <typeparam name="TTag">The marker type representing the counter category.</typeparam>
    /// <param name="configure">The configuration delegate.</param>
    public void AddImmediateCounter<TTag>(Action<CounterConfiguration> configure) where TTag : notnull {
        Preca.ThrowIfNull(configure);
        AddCounter<TTag>(cfg => {
            cfg.Strategy = CounterStrategy.Immediate;
            configure(cfg);
        });
    }

    /// <summary>
    /// Registers a named counter enforced with <see cref="CounterStrategy.Immediate"/> strategy.
    /// </summary>
    /// <param name="name">The unique name of the counter.</param>
    public void AddImmediateCounter(string name) {
        AddCounter(name, CounterStrategy.Immediate);
    }

    /// <summary>
    /// Registers a named counter enforced with <see cref="CounterStrategy.Immediate"/> strategy and custom configuration.
    /// </summary>
    /// <param name="name">The unique name of the counter.</param>
    /// <param name="configure">The configuration delegate.</param>
    public void AddImmediateCounter(string name, Action<CounterConfiguration> configure) {
        Preca.ThrowIfNullOrWhiteSpace(name);
        Preca.ThrowIfNull(configure);
        AddCounter(name, cfg => {
            cfg.Strategy = CounterStrategy.Immediate;
            configure(cfg);
        });
    }

    /// <summary>
    /// Registers a strongly-typed counter enforced with <see cref="CounterStrategy.Buffered"/> strategy.
    /// </summary>
    /// <typeparam name="TTag">The marker type representing the counter category.</typeparam>
    public void AddBufferedCounter<TTag>() where TTag : notnull {
        AddCounter<TTag>(CounterStrategy.Buffered);
    }

    /// <summary>
    /// Registers a strongly-typed counter enforced with <see cref="CounterStrategy.Buffered"/> strategy and custom configuration.
    /// </summary>
    /// <typeparam name="TTag">The marker type representing the counter category.</typeparam>
    /// <param name="configure">The configuration delegate.</param>
    public void AddBufferedCounter<TTag>(Action<CounterConfiguration> configure) where TTag : notnull {
        Preca.ThrowIfNull(configure);
        AddCounter<TTag>(cfg => {
            cfg.Strategy = CounterStrategy.Buffered;
            configure(cfg);
        });
    }

    /// <summary>
    /// Registers a named counter enforced with <see cref="CounterStrategy.Buffered"/> strategy.
    /// </summary>
    /// <param name="name">The unique name of the counter.</param>
    public void AddBufferedCounter(string name) {
        AddCounter(name, CounterStrategy.Buffered);
    }

    /// <summary>
    /// Registers a named counter enforced with <see cref="CounterStrategy.Buffered"/> strategy and custom configuration.
    /// </summary>
    /// <param name="name">The unique name of the counter.</param>
    /// <param name="configure">The configuration delegate.</param>
    public void AddBufferedCounter(string name, Action<CounterConfiguration> configure) {
        Preca.ThrowIfNullOrWhiteSpace(name);
        Preca.ThrowIfNull(configure);
        AddCounter(name, cfg => {
            cfg.Strategy = CounterStrategy.Buffered;
            configure(cfg);
        });
    }

    /// <inheritdoc/>
    public DistributedCounterOptions DeepClone() {
        DistributedCounterOptions clone = new() {
            DefaultStrategy = this.DefaultStrategy,
            AutoFlushInterval = this.AutoFlushInterval,
            GlobalKeyPrefix = this.GlobalKeyPrefix
        };

        foreach(KeyValuePair<string, CounterConfiguration> entry in this.Registrations) {
            clone.Registrations[entry.Key] = entry.Value.DeepClone();
        }

        return clone;
    }

    /// <inheritdoc/>
    public DistributedCounterOptions Merge(DistributedCounterOptions? other) {
        DistributedCounterOptions merged = DeepClone();
        if(other is null) return merged;

        merged.DefaultStrategy = other.DefaultStrategy;
        merged.AutoFlushInterval = other.AutoFlushInterval;
        merged.GlobalKeyPrefix = other.GlobalKeyPrefix;

        foreach(KeyValuePair<string, CounterConfiguration> entry in other.Registrations) {
            merged.Registrations[entry.Key] = entry.Value.DeepClone();
        }

        return merged;
    }
}

/// <summary>
/// Represents counter-specific configuration settings without external DI dependencies.
/// </summary>
public sealed class CounterConfiguration : IDeepCloneable<CounterConfiguration>, IMergeable<CounterConfiguration> {

    /// <summary>
    /// Gets or sets the counter name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the synchronization strategy for this counter.
    /// </summary>
    public CounterStrategy Strategy { get; set; } = CounterStrategy.Buffered;

    /// <summary>
    /// Gets or sets the explicit storage type implementation for this counter.
    /// </summary>
    public Type? StorageType { get; set; }

    /// <summary>
    /// Gets or sets the keyed service identifier for resolving keyed storage.
    /// </summary>
    public object? StorageKey { get; set; }

    /// <summary>
    /// Gets or sets an untyped factory delegate for custom storage instantiation.
    /// </summary>
    public Func<IServiceProvider, ICounterStorage>? StorageFactory { get; set; }

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

    /// <summary>
    /// Configures this counter to use a specific storage implementation type.
    /// </summary>
    /// <typeparam name="TStorage">The type of counter storage.</typeparam>
    /// <returns>The configuration instance for fluent chaining.</returns>
    public CounterConfiguration UseStorage<TStorage>() where TStorage : ICounterStorage {
        this.StorageType = typeof(TStorage);
        this.StorageKey = null;
        this.StorageFactory = null;
        return this;
    }

    /// <summary>
    /// Configures this counter to resolve a keyed storage instance from dependency injection.
    /// </summary>
    /// <param name="serviceKey">The keyed service identifier.</param>
    /// <returns>The configuration instance for fluent chaining.</returns>
    public CounterConfiguration UseKeyedStorage(object serviceKey) {
        Preca.ThrowIfNull(serviceKey);
        this.StorageKey = serviceKey;
        this.StorageType = null;
        this.StorageFactory = null;
        return this;
    }

    /// <summary>
    /// Configures this counter to use a custom storage factory delegate.
    /// </summary>
    /// <param name="factory">The factory delegate.</param>
    /// <returns>The configuration instance for fluent chaining.</returns>
    public CounterConfiguration UseStorage(Func<IServiceProvider, ICounterStorage> factory) {
        Preca.ThrowIfNull(factory);
        this.StorageFactory = factory;
        this.StorageType = null;
        this.StorageKey = null;
        return this;
    }

    /// <inheritdoc/>
    public CounterConfiguration DeepClone() {
        return new CounterConfiguration(this.Name, this.Strategy) {
            StorageType = this.StorageType,
            StorageKey = this.StorageKey,
            StorageFactory = this.StorageFactory
        };
    }

    /// <inheritdoc/>
    public CounterConfiguration Merge(CounterConfiguration? other) {
        if(other is null) return DeepClone();
        return new CounterConfiguration(
            string.IsNullOrWhiteSpace(other.Name) ? this.Name : other.Name,
            other.Strategy
        ) {
            StorageType = other.StorageType ?? this.StorageType,
            StorageKey = other.StorageKey ?? this.StorageKey,
            StorageFactory = other.StorageFactory ?? this.StorageFactory
        };
    }
}