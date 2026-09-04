using System.Collections.Concurrent;
using Wiaoj.Preconditions;

namespace Wiaoj.BloomFilter.Engine;

internal interface IBloomFilterRegistry {
    void Register(IPersistentBloomFilter filter);
    IEnumerable<IPersistentBloomFilter> GetAll();
}

internal sealed class BloomFilterRegistry : IBloomFilterRegistry, IDisposable {
    private readonly ConcurrentDictionary<FilterName, IPersistentBloomFilter> _filters = new();

    public void Register(IPersistentBloomFilter filter) {
        Preca.ThrowIfNull(filter);
        this._filters[filter.Name] = filter;
    }

    public IEnumerable<IPersistentBloomFilter> GetAll() => this._filters.Values;

    public void Dispose() {
        foreach(IPersistentBloomFilter filter in this._filters.Values) {
            if(filter is IDisposable disposable) {
                disposable.Dispose();
            }
        }
        this._filters.Clear();
    }
}