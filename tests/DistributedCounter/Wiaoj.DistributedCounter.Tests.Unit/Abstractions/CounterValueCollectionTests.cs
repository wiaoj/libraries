using Wiaoj.DistributedCounter;
using Xunit;

namespace Wiaoj.DistributedCounter.Tests.Unit.Abstractions;

[Trait("Category", "Unit")]
[Trait("Component", "Abstractions")]
[Trait("Feature", "CounterValueCollection")]
public sealed class CounterValueCollectionTests {

    public sealed class TheDataReadOperations {

        [Fact]
        public void GivenValidData_IndexerAndTryGetValue_ReturnCorrectValues() {
            // Arrange
            Dictionary<string, CounterValue> data = new() {
                { "orders", new CounterValue(42) },
                { "users", new CounterValue(100) }
            };

            using CounterValueCollection collection = new(data, releaser: null);

            // Act & Assert
            Assert.Equal(2, collection.Count);
            Assert.True(collection.ContainsKey("orders"));
            Assert.Equal(42, collection["orders"].Value);

            Assert.True(collection.TryGetValue("users", out CounterValue userVal));
            Assert.Equal(100, userVal.Value);

            Assert.False(collection.ContainsKey("non_existing"));
            Assert.Equal(CounterValue.Zero, collection["non_existing"]);
            Assert.False(collection.TryGetValue("non_existing", out _));
        }

        [Fact]
        public void GivenNullData_BehavesGracefullyWithoutExceptions() {
            // Arrange
            using CounterValueCollection collection = new(null!, releaser: null);

            // Act & Assert
            Assert.Equal(0, collection.Count);
            Assert.False(collection.ContainsKey("any_key"));
            Assert.Equal(CounterValue.Zero, collection["any_key"]);
            Assert.False(collection.TryGetValue("any_key", out _));
            Assert.Empty(collection);
        }
    }

    public sealed class TheDisposalAndGuardBehavior {

        [Fact]
        public void Dispose_TriggersInnerReleaserExactlyOnce() {
            // Arrange
            TestReleaser mockReleaser = new();
            Dictionary<string, CounterValue> data = new() { { "k1", new CounterValue(10) } };
            CounterValueCollection collection = new(data, mockReleaser);

            // Act
            collection.Dispose();
            collection.Dispose(); // Multiple dispose calls

            // Assert
            Assert.Equal(1, mockReleaser.DisposeCount);
        }

        [Fact]
        public void AfterDisposal_AllReadOperationsReturnZeroOrEmpty() {
            // Arrange
            Dictionary<string, CounterValue> data = new() { { "k1", new CounterValue(10) } };
            CounterValueCollection collection = new(data, releaser: null);

            // Act
            collection.Dispose();

            // Assert
            Assert.Equal(0, collection.Count);
            Assert.False(collection.ContainsKey("k1"));
            Assert.Equal(CounterValue.Zero, collection["k1"]);
            Assert.False(collection.TryGetValue("k1", out _));
            Assert.Empty(collection);
        }

        [Fact]
        public void GivenStructCopies_DisposingOneCopy_DisposesAllCopies() {
            // Arrange
            TestReleaser mockReleaser = new();
            Dictionary<string, CounterValue> data = new() { { "k1", new CounterValue(10) } };

            CounterValueCollection original = new(data, mockReleaser);
            CounterValueCollection copy1 = original;
            CounterValueCollection copy2 = original;

            // Act
            copy1.Dispose(); // Dispose from a struct copy

            // Assert
            Assert.Equal(1, mockReleaser.DisposeCount);
            Assert.Equal(0, original.Count);
            Assert.Equal(0, copy2.Count);
            Assert.Equal(CounterValue.Zero, original["k1"]);
            Assert.Equal(CounterValue.Zero, copy2["k1"]);
        }

        [Fact]
        public async Task ConcurrentDispose_DisposesInnerReleaserExactlyOnce() {
            // Arrange
            TestReleaser mockReleaser = new();
            CounterValueCollection collection = new(new Dictionary<string, CounterValue>(), mockReleaser);

            // Act
            Task[] tasks = Enumerable.Range(0, 50)
                .Select(_ => Task.Run(() => {
                    CounterValueCollection copy = collection;
                    copy.Dispose();
                }))
                .ToArray();

            await Task.WhenAll(tasks);

            // Assert
            Assert.Equal(1, mockReleaser.DisposeCount);
        }
    }

    private sealed class TestReleaser : IDisposable {
        private int _disposeCount;
        public int DisposeCount => Volatile.Read(ref this._disposeCount);

        public void Dispose() {
            Interlocked.Increment(ref this._disposeCount);
        }
    }
}