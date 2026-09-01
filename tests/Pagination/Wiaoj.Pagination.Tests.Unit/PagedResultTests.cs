using System.Text.Json;
using Wiaoj.Preconditions.Exceptions;
using Wiaoj.Primitives.Collections;

namespace Wiaoj.Pagination.Tests.Unit;

[Trait("Category", "Unit")]
[Trait("Subsystem", "OffsetPagination")]
public sealed class PagedResultTests {

    public sealed class Constructor {
        [Fact]
        public void Should_Initialize_With_Items_And_Metadata() {
            // Arrange
            EquatableArray<string> items = new[] { "item1", "item2", "item3" };
            PageMetadata metadata = new(totalCount: 3, page: 1, size: 10);

            // Act
            PagedResult<string> sut = new(items, metadata);

            // Assert
            Assert.Equal(3, sut.Count);
            Assert.False(sut.IsEmpty);
            Assert.Equal(items, sut.Items);
            Assert.Equal(metadata, sut.Metadata);
        }

        [Fact]
        public void Should_Deconstruct_Accurately() {
            // Arrange
            EquatableArray<int> items = new[] { 10, 20, 30 };
            PageMetadata metadata = new(totalCount: 3, page: 1, size: 10);
            PagedResult<int> sut = new(items, metadata);

            // Act
            (EquatableArray<int> deconstructedItems, PageMetadata deconstructedMetadata) = sut;

            // Assert
            Assert.Equal(items, deconstructedItems);
            Assert.Equal(metadata, deconstructedMetadata);
        }
    }

    public sealed class EmptyProperty {
        [Fact]
        public void Should_Represent_Empty_State() {
            // Arrange & Act
            PagedResult<int> sut = PagedResult<int>.Empty;

            // Assert
            Assert.Equal(0, sut.Count);
            Assert.True(sut.IsEmpty);
            Assert.Empty(sut.Items);
            Assert.True(sut.Metadata.IsEmpty);
        }

        [Fact]
        public void Should_Handle_Default_State_Safely() {
            // Arrange & Act
            PagedResult<string> sut = default;

            // Assert
            Assert.True(sut.IsEmpty);
            Assert.Equal(0, sut.Count);
            Assert.True(sut.AsSpan().IsEmpty);
            Assert.True(sut.Metadata.IsEmpty);
        }
    }

    public sealed class AsSpanMethod {
        [Fact]
        public void Should_Expose_Items_As_ReadOnlySpan() {
            // Arrange
            EquatableArray<int> items = new[] { 1, 2, 3 };
            var metadata = new PageMetadata(totalCount: 3, page: 1, size: 10);
            var sut = new PagedResult<int>(items, metadata);

            // Act
            ReadOnlySpan<int> span = sut.AsSpan();

            // Assert
            Assert.Equal(3, span.Length);
            Assert.Equal(1, span[0]);
            Assert.Equal(2, span[1]);
            Assert.Equal(3, span[2]);
        }
    }

    public sealed class SelectMethod {
        [Fact]
        public void Should_Project_Items_While_Preserving_Metadata() {
            // Arrange
            EquatableArray<int> numbers = new[] { 1, 2, 3 };
            PageMetadata metadata = new(totalCount: 3, page: 1, size: 10);
            PagedResult<int> sut = new(numbers, metadata);

            // Act
            PagedResult<string> mapped = sut.Select(x => $"Number: {x}");

            // Assert
            Assert.Equal(3, mapped.Count);
            Assert.Equal(metadata, mapped.Metadata);
            Assert.Equal("Number: 1", mapped.Items[0]);
            Assert.Equal("Number: 2", mapped.Items[1]);
            Assert.Equal("Number: 3", mapped.Items[2]);
        }

        [Fact]
        public void Should_Return_Empty_When_Source_Is_Empty() {
            // Arrange
            PagedResult<int> sut = PagedResult<int>.Empty;

            // Act
            PagedResult<string> projected = sut.Select(x => x.ToString());

            // Assert
            Assert.True(projected.IsEmpty);
            Assert.Equal(0, projected.Count);
        }

        [Fact]
        public void Should_Throw_When_Selector_Is_Null() {
            // Arrange
            EquatableArray<int> items = new[] { 1 };
            var sut = new PagedResult<int>(items, new PageMetadata(1, 1, 10));

            // Act & Assert
            Assert.Throws<PrecaArgumentNullException>(() => sut.Select<string>(null!));
        }
    }

    public sealed class EqualityOperators {
        [Fact]
        public void Should_Be_Equal_When_Both_Items_And_Metadata_Match() {
            // Arrange
            EquatableArray<string> items1 = new[] { "apple", "banana" };
            EquatableArray<string> items2 = new[] { "apple", "banana" };
            PageMetadata metadata = new(totalCount: 2, page: 1, size: 10);

            PagedResult<string> result1 = new(items1, metadata);
            PagedResult<string> result2 = new(items2, metadata);

            // Act & Assert
            Assert.True(result1 == result2);
            Assert.True(result1.Equals(result2));
            Assert.Equal(result1.GetHashCode(), result2.GetHashCode());
        }

        [Fact]
        public void Should_Not_Be_Equal_When_Items_Differ() {
            // Arrange
            EquatableArray<string> items1 = new[] { "apple", "banana" };
            EquatableArray<string> items2 = new[] { "apple", "orange" };
            PageMetadata metadata = new(totalCount: 2, page: 1, size: 10);

            PagedResult<string> result1 = new(items1, metadata);
            PagedResult<string> result2 = new(items2, metadata);

            // Act & Assert
            Assert.True(result1 != result2);
            Assert.False(result1.Equals(result2));
        }
    }

    public sealed class JsonSerialization {
        [Fact]
        public void Should_Serialize_And_Deserialize_Accurately() {
            // Arrange
            EquatableArray<string> items = new[] { "alpha", "beta", "gamma" };
            var metadata = new PageMetadata(totalCount: 3, page: 1, size: 10);
            var original = new PagedResult<string>(items, metadata);

            // Act
            string json = JsonSerializer.Serialize(original);
            PagedResult<string> deserialized = JsonSerializer.Deserialize<PagedResult<string>>(json);

            // Assert
            Assert.Equal(original, deserialized);
            Assert.Equal(original.Count, deserialized.Count);
            Assert.Equal(original.Items, deserialized.Items);
            Assert.Equal(original.Metadata, deserialized.Metadata);
        }
    }
}