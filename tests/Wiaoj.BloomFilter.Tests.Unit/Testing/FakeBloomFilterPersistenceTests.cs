using Microsoft.Extensions.DependencyInjection;
using Wiaoj.BloomFilter.Testing;

namespace Wiaoj.BloomFilter.Tests.Unit.Testing;

public class FakeBloomFilterPersistenceTests {
    private sealed record TestBlacklistTag;

    [Fact]
    public void Should_ResolveAsIPersistentBloomFilter_When_RegisteredViaAddFakeBloomFilter() {
        // Arrange
        ServiceCollection services = new();
        const string filterName = "url-blacklist";
        services.AddFakeBloomFilter(filterName);
        IServiceProvider sp = services.BuildServiceProvider();

        // Act
        IPersistentBloomFilter keyedPersistent = sp.GetRequiredKeyedService<IPersistentBloomFilter>(filterName);
        IBloomFilter keyedFilter = sp.GetRequiredKeyedService<IBloomFilter>(filterName);
        IPersistentBloomFilter directPersistent = sp.GetRequiredService<IPersistentBloomFilter>();
        IBloomFilter directFilter = sp.GetRequiredService<IBloomFilter>();

        // Assert
        Assert.NotNull(keyedPersistent);
        Assert.NotNull(keyedFilter);
        Assert.NotNull(directPersistent);
        Assert.NotNull(directFilter);
        Assert.Same(keyedPersistent, keyedFilter);
    }

    [Fact]
    public void Should_ResolveAsIPersistentBloomFilter_When_RegisteredViaTypedAddFakeBloomFilter() {
        // Arrange
        ServiceCollection services = new();
        services.AddFakeBloomFilter<TestBlacklistTag>();
        IServiceProvider sp = services.BuildServiceProvider();

        // Act
        IBloomFilter<TestBlacklistTag> typedFilter = sp.GetRequiredService<IBloomFilter<TestBlacklistTag>>();
        IPersistentBloomFilter directPersistent = sp.GetRequiredService<IPersistentBloomFilter>();
        IPersistentBloomFilter keyedPersistent = sp.GetRequiredKeyedService<IPersistentBloomFilter>(nameof(TestBlacklistTag));

        // Assert
        Assert.NotNull(typedFilter);
        Assert.NotNull(directPersistent);
        Assert.NotNull(keyedPersistent);
        Assert.Same(typedFilter, directPersistent);
    }

    [Fact]
    public async Task Should_TrackDirtyStateAndSaveReloadCounts_Correctly() {
        // Arrange
        FakeBloomFilter filter = new("test-filter");
        Assert.False(filter.IsDirty);
        Assert.Equal(0, filter.SaveCount);
        Assert.Equal(0, filter.ReloadCount);

        // Act 1: Add item marks dirty
        filter.Add("new-item");
        Assert.True(filter.IsDirty);

        // Act 2: Save resets dirty and increments SaveCount
        await filter.SaveAsync(TestContext.Current.CancellationToken);
        Assert.False(filter.IsDirty);
        Assert.Equal(1, filter.SaveCount);

        // Act 3: Add via byte span marks dirty
        filter.Add("second-item"u8);
        Assert.True(filter.IsDirty);

        // Act 4: Reload resets dirty and increments ReloadCount
        await filter.ReloadAsync(TestContext.Current.CancellationToken);
        Assert.False(filter.IsDirty);
        Assert.Equal(1, filter.ReloadCount);
    }
}
