namespace Wiaoj.BloomFilter.Internal;

internal sealed class TypedBloomFilterWrapper<TTag>(IBloomFilter inner) : IBloomFilter<TTag> where TTag : notnull {
    public string Name => inner.Name;
    public BloomFilterConfiguration Configuration => inner.Configuration;

    public bool Add(ReadOnlySpan<byte> item) {
        return inner.Add(item);
    }

    public bool Contains(ReadOnlySpan<byte> item) {
        return inner.Contains(item);
    }

    public bool Add(ReadOnlySpan<char> item) {
        return inner.Add(item);
    }

    public bool Contains(ReadOnlySpan<char> item) {
        return inner.Contains(item);
    }

    public long GetPopCount() {
        return inner.GetPopCount();
    }

    internal IBloomFilter InnerFilter => inner;
}