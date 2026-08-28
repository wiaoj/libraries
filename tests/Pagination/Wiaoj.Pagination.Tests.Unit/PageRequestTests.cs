using System.Runtime.CompilerServices;

namespace Wiaoj.Pagination.Tests.Unit;

[Trait("Category", "Unit")]
[Trait("Subsystem", "OffsetPagination")]
public sealed class PageRequestTests {

    public sealed class Constructor {
        [Theory]
        [InlineData(int.MinValue, 1)]
        [InlineData(-1, 1)]
        [InlineData(0, 1)]
        [InlineData(1, 1)]
        [InlineData(int.MaxValue, int.MaxValue)]
        public void Should_Clamp_PageNumber_Boundaries(int inputPageNumber, int expectedPageNumber) {
            // Arrange & Act
            PageRequest sut = new(inputPageNumber, 20);

            // Assert
            Assert.Equal(expectedPageNumber, sut.PageNumber);
        }

        [Theory]
        [InlineData(int.MinValue, PageRequest.DefaultPageSize)]
        [InlineData(-10, PageRequest.DefaultPageSize)]
        [InlineData(0, PageRequest.DefaultPageSize)]
        [InlineData(1, 1)]
        [InlineData(PageRequest.MaxPageSize, PageRequest.MaxPageSize)]
        [InlineData(PageRequest.MaxPageSize + 1, PageRequest.MaxPageSize)]
        [InlineData(int.MaxValue, PageRequest.MaxPageSize)]
        public void Should_Clamp_PageSize_Boundaries(int inputPageSize, int expectedPageSize) {
            // Arrange & Act
            PageRequest sut = new(1, inputPageSize);

            // Assert
            Assert.Equal(expectedPageSize, sut.PageSize);
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
        private static PageRequest ExecuteInstantiation() {
            return new PageRequest(int.MaxValue, PageRequest.MaxPageSize);
        }
    }

    public sealed class CalculateSkipMethod {
        [Theory]
        [InlineData(1, 20, 0)]
        [InlineData(2, 20, 20)]
        [InlineData(5, 50, 200)]
        public void Should_Calculate_Accurately_For_Normal_Inputs(int page, int size, int expectedSkip) {
            // Arrange
            PageRequest sut = new(page, size);

            // Act
            int skip = sut.CalculateSkip();

            // Assert
            Assert.Equal(expectedSkip, skip);
        }

        [Fact]
        public void Should_Return_Zero_For_Default_Struct() {
            // Arrange
            PageRequest sut = default;

            // Act
            int skip = sut.CalculateSkip();

            // Assert
            Assert.Equal(0, skip);
        }

        [Fact]
        public void Should_Prevent_Integer_Overflow_When_PageNumber_Is_Extreme() {
            // Arrange: (int.MaxValue - 1) * 100 exceeds int.MaxValue
            PageRequest sut = new(pageNumber: int.MaxValue, pageSize: 100);

            // Act
            int skip = sut.CalculateSkip();

            // Assert: Must cap at int.MaxValue without wrapping to negative
            Assert.Equal(int.MaxValue, skip);
        }
    }

    public sealed class ParseMethod {
        [Theory]
        [InlineData("2:50", 2, 50)]
        [InlineData("3,25", 3, 25)]
        [InlineData("5", 5, PageRequest.DefaultPageSize)]
        [InlineData("0:0", 1, PageRequest.DefaultPageSize)]
        [InlineData("-5:-20", 1, PageRequest.DefaultPageSize)]
        [InlineData("1:500", 1, PageRequest.MaxPageSize)]
        public void Should_Parse_And_Sanitize_Valid_Or_OutOfBound_Strings(string input, int expectedPage, int expectedSize) {
            // Act
            PageRequest result = PageRequest.Parse(input);

            // Assert
            Assert.Equal(expectedPage, result.PageNumber);
            Assert.Equal(expectedSize, result.PageSize);
        }

        [Fact]
        public void Should_Return_Default_Instance_When_Span_Is_Empty() {
            // Act
            bool success = PageRequest.TryParse(ReadOnlySpan<char>.Empty, out PageRequest result);

            // Assert
            Assert.True(success);
            Assert.Equal(PageRequest.Default, result);
        }

        [Theory]
        [InlineData(":")]
        [InlineData(",")]
        [InlineData("1:")]
        [InlineData(":20")]
        [InlineData("abc:def")]
        [InlineData("1:20:extra")]
        [InlineData("9999999999999999999999999:20")]
        [InlineData("1:9999999999999999999999999")]
        public void Should_Fail_Gracefully_On_Malformed_Or_Overflow_Strings(string malformed) {
            // Act & Assert
            Assert.False(PageRequest.TryParse(malformed, out _));
            Assert.Throws<FormatException>(() => PageRequest.Parse(malformed));
        }
    }

    public sealed class TryFormatMethod {
        [Fact]
        public void Should_Format_Default_Struct_Safely() {
            // Arrange
            PageRequest sut = default;
            Span<char> destination = stackalloc char[32];

            // Act
            bool success = sut.TryFormat(destination, out int charsWritten);

            // Assert
            Assert.True(success);
            Assert.Equal("PageNumber: 0, PageSize: 0", destination[..charsWritten].ToString());
        }

        [Fact]
        public void Should_Return_False_When_Buffer_Is_Insufficient() {
            // Arrange
            PageRequest sut = new(100, 50);
            Span<char> smallBuffer = stackalloc char[2];

            // Act
            bool success = sut.TryFormat(smallBuffer, out int charsWritten);

            // Assert
            Assert.False(success);
            Assert.Equal(0, charsWritten);
        }
    }
}