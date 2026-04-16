using System.Collections.Concurrent;

namespace Wiaoj.BloomFilter.Internal;

internal interface IBloomFilterRegistry {
    void Register(IPersistentBloomFilter filter);
    IEnumerable<IPersistentBloomFilter> GetAll();
}

internal sealed class BloomFilterRegistry : IBloomFilterRegistry, IDisposable {
    private readonly ConcurrentBag<IPersistentBloomFilter> _filters = [];

    public void Register(IPersistentBloomFilter filter) {
        this._filters.Add(filter);
    }

    public IEnumerable<IPersistentBloomFilter> GetAll() => this._filters;

    public void Dispose() {
        foreach(var filter in this._filters) {
            if(filter is IDisposable disposable) {
                disposable.Dispose();
            }
        }
    }
}