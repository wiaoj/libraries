namespace Wiaoj.BloomFilter.Tests.Unit.Abstractions;

public class AsyncBloomFilterContractTests {
    private sealed record TestUserTag;

    [Fact]
    public void IAsyncBloomFilter_Should_DefineRequiredMembers() {
        Type interfaceType = typeof(IAsyncBloomFilter);

        Assert.True(interfaceType.IsInterface);
        Assert.NotNull(interfaceType.GetProperty("Name"));
        Assert.NotNull(interfaceType.GetProperty("Configuration"));

        Assert.NotNull(interfaceType.GetMethod("AddAsync", [typeof(ReadOnlyMemory<byte>), typeof(CancellationToken)]));
        Assert.NotNull(interfaceType.GetMethod("AddAsync", [typeof(string), typeof(CancellationToken)]));
        Assert.NotNull(interfaceType.GetMethod("ContainsAsync", [typeof(ReadOnlyMemory<byte>), typeof(CancellationToken)]));
        Assert.NotNull(interfaceType.GetMethod("ContainsAsync", [typeof(string), typeof(CancellationToken)]));
        Assert.NotNull(interfaceType.GetMethod("GetPopCountAsync", [typeof(CancellationToken)]));
    }

    [Fact]
    public void IAsyncBloomFilterGeneric_Should_InheritFromIAsyncBloomFilter() {
        Type genericInterfaceType = typeof(IAsyncBloomFilter<TestUserTag>);

        Assert.True(genericInterfaceType.IsInterface);
        Assert.True(typeof(IAsyncBloomFilter).IsAssignableFrom(genericInterfaceType));
    }
}
