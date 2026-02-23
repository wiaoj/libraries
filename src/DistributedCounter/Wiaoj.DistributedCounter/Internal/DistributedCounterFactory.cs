using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace Wiaoj.DistributedCounter.Internal; 
public sealed class DistributedCounterFactory : IDistributedCounterFactory, IBufferedCounterSource {
    private readonly ICounterStorage _storage;
    private readonly ICounterKeyBuilder _keyBuilder;
    private readonly DistributedCounterOptions _options;

    // Oluşturulan Buffered sayaçları burada tutuyoruz (Flush için)
    private readonly ConcurrentBag<BufferedDistributedCounter> _bufferedCounters = [];

    // Cache created instances to avoid allocations (Singleton behavior per key)
    private readonly ConcurrentDictionary<string, IDistributedCounter> _counters = new();

    public DistributedCounterFactory(
        ICounterStorage storage, // Redis veya Memory buradan gelir
        ICounterKeyBuilder keyBuilder,
        IOptions<DistributedCounterOptions> options) {
        this._storage = storage;
        this._keyBuilder = keyBuilder;
        this._options = options.Value;
    }

    public IDistributedCounter Create<TTag>() where TTag : notnull {
        string name = typeof(TTag).Name; // Basit isim
        // KeyBuilder generic methodunu kullanarak Typed Key oluşturur
        CounterKey key = this._keyBuilder.Build<TTag>(name, this._options);
        return GetOrCreate(name, key);
    }

    public IDistributedCounter Create(string name) {
        CounterKey key = this._keyBuilder.Build(name, this._options);
        return GetOrCreate(name, key);
    }

    public IDistributedCounter Create<TKey>(string name, TKey key) where TKey : notnull {
        CounterKey counterKey = _keyBuilder.Build(name, key, _options);
        return GetOrCreate(name, counterKey);
    }

    public IDistributedCounter Create<TTag, TKey>(TKey key) where TTag : notnull where TKey : notnull {
        string name = typeof(TTag).Name;
        CounterKey counterKey = _keyBuilder.Build<TTag, TKey>(key, _options);
        return GetOrCreate(name, counterKey);
    }

    IEnumerable<BufferedDistributedCounter> IBufferedCounterSource.GetBufferedCounters() {
        return this._bufferedCounters;
    }

    IEnumerable<IDistributedCounter> IBufferedCounterSource.GetAllTrackedCounters() => _counters.Values;

    void IBufferedCounterSource.ClearCache() {
        _counters.Clear();
        while(_bufferedCounters.TryTake(out _)) { }
    }

    private IDistributedCounter GetOrCreate(string name, CounterKey key) {
        return this._counters.GetOrAdd(key.Value, _ => {
            // 1. Bu sayaç için özel bir strateji var mı? Yoksa varsayılanı al.
            CounterStrategy strategy = this._options.Registrations.TryGetValue(name, out CounterConfiguration? config)
                ? config.Strategy
                : this._options.DefaultStrategy;

            if(strategy == CounterStrategy.Immediate) {
                return new ImmediateDistributedCounter(key, this._storage);
            }
            else {
                var buffered = new BufferedDistributedCounter(key, this._storage);
                this._bufferedCounters.Add(buffered); // Background service için kaydet
                return buffered;
            }
        });
    }
}