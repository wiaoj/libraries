using System.Text;

namespace Wiaoj.Querying.Tests.Unit;

/// <summary>
/// Unit test suite for <see cref="FilterConditionNode"/> validating factories, self-contained parsing,
/// formatting, unary helpers, and structural equality.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Feature", "FilterConditionNode")]
public class FilterConditionNodeTests {
    public sealed class InitializationAndFactories : FilterConditionNodeTests {
        [Fact]
        public void Default_Instance_Should_Be_Empty() {
            // Arrange & Act
            FilterConditionNode node = FilterConditionNode.Empty;

            // Assert
            Assert.True(node.IsEmpty);
            Assert.False(node.HasValue);
            Assert.False(node.IsUnary);
            Assert.Equal(string.Empty, node.Field);
        }

        [Fact]
        public void Factory_Equal_Should_Construct_Proper_Node() {
            // Arrange & Act
            var node = FilterConditionNode.Equal("status", "Active");

            // Assert
            Assert.Equal("status", node.Field);
            Assert.Equal(QueryOperator.Equal, node.Operator);
            Assert.Equal("Active", node.RawValue);
            Assert.False(node.IsUnary);
            Assert.True(node.HasValue);
        }

        [Fact]
        public void Factory_GreaterThanOrEqual_Should_Format_Numeric_Value() {
            // Arrange & Act
            var node = FilterConditionNode.GreaterThanOrEqual("price", 150.5m);

            // Assert
            Assert.Equal("price", node.Field);
            Assert.Equal(QueryOperator.GreaterThanOrEqual, node.Operator);
            Assert.Equal("150.5", node.RawValue);
        }

        [Fact]
        public void Factory_Between_Should_Format_Range_Syntax() {
            // Arrange & Act
            var node = FilterConditionNode.Between("price", 100, 500);

            // Assert
            Assert.Equal("price", node.Field);
            Assert.Equal(QueryOperator.Between, node.Operator);
            Assert.Equal("100..500", node.RawValue);
        }

        [Fact]
        public void Factory_IsNull_Should_Create_Unary_Node_With_Null_Value() {
            // Arrange & Act
            var node = FilterConditionNode.IsNull("deletedAt");

            // Assert
            Assert.Equal("deletedAt", node.Field);
            Assert.Equal(QueryOperator.IsNull, node.Operator);
            Assert.Null(node.RawValue);
            Assert.True(node.IsUnary);
            Assert.False(node.HasValue);
        }
    }

    public sealed class ParsingAndSpanSupport : FilterConditionNodeTests {
        [Theory]
        [InlineData("price[gte]=100", "price", QueryOperator.GreaterThanOrEqual, "100")]
        [InlineData("status[eq]=Active", "status", QueryOperator.Equal, "Active")]
        [InlineData("status=Active", "status", QueryOperator.Equal, "Active")]
        [InlineData("deletedAt[isNull]", "deletedAt", QueryOperator.IsNull, null)]
        public void Should_Parse_Valid_Bracket_Query_Span(
            string input,
            string expectedField,
            QueryOperator expectedOp,
            string? expectedValue) {
            // Act
            bool parsed = FilterConditionNode.TryParse(input.AsSpan(), out FilterConditionNode result);

            // Assert
            Assert.True(parsed);
            Assert.Equal(expectedField, result.Field);
            Assert.Equal(expectedOp, result.Operator);
            Assert.Equal(expectedValue, result.RawValue);
        }

        [Fact]
        public void Should_Parse_Utf8_Byte_Span_Correctly() {
            // Arrange
            byte[] utf8Bytes = Encoding.UTF8.GetBytes("price[gte]=100");

            // Act
            bool parsed = FilterConditionNode.TryParse((ReadOnlySpan<byte>)utf8Bytes, out FilterConditionNode result);

            // Assert
            Assert.True(parsed);
            Assert.Equal("price", result.Field);
            Assert.Equal(QueryOperator.GreaterThanOrEqual, result.Operator);
            Assert.Equal("100", result.RawValue);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("invalid_format[")]
        public void Should_Return_False_For_Malformed_Inputs(string input) {
            // Act
            bool parsed = FilterConditionNode.TryParse(input, out FilterConditionNode result);

            // Assert
            Assert.False(parsed);
            Assert.True(result.IsEmpty);
        }
    }

    public sealed class Formatting : FilterConditionNodeTests {
        [Fact]
        public void ToString_Should_Format_Standard_Condition_With_Operator_Brackets() {
            // Arrange
            var node = FilterConditionNode.GreaterThanOrEqual("price", 100);

            // Act
            string result = node.ToString();

            // Assert
            Assert.Equal("price[gte]=100", result);
        }

        [Fact]
        public void ToString_Should_Format_Unary_Null_Condition_Without_Equals() {
            // Arrange
            var node = FilterConditionNode.IsNull("deletedAt");

            // Act
            string result = node.ToString();

            // Assert
            Assert.Equal("deletedAt[isNull]", result);
        }

        [Fact]
        public void TryFormat_Char_Should_Format_Directly_Into_Span() {
            // Arrange
            var node = FilterConditionNode.Equal("category", "Books");
            Span<char> destination = stackalloc char[32];

            // Act
            bool succeeded = node.TryFormat(destination, out int charsWritten);

            // Assert
            Assert.True(succeeded);
            Assert.Equal("category[eq]=Books", destination[..charsWritten].ToString());
        }

        [Fact]
        public void TryFormat_Utf8_Should_Format_Directly_Into_Byte_Span() {
            // Arrange
            var node = FilterConditionNode.GreaterThanOrEqual("price", 250);
            Span<byte> destination = stackalloc byte[32];

            // Act
            bool succeeded = node.TryFormat(destination, out int bytesWritten);

            // Assert
            Assert.True(succeeded);
            Assert.Equal("price[gte]=250", Encoding.UTF8.GetString(destination[..bytesWritten]));
        }
    }
}