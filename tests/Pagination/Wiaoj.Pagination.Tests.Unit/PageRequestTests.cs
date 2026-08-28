using System.Runtime.CompilerServices;
using System.Text;

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

        [Fact]
        public void Should_Parse_From_Utf8_Span() {
            // Arrange
            ReadOnlySpan<byte> utf8Input = "4:30"u8;

            // Act
            PageRequest result = PageRequest.Parse(utf8Input);

            // Assert
            Assert.Equal(4, result.PageNumber);
            Assert.Equal(30, result.PageSize);
        }

        [Fact]
        public void Should_TryParse_From_Utf8_Span() {
            // Arrange
            ReadOnlySpan<byte> utf8Input = "2:15"u8;

            // Act
            bool success = PageRequest.TryParse(utf8Input, out PageRequest result);

            // Assert
            Assert.True(success);
            Assert.Equal(2, result.PageNumber);
            Assert.Equal(15, result.PageSize);
        }

        [Fact]
        public void Should_Fail_To_Parse_Malformed_Utf8_Span() {
            // Arrange
            ReadOnlySpan<byte> malformedInput = "abc:def"u8;

            // Act & Assert
            Assert.False(PageRequest.TryParse(malformedInput, out _));
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
            Assert.Equal("0:0", destination[..charsWritten].ToString());
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

        [Fact]
        public void Should_Format_To_Utf8_Span() {
            // Arrange
            PageRequest sut = new(4, 30);
            Span<byte> destination = stackalloc byte[64];

            // Act
            bool success = sut.TryFormat(destination, out int bytesWritten);

            // Assert
            Assert.True(success);
            Assert.Equal("4:30", Encoding.UTF8.GetString(destination[..bytesWritten]));
        }

        [Fact]
        public void Should_Return_False_When_Utf8_Buffer_Is_Too_Small() {
            // Arrange
            PageRequest sut = new(100, 50);
            Span<byte> smallBuffer = stackalloc byte[2];

            // Act
            bool success = sut.TryFormat(smallBuffer, out int bytesWritten);

            // Assert
            Assert.False(success);
            Assert.Equal(0, bytesWritten);
        }
    }

    public sealed class ToStringMethod {
        [Fact]
        public void Should_Format_Non_Default_Instance() {
            // Arrange
            PageRequest sut = new(4, 30);

            // Act & Assert
            Assert.Equal("4:30", sut.ToString());
        }

        [Fact]
        public void Should_Round_Trip_Through_Parse() {
            // Arrange
            PageRequest original = new(7, 45);

            // Act
            string formatted = original.ToString();
            PageRequest reparsed = PageRequest.Parse(formatted);

            // Assert
            Assert.Equal(original, reparsed);
            Assert.Contains(original.PageNumber.ToString(), formatted);
            Assert.Contains(original.PageSize.ToString(), formatted);
        }
    }

    public sealed class IsEmptyProperty {

        [Fact]
        public void Should_Be_True_For_Empty_Static_Instance() {
            // PageRequest.Empty bypasses the constructor (raw default), so PageNumber/PageSize stay 0
            Assert.True(PageRequest.Empty.IsEmpty);
        }

        [Fact]
        public void Should_Be_True_For_Default_Struct_Value() {
            Assert.True(default(PageRequest).IsEmpty);
        }

        [Fact]
        public void Should_Be_False_For_Default_Static_Instance() {
            // PageRequest.Default is (1, 20) via the constructor - not the uninitialized (0, 0) state
            Assert.False(PageRequest.Default.IsEmpty);
        }

        [Fact]
        public void Should_Be_False_For_Any_Normally_Constructed_Request() {
            PageRequest request = new(1, 10);
            Assert.False(request.IsEmpty);
        }
    }

    public sealed class DeconstructMethod {

        [Fact]
        public void Should_Deconstruct_Into_PageNumber_And_PageSize() {
            // Arrange
            PageRequest request = new(3, 25);

            // Act
            (int pageNumber, int pageSize) = request;

            // Assert
            Assert.Equal(3, pageNumber);
            Assert.Equal(25, pageSize);
        }
    }

    public sealed class EqualityOperators {

        [Fact]
        public void Should_Consider_Two_Requests_With_Same_Values_Equal() {
            PageRequest a = new(2, 10);
            PageRequest b = new(2, 10);

            Assert.Equal(a, b);
            Assert.True(a == b);
        }

        [Fact]
        public void Should_Consider_Requests_With_Different_Values_Not_Equal() {
            PageRequest a = new(2, 10);
            PageRequest b = new(3, 10);

            Assert.NotEqual(a, b);
            Assert.True(a != b);
        }

        [Fact]
        public void Should_Consider_Equivalent_Post_Clamping_Requests_Equal() {
            // Arrange: Both clamp to (1, 20) despite different raw inputs
            PageRequest a = new(-5, -5);
            PageRequest b = new(0, 0);

            // Assert
            Assert.Equal(a, b);
        }

        [Fact]
        public void Should_Not_Consider_Empty_Equal_To_Default() {
            // Empty = (0,0) via raw default; Default = (1,20) via constructor - these must differ
            Assert.NotEqual(PageRequest.Empty, PageRequest.Default);
        }
    }
}