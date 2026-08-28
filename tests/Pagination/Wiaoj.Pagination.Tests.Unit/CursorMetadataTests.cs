using System.Text.Json;

namespace Wiaoj.Pagination.Tests.Unit;

[Trait("Category", "Unit")]
[Trait("Subsystem", "KeysetPagination")]
public sealed class CursorMetadataTests {

    public sealed class Constructor {
        [Fact]
        public void Should_Initialize_With_Boundaries() {
            // Arrange
            CursorToken start = CursorToken.FromUtf8("start_1");
            CursorToken end = CursorToken.FromUtf8("end_10");

            // Act
            CursorMetadata sut = new(start, end, hasPrevious: true, hasNext: true);

            // Assert
            Assert.Equal(start, sut.StartCursor);
            Assert.Equal(end, sut.EndCursor);
            Assert.True(sut.HasPrevious);
            Assert.True(sut.HasNext);
            Assert.False(sut.IsEmpty);
        }

        [Fact]
        public void Should_Handle_Single_Item_Window() {
            // Arrange
            CursorToken singleCursor = CursorToken.FromUtf8("unique_record_id");

            // Act
            CursorMetadata sut = new(singleCursor, singleCursor, hasPrevious: true, hasNext: true);

            // Assert
            Assert.Equal(sut.StartCursor, sut.EndCursor);
            Assert.False(sut.IsEmpty);
        }

        [Fact]
        public void Should_Deconstruct_Accurately() {
            // Arrange
            CursorToken start = CursorToken.FromUtf8("c_start");
            CursorToken end = CursorToken.FromUtf8("c_end");
            CursorMetadata sut = new(start, end, hasPrevious: false, hasNext: true);

            // Act
            (CursorToken startCursor, CursorToken endCursor, bool hasPrev, bool hasNext) = sut;

            // Assert
            Assert.Equal(start, startCursor);
            Assert.Equal(end, endCursor);
            Assert.False(hasPrev);
            Assert.True(hasNext);
        }
    }

    public sealed class EmptyProperty {
        [Fact]
        public void Should_Represent_Default_State() {
            // Arrange & Act
            CursorMetadata sut = CursorMetadata.Empty;

            // Assert
            Assert.True(sut.IsEmpty);
            Assert.True(sut.StartCursor.IsEmpty);
            Assert.True(sut.EndCursor.IsEmpty);
            Assert.False(sut.HasPrevious);
            Assert.False(sut.HasNext);
        }
    }

    public sealed class TryFormatMethod {
        [Fact]
        public void Should_Format_To_Char_Span() {
            // Arrange
            CursorMetadata sut = new(CursorToken.FromUtf8("start"), CursorToken.FromUtf8("end"), true, true);
            Span<char> destination = stackalloc char[128];

            // Act
            bool success = sut.TryFormat(destination, out int charsWritten);

            // Assert
            Assert.True(success);
            Assert.Equal(sut.ToString(), destination[..charsWritten].ToString());
        }

        [Fact]
        public void Should_Return_False_When_Buffer_Is_Too_Small() {
            // Arrange
            CursorMetadata sut = new(CursorToken.FromUtf8("start"), CursorToken.FromUtf8("end"), true, true);
            Span<char> smallBuffer = stackalloc char[4];

            // Act
            bool success = sut.TryFormat(smallBuffer, out int charsWritten);

            // Assert
            Assert.False(success);
            Assert.Equal(0, charsWritten);
        }
    }

    public sealed class EqualityOperators {
        [Fact]
        public void Should_Be_Equal_When_All_Boundaries_Match() {
            // Arrange
            CursorMetadata meta1 = new(CursorToken.FromUtf8("c1"), CursorToken.FromUtf8("c2"), true, false);
            CursorMetadata meta2 = new(CursorToken.FromUtf8("c1"), CursorToken.FromUtf8("c2"), true, false);

            // Act & Assert
            Assert.True(meta1 == meta2);
            Assert.True(meta1.Equals(meta2));
            Assert.Equal(meta1.GetHashCode(), meta2.GetHashCode());
        }

        [Fact]
        public void Should_Not_Be_Equal_When_Flags_Differ() {
            // Arrange
            CursorMetadata meta1 = new(CursorToken.FromUtf8("c1"), CursorToken.FromUtf8("c2"), true, false);
            CursorMetadata meta2 = new(CursorToken.FromUtf8("c1"), CursorToken.FromUtf8("c2"), true, true);

            // Act & Assert
            Assert.True(meta1 != meta2);
            Assert.False(meta1.Equals(meta2));
        }
    }

    public sealed class JsonSerialization {
        [Fact]
        public void Should_Serialize_And_Deserialize_Accurately() {
            // Arrange
            CursorToken start = CursorToken.FromUtf8("cursor_001");
            CursorToken end = CursorToken.FromUtf8("cursor_020");
            CursorMetadata original = new(start, end, hasPrevious: true, hasNext: false);

            // Act
            string json = JsonSerializer.Serialize(original);
            CursorMetadata deserialized = JsonSerializer.Deserialize<CursorMetadata>(json);

            // Assert
            Assert.Equal(original, deserialized);
            Assert.Equal(original.StartCursor, deserialized.StartCursor);
            Assert.Equal(original.EndCursor, deserialized.EndCursor);
            Assert.Equal(original.HasPrevious, deserialized.HasPrevious);
            Assert.Equal(original.HasNext, deserialized.HasNext);
        }
    }
}