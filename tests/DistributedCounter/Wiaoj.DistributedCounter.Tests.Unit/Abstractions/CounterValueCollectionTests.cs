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

            CounterValueCollection collection = new(data);

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
            CounterValueCollection collection = new(null!);

            // Act & Assert
            Assert.Equal(0, collection.Count);
            Assert.False(collection.ContainsKey("any_key"));
            Assert.Equal(CounterValue.Zero, collection["any_key"]);
            Assert.False(collection.TryGetValue("any_key", out _));
            Assert.Empty(collection);
        }
    }
}