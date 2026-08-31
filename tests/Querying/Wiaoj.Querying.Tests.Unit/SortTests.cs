using System.Text;

namespace Wiaoj.Querying.Tests.Unit;

/// <summary>
/// Unit test suite for <see cref="Sort"/> and <see cref="SortNode"/> structs validating parsing,
/// custom enumeration, structural equality, formatting, and edge cases.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Feature", "Sort")]
public class SortTests {
    public sealed class InitializationAndState : SortTests {
        [Fact]
        public void Default_Instance_Should_Be_Empty() {
            // Arrange & Act
            Sort sort = Sort.Empty;

            // Assert
            Assert.True(sort.IsEmpty);
            Assert.Equal(0, sort.Count);
            Assert.Empty(sort);
        }

        [Fact]
        public void Node_Constructor_Should_Set_Properties() {
            // Arrange & Act
            var node = new SortNode("price", SortDirection.Descending);

            // Assert
            Assert.Equal("price", node.Field);
            Assert.Equal(SortDirection.Descending, node.Direction);
            Assert.True(node.IsDescending);
        }

        [Fact]
        public void Initialized_Instance_With_Nodes_Should_Expose_Correct_Count_And_Indexer() {
            // Arrange
            var nodes = new SortNode[] {
                new("price", SortDirection.Descending),
                new("createdAt", SortDirection.Ascending)
            };

            // Act
            var sort = new Sort(nodes);

            // Assert
            Assert.False(sort.IsEmpty);
            Assert.Equal(2, sort.Count);
            Assert.Equal("price", sort[0].Field);
            Assert.True(sort[0].IsDescending);
            Assert.Equal("createdAt", sort[1].Field);
            Assert.False(sort[1].IsDescending);
        }
    }

    public sealed class ParsingAndSpanSupport : SortTests {
        [Theory]
        [InlineData("price", "price", SortDirection.Ascending)]
        [InlineData("+price", "price", SortDirection.Ascending)]
        [InlineData("-price", "price", SortDirection.Descending)]
        [InlineData("  -price  ", "price", SortDirection.Descending)]
        public void Should_Parse_Single_Field_Sort_Expression(
            string input,
            string expectedField,
            SortDirection expectedDirection) {
            // Act
            bool parsed = Sort.TryParse(input, out Sort result);

            // Assert
            Assert.True(parsed);
            var node = Assert.Single(result);
            Assert.Equal(expectedField, node.Field);
            Assert.Equal(expectedDirection, node.Direction);
        }

        [Fact]
        public void Should_Parse_Multi_Field_Sort_Expression() {
            // Arrange
            const string input = "-price,createdAt,+stock";

            // Act
            bool parsed = Sort.TryParse(input.AsSpan(), out Sort result);

            // Assert
            Assert.True(parsed);
            Assert.Equal(3, result.Count);

            Assert.Equal("price", result[0].Field);
            Assert.Equal(SortDirection.Descending, result[0].Direction);

            Assert.Equal("createdAt", result[1].Field);
            Assert.Equal(SortDirection.Ascending, result[1].Direction);

            Assert.Equal("stock", result[2].Field);
            Assert.Equal(SortDirection.Ascending, result[2].Direction);
        }

        [Fact]
        public void Should_Handle_Consecutive_Commas_And_Whitespace_Gracefully() {
            // Arrange
            const string input = " , -price , , createdAt , ";

            // Act
            bool parsed = Sort.TryParse(input.AsSpan(), out Sort result);

            // Assert
            Assert.True(parsed);
            Assert.Equal(2, result.Count);
            Assert.Equal("price", result[0].Field);
            Assert.Equal("createdAt", result[1].Field);
        }

        [Fact]
        public void Should_Parse_Utf8_Byte_Span_Correctly() {
            // Arrange
            byte[] utf8Bytes = Encoding.UTF8.GetBytes("-price,createdAt");

            // Act
            bool parsed = Sort.TryParse((ReadOnlySpan<byte>)utf8Bytes, out Sort result);

            // Assert
            Assert.True(parsed);
            Assert.Equal(2, result.Count);
            Assert.Equal("price", result[0].Field);
            Assert.True(result[0].IsDescending);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(",")]
        [InlineData(",,,")]
        public void Should_Return_Empty_Sort_For_Empty_Or_Whitespace_Inputs(string input) {
            // Act
            bool parsed = Sort.TryParse(input, out Sort result);

            // Assert
            Assert.True(parsed);
            Assert.True(result.IsEmpty);
        }
    }

    public sealed class Enumeration : SortTests {
        [Fact]
        public void Should_Enumerate_Nodes_Using_Custom_Struct_Enumerator_Without_Allocations() {
            // Arrange
            var sort = new Sort("-price,createdAt");
            var fields = new List<string>();

            // Act
            foreach(SortNode node in sort) {
                fields.Add(node.Field);
            }

            // Assert
            Assert.Equal(["price", "createdAt"], fields);
        }
    }

    public sealed class Equality : SortTests {
        [Fact]
        public void Sort_Instances_With_Same_Nodes_Should_Be_Equal() {
            // Arrange
            var sort1 = Sort.Parse("-price,createdAt");
            var sort2 = new Sort([
                new SortNode("price", SortDirection.Descending),
                new SortNode("createdAt", SortDirection.Ascending)
            ]);

            // Act & Assert
            Assert.Equal(sort1, sort2);
            Assert.True(sort1 == sort2);
            Assert.Equal(sort1.GetHashCode(), sort2.GetHashCode());
        }

        [Fact]
        public void Empty_Instances_Should_Be_Equal() {
            // Arrange
            var sort1 = Sort.Empty;
            var sort2 = new Sort(string.Empty);

            // Act & Assert
            Assert.Equal(sort1, sort2);
            Assert.True(sort1 == sort2);
        }

        [Fact]
        public void Different_Sort_Directions_Should_Not_Be_Equal() {
            // Arrange
            var sort1 = Sort.Parse("price");
            var sort2 = Sort.Parse("-price");

            // Act & Assert
            Assert.NotEqual(sort1, sort2);
            Assert.True(sort1 != sort2);
        }
    }

    public sealed class Formatting : SortTests {
        [Fact]
        public void ToString_Should_Return_Normalized_Sort_Expression() {
            // Arrange
            var sort = new Sort("-price,createdAt");

            // Act
            string result = sort.ToString();

            // Assert
            Assert.Equal("-price,createdAt", result);
        }

        [Fact]
        public void ToString_Should_Return_Empty_String_When_Empty() {
            // Arrange
            var sort = Sort.Empty;

            // Act
            string result = sort.ToString();

            // Assert
            Assert.Equal(string.Empty, result);
        }

        [Fact]
        public void TryFormat_Char_Should_Format_Correctly() {
            // Arrange
            var sort = new Sort("-price,createdAt");
            Span<char> destination = stackalloc char[32];

            // Act
            bool succeeded = sort.TryFormat(destination, out int charsWritten);

            // Assert
            Assert.True(succeeded);
            Assert.Equal("-price,createdAt", destination[..charsWritten].ToString());
        }

        [Fact]
        public void TryFormat_Utf8_Should_Format_Correctly() {
            // Arrange
            var sort = new Sort("-price,createdAt");
            Span<byte> destination = stackalloc byte[32];

            // Act
            bool succeeded = sort.TryFormat(destination, out int bytesWritten);

            // Assert
            Assert.True(succeeded);
            Assert.Equal("-price,createdAt", Encoding.UTF8.GetString(destination[..bytesWritten]));
        }
    }
}