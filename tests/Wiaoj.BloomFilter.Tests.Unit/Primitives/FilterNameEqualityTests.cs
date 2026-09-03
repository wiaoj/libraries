using Wiaoj.BloomFilter.Internal;
using Xunit;

namespace Wiaoj.BloomFilter.Tests.Unit.Primitives;

public sealed class FilterNameEqualityTests {
    public sealed class EqualityMethod {
        [Fact]
        public void Should_ReturnTrue_When_ValuesAreIdentical() {
            // Arrange
            FilterName first = FilterName.Parse("same-name");
            FilterName second = FilterName.Parse("same-name");

            // Act & Assert
            Assert.Equal(first, second);
            Assert.True(first.Equals(second));
            Assert.Equal(first.GetHashCode(), second.GetHashCode());
        }

        [Fact]
        public void Should_ReturnFalse_When_ValuesDiffer() {
            // Arrange
            FilterName first = FilterName.Parse("name-a");
            FilterName second = FilterName.Parse("name-b");

            // Act & Assert
            Assert.NotEqual(first, second);
        }

        [Fact]
        public void Should_BeUsableAsDictionaryKey_ForFilterLookup() {
            // Arrange: FilterName is used as a dictionary/registry key throughout the library
            // (e.g. BloomFilterRegistry, BloomFilterService stats), so its equality contract matters.
            Dictionary<FilterName, int> counters = new() {
                [FilterName.Parse("key-one")] = 1,
                [FilterName.Parse("key-two")] = 2
            };

            // Act
            bool found = counters.TryGetValue(FilterName.Parse("key-one"), out int value);

            // Assert
            Assert.True(found);
            Assert.Equal(1, value);
        }
    }
}