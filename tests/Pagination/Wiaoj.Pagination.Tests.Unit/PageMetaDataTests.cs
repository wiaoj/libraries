using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

namespace Wiaoj.Pagination.Tests.Unit;

[Trait("Category", "Unit")]
[Trait("Subsystem", "OffsetPagination")]
public sealed class PageMetadataTests {

    public sealed class Constructor {
        [Theory]
        [InlineData(-100, 0)]
        [InlineData(0, 0)]
        [InlineData(500, 500)]
        public void Should_Sanitize_TotalCount(int inputTotalCount, int expectedTotalCount) {
            // Arrange & Act
            PageMetadata sut = new(totalCount: inputTotalCount, pageNumber: 1, pageSize: 20);

            // Assert
            Assert.Equal(expectedTotalCount, sut.TotalCount);
        }

        [Theory]
        [InlineData(-1, 1)]
        [InlineData(0, 1)]
        [InlineData(1, 1)]
        [InlineData(25, 25)]
        public void Should_Sanitize_PageNumber(int inputPageNumber, int expectedPageNumber) {
            // Arrange & Act
            PageMetadata sut = new(totalCount: 100, pageNumber: inputPageNumber, pageSize: 20);

            // Assert
            Assert.Equal(expectedPageNumber, sut.PageNumber);
        }

        [Theory]
        [InlineData(-10, 1)]
        [InlineData(0, 1)]
        [InlineData(50, 50)]
        public void Should_Sanitize_PageSize(int inputPageSize, int expectedPageSize) {
            // Arrange & Act
            PageMetadata sut = new(totalCount: 100, pageNumber: 1, pageSize: inputPageSize);

            // Assert
            Assert.Equal(expectedPageSize, sut.PageSize);
        }

        [Fact]
        public void Should_Deconstruct_Accurately() {
            // Arrange
            PageMetadata sut = new(totalCount: 95, pageNumber: 2, pageSize: 10);

            // Act
            (long totalCount, int pageNumber, int pageSize, long totalPages) = sut;

            // Assert
            Assert.Equal(95, totalCount);
            Assert.Equal(2, pageNumber);
            Assert.Equal(10, pageSize);
            Assert.Equal(10, totalPages);
        }

        [Fact]
        public void Should_Produce_Zero_Heap_Allocations() {
            // Arrange
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            long beforeAllocated = GC.GetAllocatedBytesForCurrentThread();

            // Act
            ExecuteInstantiation();

            long afterAllocated = GC.GetAllocatedBytesForCurrentThread();

            // Assert
            Assert.Equal(0, afterAllocated - beforeAllocated);
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        private static PageMetadata ExecuteInstantiation() {
            return new PageMetadata(10_000, 3, 100);
        }
    }

    public sealed class EmptyProperty {
        [Fact]
        public void Should_Represent_Default_Empty_State() {
            // Arrange & Act
            PageMetadata sut = PageMetadata.Empty;

            // Assert
            Assert.True(sut.IsEmpty);
            Assert.Equal(0, sut.TotalCount);
            Assert.Equal(0, sut.PageNumber);
            Assert.Equal(0, sut.PageSize);
            Assert.Equal(0, sut.TotalPages);
            Assert.False(sut.HasPrevious);
            Assert.False(sut.HasNext);
            Assert.Equal("Page 0 of 0 (Total: 0)", sut.ToString());
        }
    }

    public sealed class TotalPagesProperty {
        [Theory]
        [InlineData(0, 10, 0)]
        [InlineData(1, 10, 1)]
        [InlineData(10, 10, 1)]
        [InlineData(11, 10, 2)]
        [InlineData(99, 10, 10)]
        [InlineData(100, 10, 10)]
        [InlineData(101, 10, 11)]
        public void Should_Calculate_Accurately(int totalCount, int pageSize, int expectedTotalPages) {
            // Arrange & Act
            PageMetadata sut = new(totalCount: totalCount, pageNumber: 1, pageSize: pageSize);

            // Assert
            Assert.Equal(expectedTotalPages, sut.TotalPages);
        }

        [Fact]
        public void Should_Not_Overflow_When_TotalCount_Is_Near_MaxValue() {
            // Arrange
            PageMetadata sut = new(totalCount: long.MaxValue, pageNumber: 1, pageSize: 10);

            // Act
            long totalPages = sut.TotalPages;

            // Assert
            Assert.True(totalPages > 0);
            Assert.Equal((long.MaxValue / 10) + 1, totalPages);
        }
    }

    public sealed class NavigationFlagsProperties {
        [Theory]
        [InlineData(0, 1, 10, false, false)]
        [InlineData(10, 1, 10, false, false)]
        [InlineData(100, 1, 10, false, true)]
        [InlineData(100, 5, 10, true, true)]
        [InlineData(100, 10, 10, true, false)]
        [InlineData(100, 11, 10, true, false)]
        public void Should_Evaluate_HasPrevious_And_HasNext(
            int totalCount,
            int pageNumber,
            int pageSize,
            bool expectedHasPrevious,
            bool expectedHasNext) {

            // Arrange & Act
            PageMetadata sut = new(totalCount, pageNumber, pageSize);

            // Assert
            Assert.Equal(expectedHasPrevious, sut.HasPrevious);
            Assert.Equal(expectedHasNext, sut.HasNext);
        }
    }

    public sealed class ToStringMethod {
        [Fact]
        public void Should_Format_Standard_Representation() {
            // Arrange
            PageMetadata sut = new(totalCount: 150, pageNumber: 2, pageSize: 50);

            // Act
            string result = sut.ToString();

            // Assert
            Assert.Equal("Page 2 of 3 (Total: 150)", result);
        }
    }

    public sealed class TryFormatMethod {
        [Fact]
        public void Should_Format_To_Char_Span() {
            // Arrange
            PageMetadata sut = new(totalCount: 200, pageNumber: 3, pageSize: 20);
            Span<char> destination = stackalloc char[64];

            // Act
            bool success = sut.TryFormat(destination, out int charsWritten);

            // Assert
            Assert.True(success);
            Assert.Equal("Page 3 of 10 (Total: 200)", destination[..charsWritten].ToString());
        }

        [Fact]
        public void Should_Format_To_Utf8_Span() {
            // Arrange
            PageMetadata sut = new(totalCount: 300, pageNumber: 1, pageSize: 50);
            Span<byte> utf8Destination = stackalloc byte[64];

            // Act
            bool success = sut.TryFormat(utf8Destination, out int bytesWritten);

            // Assert
            Assert.True(success);
            string decoded = Encoding.UTF8.GetString(utf8Destination[..bytesWritten]);
            Assert.Equal("Page 1 of 6 (Total: 300)", decoded);
        }

        [Fact]
        public void Should_Return_False_When_Buffer_Is_Too_Small() {
            // Arrange
            PageMetadata sut = new(totalCount: 200, pageNumber: 3, pageSize: 20);
            Span<char> smallDestination = stackalloc char[4];

            // Act
            bool success = sut.TryFormat(smallDestination, out int charsWritten);

            // Assert
            Assert.False(success);
            Assert.Equal(0, charsWritten);
        }

        [Fact]
        public void Should_Return_False_When_Utf8_Buffer_Is_Too_Small() {
            // Arrange
            PageMetadata sut = new(totalCount: 200, pageNumber: 3, pageSize: 20);
            Span<byte> smallDestination = stackalloc byte[4];

            // Act
            bool success = sut.TryFormat(smallDestination, out int bytesWritten);

            // Assert
            Assert.False(success);
            Assert.Equal(0, bytesWritten);
        }
    }

    public sealed class EqualityOperators {
        [Fact]
        public void Should_Be_Equal_When_Properties_Match() {
            // Arrange
            var meta1 = new PageMetadata(100, 2, 10);
            var meta2 = new PageMetadata(100, 2, 10);

            // Act & Assert
            Assert.True(meta1 == meta2);
            Assert.True(meta1.Equals(meta2));
            Assert.Equal(meta1.GetHashCode(), meta2.GetHashCode());
        }

        [Fact]
        public void Should_Not_Be_Equal_When_Properties_Differ() {
            // Arrange
            var meta1 = new PageMetadata(100, 2, 10);
            var meta2 = new PageMetadata(100, 3, 10);

            // Act & Assert
            Assert.True(meta1 != meta2);
            Assert.False(meta1.Equals(meta2));
        }
    }

    public sealed class JsonSerialization {
        [Fact]
        public void Should_Serialize_And_Deserialize_Accurately() {
            // Arrange
            var original = new PageMetadata(totalCount: 250, pageNumber: 3, pageSize: 25);

            // Act
            string json = JsonSerializer.Serialize(original);
            PageMetadata deserialized = JsonSerializer.Deserialize<PageMetadata>(json);

            // Assert
            Assert.Equal(original, deserialized);
            Assert.Equal(original.TotalCount, deserialized.TotalCount);
            Assert.Equal(original.PageNumber, deserialized.PageNumber);
            Assert.Equal(original.PageSize, deserialized.PageSize);
            Assert.Equal(original.TotalPages, deserialized.TotalPages);
            Assert.Equal(original.HasPrevious, deserialized.HasPrevious);
            Assert.Equal(original.HasNext, deserialized.HasNext);
        }
    }
}