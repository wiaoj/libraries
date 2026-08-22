namespace Wiaoj.BloomFilter.Testing;

/// <summary>
/// Strongly-typed test double for <see cref="IBloomFilter{TTag}"/>.
/// </summary>
/// <typeparam name="TTag">The marker type for the filter.</typeparam>
public sealed class FakeBloomFilter<TTag> : FakeBloomFilter, IBloomFilter<TTag> where TTag : notnull {
    /// <summary>
    /// Initializes a new instance of <see cref="FakeBloomFilter{TTag}"/> with the type name as filter name.
    /// </summary>
    public FakeBloomFilter() : base(typeof(TTag).Name) {
    }

    /// <summary>
    /// Initializes a new instance of <see cref="FakeBloomFilter{TTag}"/> with a custom configuration.
    /// </summary>
    public FakeBloomFilter(BloomFilterConfiguration configuration) : base(configuration) {
    }
}