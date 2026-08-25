using Wiaoj.DistributedCounter.Internal;
using Wiaoj.DistributedCounter.Testing;

namespace Wiaoj.DistributedCounter.Tests.Unit.Internal;

[Trait("Category", "Unit")]
[Trait("Component", "Internal")]
[Trait("Feature", "ImmediateCounter")]
public sealed class ImmediateDistributedCounterTests {

    [Fact]
    public async Task AllOperations_DirectlyDelegateToStorageWithoutBuffering() {
        // Arrange
        FakeCounterStorage storage = new();
        CounterKey key = "immediate:direct";
        ImmediateDistributedCounter counter = new(key, storage);
        CancellationToken ct = TestContext.Current.CancellationToken;

        // Act 1: Increment
        CounterValue incResult = await counter.IncrementAsync(10, CounterExpiry.Infinite, ct);
        Assert.Equal(10, incResult.Value);
        Assert.Equal(1, storage.AtomicIncrementCallCount);

        // Act 2: Decrement
        CounterValue decResult = await counter.DecrementAsync(3, CounterExpiry.Infinite, ct);
        Assert.Equal(7, decResult.Value);
        Assert.Equal(2, storage.AtomicIncrementCallCount);

        // Act 3: TryIncrement
        CounterLimitResult limitResult = await counter.TryIncrementAsync(3, limit: 20, CounterExpiry.Infinite, ct);
        Assert.True(limitResult.IsAllowed);
        Assert.Equal(10, limitResult.CurrentValue);

        // Act 4: Set, Get & Reset
        await counter.SetAsync(50, CounterExpiry.Infinite, ct);
        Assert.Equal(50, (await counter.GetValueAsync(ct)).Value);

        await counter.ResetAsync(ct);
        Assert.Equal(1, storage.DeleteCallCount);
        Assert.Equal(0, (await counter.GetValueAsync(ct)).Value);
    }
}