using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using Wiaoj.Preconditions;

namespace Wiaoj.DistributedCounter.Internal;

/// <summary>
/// Default factory implementation for creating, resolving, and tracking <see cref="IDistributedCounter"/> instances.
/// </summary>
internal sealed class DistributedCounterFactory : IDistributedCounterFactory, IBufferedCounterSource {
    private readonly ICounterStorage _defaultStorage;
    private readonly ICounterKeyBuilder _keyBuilder;
    private readonly DistributedCounterOptions _options;
    private readonly IServiceProvider? _serviceProvider;

    private readonly ConcurrentBag<BufferedDistributedCounter> _bufferedCounters = [];
    private readonly ConcurrentDictionary<string, IDistributedCounter> _counters = new(StringComparer.Ordinal);

    /// <summary>
    /// Initializes a new instance of the <see cref="DistributedCounterFactory"/> class with default storage.
    /// </summary>
    /// <param name="defaultStorage">The default underlying storage provider.</param>
    /// <param name="keyBuilder">The counter key builder.</param>
    /// <param name="options">The distributed counter configuration options.</param>
    public DistributedCounterFactory(
        ICounterStorage defaultStorage,
        ICounterKeyBuilder keyBuilder,
        IOptions<DistributedCounterOptions> options)
        : this(defaultStorage, keyBuilder, options, null) {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DistributedCounterFactory"/> class with service provider resolution.
    /// </summary>
    /// <param name="defaultStorage">The default underlying storage provider.</param>
    /// <param name="keyBuilder">The counter key builder.</param>
    /// <param name="options">The distributed counter configuration options.</param>
    /// <param name="serviceProvider">The service provider used for per-tag storage resolution.</param>
    public DistributedCounterFactory(
        ICounterStorage defaultStorage,
        ICounterKeyBuilder keyBuilder,
        IOptions<DistributedCounterOptions> options,
        IServiceProvider? serviceProvider) {
        Preca.ThrowIfNull(defaultStorage);
        Preca.ThrowIfNull(keyBuilder);
        Preca.ThrowIfNull(options);

        this._defaultStorage = defaultStorage;
        this._keyBuilder = keyBuilder;
        this._options = options.Value;
        this._serviceProvider = serviceProvider;
    }

    /// <inheritdoc/>
    public IDistributedCounter Create<TTag>() where TTag : notnull {
        string name = typeof(TTag).Name;
        CounterKey key = this._keyBuilder.Build<TTag>(name, this._options);
        return GetOrCreate(name, key);
    }

    /// <inheritdoc/>
    public IDistributedCounter Create(string name) {
        Preca.ThrowIfNullOrWhiteSpace(name);
        CounterKey key = this._keyBuilder.Build(name, this._options);
        return GetOrCreate(name, key);
    }

    /// <inheritdoc/>
    public IDistributedCounter Create<TKey>(string name, TKey key) where TKey : notnull {
        Preca.ThrowIfNullOrWhiteSpace(name);
        Preca.ThrowIfNull(key);
        CounterKey counterKey = this._keyBuilder.Build(name, key, this._options);
        return GetOrCreate(name, counterKey);
    }

    /// <inheritdoc/>
    public IDistributedCounter Create<TTag, TKey>(TKey key) where TTag : notnull where TKey : notnull {
        Preca.ThrowIfNull(key);
        string name = typeof(TTag).Name;
        CounterKey counterKey = this._keyBuilder.Build<TTag, TKey>(key, this._options);
        return GetOrCreate(name, counterKey);
    }

    IEnumerable<BufferedDistributedCounter> IBufferedCounterSource.GetBufferedCounters() {
        return this._bufferedCounters;
    }

    IEnumerable<IDistributedCounter> IBufferedCounterSource.GetAllTrackedCounters() {
        return this._counters.Values;
    }

    void IBufferedCounterSource.ClearCache() {
        this._counters.Clear();
        while(this._bufferedCounters.TryTake(out _)) { }
    }

    private IDistributedCounter GetOrCreate(string name, CounterKey key) {
        return this._counters.GetOrAdd(key.Value, _ => {
            bool hasConfig = this._options.Registrations.TryGetValue(name, out CounterConfiguration? config);
            CounterStrategy strategy = hasConfig && config is not null ? config.Strategy : this._options.DefaultStrategy;
            ICounterStorage storage = ResolveStorage(config);

            if(strategy == CounterStrategy.Immediate) {
                return new ImmediateDistributedCounter(key, storage);
            }

            BufferedDistributedCounter buffered = new(key, storage);
            this._bufferedCounters.Add(buffered);
            return buffered;
        });
    }

    private ICounterStorage ResolveStorage(CounterConfiguration? config) {
        if(config is null || this._serviceProvider is null) {
            return this._defaultStorage;
        }

        if(config.StorageFactory is not null) {
            return config.StorageFactory(this._serviceProvider);
        }

        if(config.StorageKey is not null) {
            return this._serviceProvider.GetRequiredKeyedService<ICounterStorage>(config.StorageKey);
        }

        if(config.StorageType is not null) {
            return (ICounterStorage)ActivatorUtilities.GetServiceOrCreateInstance(this._serviceProvider, config.StorageType);
        }

        return this._defaultStorage;
    }
}