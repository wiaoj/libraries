using System.Text.Json;
using Wiaoj.Preconditions.Exceptions;
using Wiaoj.Primitives.Collections;
using Xunit;

namespace Wiaoj.Pagination.Tests.Unit;

[Trait("Category", "Unit")]
[Trait("Subsystem", "KeysetPagination")]
public sealed class CursorResultTests {

    public sealed class Constructor {
        [Fact]
        public void Should_Initialize_With_Items_And_Metadata() {
            // Arrange
            EquatableArray<string> items = new[] { "A", "B", "C" };
            CursorMetadata metadata = new(CursorToken.FromUtf8("c1"), CursorToken.FromUtf8("c3"), false, true);

            // Act
            CursorResult<string> sut = new(items, metadata);

            // Assert
            Assert.Equal(3, sut.Count);
            Assert.False(sut.IsEmpty);
            Assert.Equal(items, sut.Items);
            Assert.Equal(metadata, sut.Metadata);
        }

        [Fact]
        public void Should_Deconstruct_Accurately() {
            // Arrange
            EquatableArray<string> items = new[] { "item1" };
            CursorMetadata metadata = new(CursorToken.FromUtf8("c1"), CursorToken.FromUtf8("c1"), false, false);
            CursorResult<string> sut = new(items, metadata);

            // Act
            var (deconstructedItems, deconstructedMetadata) = sut;

            // Assert
            Assert.Equal(items, deconstructedItems);
            Assert.Equal(metadata, deconstructedMetadata);
        }
    }

    public sealed class EmptyProperty {
        [Fact]
        public void Should_Represent_Empty_State() {
            // Arrange & Act
            CursorResult<string> sut = CursorResult<string>.Empty;

            // Assert
            Assert.Equal(0, sut.Count);
            Assert.True(sut.IsEmpty);
            Assert.True(sut.Metadata.IsEmpty);
        }

        [Fact]
        public void Should_Handle_Default_State_Safely() {
            // Arrange & Act
            CursorResult<string> sut = default;

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
            EquatableArray<int> items = new[] { 10, 20 };
            CursorResult<int> sut = new(items, CursorMetadata.Empty);

            // Act
            ReadOnlySpan<int> span = sut.AsSpan();

            // Assert
            Assert.Equal(2, span.Length);
            Assert.Equal(10, span[0]);
            Assert.Equal(20, span[1]);
        }
    }

    public sealed class SelectMethod {
        [Fact]
        public void Should_Project_Items_While_Preserving_Metadata() {
            // Arrange
            EquatableArray<int> items = new[] { 100, 200 };
            CursorMetadata metadata = new(CursorToken.FromUtf8("c1"), CursorToken.FromUtf8("c2"), false, false);
            CursorResult<int> sut = new(items, metadata);

            // Act
            CursorResult<string> mapped = sut.Select(x => $"Value: {x}");

            // Assert
            Assert.Equal(2, mapped.Count);
            Assert.Equal("Value: 100", mapped.Items[0]);
            Assert.Equal("Value: 200", mapped.Items[1]);
            Assert.Equal(metadata, mapped.Metadata);
        }

        [Fact]
        public void Should_Return_Empty_When_Source_Is_Empty() {
            // Arrange
            CursorResult<int> sut = CursorResult<int>.Empty;

            // Act
            CursorResult<string> mapped = sut.Select(x => x.ToString());

            // Assert
            Assert.True(mapped.IsEmpty);
            Assert.Equal(0, mapped.Count);
        }

        [Fact]
        public void Should_Throw_When_Selector_Is_Null() {
            // Arrange
            EquatableArray<int> items = new[] { 10 };
            CursorResult<int> sut = new(items, CursorMetadata.Empty);

            // Act & Assert
            Assert.Throws<PrecaArgumentNullException>(() => sut.Select<string>(null!));
        }
    }

    public sealed class EqualityOperators {
        [Fact]
        public void Should_Be_Equal_When_Items_And_Metadata_Match() {
            // Arrange
            EquatableArray<int> items1 = new[] { 1, 2 };
            EquatableArray<int> items2 = new[] { 1, 2 };
            CursorMetadata metadata = new(CursorToken.FromUtf8("c1"), CursorToken.FromUtf8("c2"), false, false);

            CursorResult<int> res1 = new(items1, metadata);
            CursorResult<int> res2 = new(items2, metadata);

            // Act & Assert
            Assert.True(res1 == res2);
            Assert.True(res1.Equals(res2));
            Assert.Equal(res1.GetHashCode(), res2.GetHashCode());
        }
    }

    public sealed class JsonSerialization {
        [Fact]
        public void Should_Serialize_And_Deserialize_Generic_CursorResult() {
            // Arrange
            EquatableArray<int> items = new[] { 1, 2, 3 };
            CursorMetadata metadata = new(CursorToken.FromUtf8("start"), CursorToken.FromUtf8("end"), true, true);
            CursorResult<int> original = new(items, metadata);

            // Act
            string json = JsonSerializer.Serialize(original);
            CursorResult<int> deserialized = JsonSerializer.Deserialize<CursorResult<int>>(json);

            // Assert
            Assert.Equal(original, deserialized);
            Assert.Equal(original.Items, deserialized.Items);
            Assert.Equal(original.Metadata, deserialized.Metadata);
        }
    }
}