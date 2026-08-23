using System.Collections.Concurrent;
using Wiaoj.DistributedCounter;

namespace Wiaoj.RateLimiting.Tests.Unit.Fakes;

/// <summary>
/// A minimal <see cref="IDistributedCounterFactory"/> that hands out <see cref="FakeDistributedCounter"/>
/// instances backed by a single shared <see cref="FakeCounterStorage"/> — no key-builder prefixing,
/// no strategy selection, no buffering. Enough to test <see cref="FixedWindowRateLimiter"/> and
/// <see cref="SlidingWindowRateLimiter"/>.
/// </summary>
public sealed class FakeDistributedCounterFactory : IDistributedCounterFactory {
    private readonly FakeCounterStorage _storage;
    private readonly ConcurrentDictionary<string, IDistributedCounter> _counters = new(StringComparer.Ordinal);

    public FakeDistributedCounterFactory(FakeCounterStorage storage) {
        this._storage = storage;
    }

    public IDistributedCounter Create(string name) {
        return this._counters.GetOrAdd(name, n => new FakeDistributedCounter(CounterKey.Parse(n), this._storage));
    }

    public IDistributedCounter Create<TTag>() where TTag : notnull {
        return Create(typeof(TTag).Name);
    }

    public IDistributedCounter Create<TKey>(string name, TKey key) where TKey : notnull {
        return Create($"{name}:{key}");
    }

    public IDistributedCounter Create<TTag, TKey>(TKey key) where TTag : notnull where TKey : notnull {
        return Create($"{typeof(TTag).Name}:{key}");
    }
}