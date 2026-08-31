using System.Text;

namespace Wiaoj.Querying.Tests.Unit;
/// <summary>
/// Unit test suite for <see cref="Q"/> struct behavior, parsing, formatting, equality, and ordering.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Feature", "Q")]
public class QTests {
    public sealed class ConstructionAndNormalization : QTests {
        [Fact]
        public void Constructor_Should_Trim_Surrounding_Whitespace() {
            // Arrange
            const string raw = "  laptop  ";

            // Act
            Q q = new(raw);

            // Assert
            Assert.Equal("laptop", q.Value);
        }

        [Fact]
        public void Constructor_With_Null_Should_Be_Empty() {
            // Arrange & Act
            Q q = new(value: null);

            // Assert
            Assert.True(q.IsEmpty);
            Assert.Equal(string.Empty, q.Value);
        }

        [Fact]
        public void Constructor_With_Whitespace_Only_Should_Be_Empty() {
            // Arrange & Act
            Q q = new("   ");

            // Assert
            Assert.True(q.IsEmpty);
        }

        [Fact]
        public void Constructor_From_Span_Should_Trim_And_Normalize() {
            // Arrange
            ReadOnlySpan<char> raw = "  gaming mouse  ".AsSpan();

            // Act
            Q q = new(raw);

            // Assert
            Assert.Equal("gaming mouse", q.Value);
        }

        [Fact]
        public void Empty_Constant_Should_Be_Empty() {
            // Arrange & Act
            var q = Q.Empty;

            // Assert
            Assert.True(q.IsEmpty);
            Assert.Equal(0, q.Length);
        }
    }

    public sealed class LengthAndValue : QTests {
        [Fact]
        public void Length_Should_Reflect_Normalized_Value() {
            // Arrange
            Q q = new("  keyboard  ");

            // Act
            int length = q.Length;

            // Assert
            Assert.Equal(8, length);
        }

        [Fact]
        public void Length_Should_Be_Zero_When_Empty() {
            // Arrange
            var q = Q.Empty;

            // Act
            int length = q.Length;

            // Assert
            Assert.Equal(0, length);
        }
    }

    public sealed class Parsing : QTests {
        [Fact]
        public void Parse_String_Should_Normalize_Value() {
            // Arrange
            const string raw = "  monitor  ";

            // Act
            Q q = Q.Parse(raw);

            // Assert
            Assert.Equal("monitor", q.Value);
        }

        [Fact]
        public void Parse_Span_Should_Normalize_Value() {
            // Arrange
            ReadOnlySpan<char> raw = "  webcam  ".AsSpan();

            // Act
            Q q = Q.Parse(raw);

            // Assert
            Assert.Equal("webcam", q.Value);
        }

        [Fact]
        public void Parse_Utf8_Should_Decode_And_Normalize_Value() {
            // Arrange
            byte[] utf8 = Encoding.UTF8.GetBytes("  headset  ");

            // Act
            Q q = Q.Parse(utf8);

            // Assert
            Assert.Equal("headset", q.Value);
        }

        [Fact]
        public void Parse_Utf8_Should_Return_Empty_For_Empty_Input() {
            // Arrange
            ReadOnlySpan<byte> utf8 = [];

            // Act
            Q q = Q.Parse(utf8);

            // Assert
            Assert.True(q.IsEmpty);
        }

        [Fact]
        public void Parse_Utf8_Should_Throw_When_Exceeding_MaxUtf8Length() {
            // Arrange
            byte[] utf8 = new byte[Q.MaxUtf8Length + 1];
            Array.Fill(utf8, (byte)'a');

            // Act & Assert
            Assert.Throws<ArgumentException>(() => Q.Parse(utf8));
        }

        [Fact]
        public void Parse_Utf8_Should_Throw_For_Invalid_Byte_Sequence() {
            // Arrange
            byte[] invalidUtf8 = [0xFF, 0xFE, 0xFD];

            // Act & Assert
            Assert.Throws<ArgumentException>(() => Q.Parse(invalidUtf8.AsSpan()));
        }
    }

    public sealed class TryParsing : QTests {
        [Fact]
        public void TryParse_String_Should_Succeed_And_Normalize() {
            // Arrange
            const string raw = "  chair  ";

            // Act
            bool succeeded = Q.TryParse(raw, out var result);

            // Assert
            Assert.True(succeeded);
            Assert.Equal("chair", result.Value);
        }

        [Fact]
        public void TryParse_Null_String_Should_Succeed_With_Empty_Result() {
            // Arrange & Act
            bool succeeded = Q.TryParse((string?)null, out var result);

            // Assert
            Assert.True(succeeded);
            Assert.True(result.IsEmpty);
        }

        [Fact]
        public void TryParse_Span_Should_Succeed_And_Normalize() {
            // Arrange
            ReadOnlySpan<char> raw = "  desk  ".AsSpan();

            // Act
            bool succeeded = Q.TryParse(raw, out var result);

            // Assert
            Assert.True(succeeded);
            Assert.Equal("desk", result.Value);
        }

        [Fact]
        public void TryParse_Utf8_Should_Succeed_For_Valid_Input() {
            // Arrange
            byte[] utf8 = Encoding.UTF8.GetBytes("lamp");

            // Act
            bool succeeded = Q.TryParse(utf8, out var result);

            // Assert
            Assert.True(succeeded);
            Assert.Equal("lamp", result.Value);
        }

        [Fact]
        public void TryParse_Utf8_Should_Fail_When_Exceeding_MaxUtf8Length() {
            // Arrange
            byte[] utf8 = new byte[Q.MaxUtf8Length + 1];
            Array.Fill(utf8, (byte)'a');

            // Act
            bool succeeded = Q.TryParse(utf8, out var result);

            // Assert
            Assert.False(succeeded);
            Assert.True(result.IsEmpty);
        }

        [Fact]
        public void TryParse_Utf8_Should_Fail_For_Invalid_Byte_Sequence() {
            // Arrange
            byte[] invalidUtf8 = [0xFF, 0xFE, 0xFD];

            // Act
            bool succeeded = Q.TryParse((ReadOnlySpan<byte>)invalidUtf8, out var result);

            // Assert
            Assert.False(succeeded);
            Assert.True(result.IsEmpty);
        }
    }

    public sealed class Equality : QTests {
        [Fact]
        public void Equals_Should_Be_Case_Insensitive() {
            // Arrange
            Q left = new("Laptop");
            Q right = new("laptop");

            // Act & Assert
            Assert.True(left.Equals(right));
            Assert.True(left == right);
        }

        [Fact]
        public void Equals_Should_Return_False_For_Different_Values() {
            // Arrange
            Q left = new("laptop");
            Q right = new("desktop");

            // Act & Assert
            Assert.False(left.Equals(right));
            Assert.True(left != right);
        }

        [Fact]
        public void GetHashCode_Should_Be_Case_Insensitive() {
            // Arrange
            Q left = new("Gaming");
            Q right = new("gaming");

            // Act & Assert
            Assert.Equal(left.GetHashCode(), right.GetHashCode());
        }

        [Fact]
        public void Empty_Instances_Should_Be_Equal() {
            // Arrange
            Q left = new(null);
            var right = Q.Empty;

            // Act & Assert
            Assert.True(left.Equals(right));
        }
    }

    public sealed class ComparisonAndOrdering : QTests {
        [Fact]
        public void CompareTo_Should_Treat_Different_Casing_As_Equal() {
            // Arrange
            Q left = new("apple");
            Q right = new("APPLE");

            // Act
            int result = left.CompareTo(right);

            // Assert
            Assert.Equal(0, result);
        }

        [Fact]
        public void LessThan_Operator_Should_Reflect_Ordinal_Order() {
            // Arrange
            Q left = new("apple");
            Q right = new("banana");

            // Act & Assert
            Assert.True(left < right);
            Assert.False(right < left);
        }

        [Fact]
        public void GreaterThan_Operator_Should_Reflect_Ordinal_Order() {
            // Arrange
            Q left = new("banana");
            Q right = new("apple");

            // Act & Assert
            Assert.True(left > right);
        }

        [Fact]
        public void CompareTo_Object_Should_Return_Positive_For_Null() {
            // Arrange
            Q q = new("value");

            // Act
            int result = q.CompareTo(null);

            // Assert
            Assert.True(result > 0);
        }

        [Fact]
        public void CompareTo_Object_Should_Throw_For_Incompatible_Type() {
            // Arrange
            Q q = new("value");
            object other = 42;

            // Act & Assert
            Assert.Throws<ArgumentException>(() => q.CompareTo(other));
        }
    }

    public sealed class Formatting : QTests {
        [Fact]
        public void ToString_Should_Return_Normalized_Value() {
            // Arrange
            Q q = new("  tablet  ");

            // Act
            string result = q.ToString();

            // Assert
            Assert.Equal("tablet", result);
        }

        [Fact]
        public void AsSpan_Should_Expose_Normalized_Value() {
            // Arrange
            Q q = new("printer");

            // Act
            ReadOnlySpan<char> span = q.AsSpan();

            // Assert
            Assert.Equal("printer", span.ToString());
        }

        [Fact]
        public void TryFormat_Char_Should_Write_Value_When_Destination_Is_Large_Enough() {
            // Arrange
            Q q = new("router");
            Span<char> destination = stackalloc char[16];

            // Act
            bool succeeded = q.TryFormat(destination, out int charsWritten);

            // Assert
            Assert.True(succeeded);
            Assert.Equal("router", destination[..charsWritten].ToString());
        }

        [Fact]
        public void TryFormat_Char_Should_Fail_When_Destination_Too_Small() {
            // Arrange
            Q q = new("router");
            Span<char> destination = stackalloc char[2];

            // Act
            bool succeeded = q.TryFormat(destination, out int charsWritten);

            // Assert
            Assert.False(succeeded);
            Assert.Equal(0, charsWritten);
        }

        [Fact]
        public void TryFormat_Char_Should_Succeed_With_Zero_Chars_When_Empty() {
            // Arrange
            var q = Q.Empty;
            Span<char> destination = stackalloc char[4];

            // Act
            bool succeeded = q.TryFormat(destination, out int charsWritten);

            // Assert
            Assert.True(succeeded);
            Assert.Equal(0, charsWritten);
        }

        [Fact]
        public void TryFormat_Utf8_Should_Write_Expected_Bytes() {
            // Arrange
            Q q = new("switch");
            Span<byte> destination = stackalloc byte[16];

            // Act
            bool succeeded = q.TryFormat(destination, out int bytesWritten);

            // Assert
            Assert.True(succeeded);
            Assert.Equal("switch", Encoding.UTF8.GetString(destination[..bytesWritten]));
        }
    }
}