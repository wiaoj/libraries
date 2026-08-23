using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using Wiaoj.Preconditions;

namespace Wiaoj.DistributedCounter.Internal;

/// <summary>
/// Default factory implementation for creating, resolving, and tracking <see cref="IDistributedCounter"/> instances.
/// </summary>
internal sealed class DistributedCounterFactory : IDistributedCounterFactory, IBufferedCounterSource {
    private readonly ICounterStorage _storage;
    private readonly ICounterKeyBuilder _keyBuilder;
    private readonly DistributedCounterOptions _options;

    private readonly ConcurrentBag<BufferedDistributedCounter> _bufferedCounters = [];
    private readonly ConcurrentDictionary<string, IDistributedCounter> _counters = new(StringComparer.Ordinal);

    /// <summary>
    /// Initializes a new instance of the <see cref="DistributedCounterFactory"/> class.
    /// </summary>
    /// <param name="storage">The underlying storage provider.</param>
    /// <param name="keyBuilder">The counter key builder.</param>
    /// <param name="options">The distributed counter configuration options.</param>
    public DistributedCounterFactory(
        ICounterStorage storage,
        ICounterKeyBuilder keyBuilder,
        IOptions<DistributedCounterOptions> options) {
        Preca.ThrowIfNull(storage);
        Preca.ThrowIfNull(keyBuilder);
        Preca.ThrowIfNull(options);

        this._storage = storage;
        this._keyBuilder = keyBuilder;
        this._options = options.Value;
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
            CounterStrategy strategy = this._options.Registrations.TryGetValue(name, out CounterConfiguration? config)
                ? config.Strategy
                : this._options.DefaultStrategy;

            if(strategy == CounterStrategy.Immediate) {
                return new ImmediateDistributedCounter(key, this._storage);
            }

            BufferedDistributedCounter buffered = new(key, this._storage);
            this._bufferedCounters.Add(buffered);
            return buffered;
        });
    }
}