using System.Text;
using Wiaoj.Primitives.Hashing;

namespace Wiaoj.Querying.Tests.Unit;

/// <summary>
/// Unit test suite for <see cref="QueryRequest"/> struct behavior, parsing, hashing, and formatting.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Feature", "QueryRequest")]
public class QueryRequestTests {
    public sealed class InitializationAndState : QueryRequestTests {
        [Fact]
        public void Default_Instance_Should_Be_Empty() {
            // Arrange & Act
            QueryRequest request = QueryRequest.Empty;

            // Assert
            Assert.True(request.IsEmpty);
            Assert.True(request.Q.IsEmpty);
            Assert.True(request.Sort.IsEmpty);
            Assert.Empty(request.Filters);
        }

        [Fact]
        public void Initialized_Instance_Should_Retain_Provided_Values() {
            // Arrange
            FilterConditionNode[] filters = [
                new("price", QueryOperator.GreaterThanOrEqual, "100")
            ];
            Sort sort = new("-price");

            // Act
            QueryRequest request = new(q: new Q("laptop"), sort: sort, filters: filters);

            // Assert
            Assert.False(request.IsEmpty);
            Assert.Equal("laptop", request.Q.Value);
            Assert.Equal(sort, request.Sort);
            Assert.Equal("-price", request.Sort.ToString());
            Assert.Single(request.Filters);
        }

        [Fact]
        public void Sort_Should_Be_Trimmed_And_Normalized() {
            // Arrange & Act
            QueryRequest request = new(sort: new Sort("  -createdAt  "));

            // Assert
            Assert.Equal("-createdAt", request.Sort.ToString());
        }

        [Fact]
        public void Sort_Should_Be_Empty_When_Whitespace() {
            // Arrange & Act
            QueryRequest request = new(sort: new Sort("   "));

            // Assert
            Assert.True(request.Sort.IsEmpty);
        }

        [Fact]
        public void Filters_Should_Default_To_Empty_When_Not_Provided() {
            // Arrange & Act
            QueryRequest request = new(q: new Q("mouse"));

            // Assert
            Assert.Empty(request.Filters);
        }

        [Fact]
        public void IsEmpty_Should_Be_False_When_Only_Filters_Are_Provided() {
            // Arrange
            FilterConditionNode[] filters = [
                new("stock", QueryOperator.GreaterThan, "0")
            ];

            // Act
            QueryRequest request = new(filters: filters);

            // Assert
            Assert.False(request.IsEmpty);
        }

        [Fact]
        public void IsEmpty_Should_Be_False_When_Only_Sort_Is_Provided() {
            // Arrange & Act
            QueryRequest request = new(sort: new Sort("price"));

            // Assert
            Assert.False(request.IsEmpty);
        }
    }

    public sealed class ParsingAndSpanSupport : QueryRequestTests {
        [Fact]
        public void Should_Parse_Full_Query_String_With_Search_Sort_And_Filters() {
            // Arrange
            const string queryString = "q=laptop&sort=-price,createdAt&category[eq]=Electronics&price[gte]=100";

            // Act
            bool parsed = QueryRequest.TryParse(queryString, out QueryRequest request);

            // Assert
            Assert.True(parsed);
            Assert.Equal("laptop", request.Q.Value);
            Assert.Equal("-price,createdAt", request.Sort.ToString());
            Assert.Equal(2, request.Sort.Count);
            Assert.Equal(2, request.Filters.Count);
            Assert.Equal("category", request.Filters[0].Field);
            Assert.Equal(QueryOperator.Equal, request.Filters[0].Operator);
            Assert.Equal("Electronics", request.Filters[0].RawValue);
            Assert.Equal("price", request.Filters[1].Field);
            Assert.Equal(QueryOperator.GreaterThanOrEqual, request.Filters[1].Operator);
            Assert.Equal("100", request.Filters[1].RawValue);
        }

        [Fact]
        public void Should_Handle_Leading_Question_Mark_And_Surrounding_Whitespace() {
            // Arrange
            const string queryString = "  ?q=monitor&price[lt]=500  ";

            // Act
            bool parsed = QueryRequest.TryParse(queryString.AsSpan(), out QueryRequest request);

            // Assert
            Assert.True(parsed);
            Assert.Equal("monitor", request.Q.Value);
            Assert.Single(request.Filters);
            Assert.Equal("price", request.Filters[0].Field);
            Assert.Equal(QueryOperator.LessThan, request.Filters[0].Operator);
            Assert.Equal("500", request.Filters[0].RawValue);
        }

        [Fact]
        public void Should_Handle_Multiple_Consecutive_Ampersands_And_Trailing_Delimiters() {
            // Arrange
            const string queryString = "&&q=desk&&&sort=name&&";

            // Act
            bool parsed = QueryRequest.TryParse(queryString.AsSpan(), out QueryRequest request);

            // Assert
            Assert.True(parsed);
            Assert.Equal("desk", request.Q.Value);
            Assert.Equal("name", request.Sort.ToString());
            Assert.Empty(request.Filters);
        }

        [Fact]
        public void Should_Parse_Unary_Null_Filters_Correctly() {
            // Arrange
            const string queryString = "deletedAt[isNull]&assignedTo[isNotNull]";

            // Act
            bool parsed = QueryRequest.TryParse(queryString, out QueryRequest request);

            // Assert
            Assert.True(parsed);
            Assert.Equal(2, request.Filters.Count);
            Assert.Equal(QueryOperator.IsNull, request.Filters[0].Operator);
            Assert.Null(request.Filters[0].RawValue);
            Assert.Equal(QueryOperator.IsNotNull, request.Filters[1].Operator);
            Assert.Null(request.Filters[1].RawValue);
        }

        [Fact]
        public void Should_Parse_Utf8_Byte_Span_Correctly() {
            // Arrange
            byte[] utf8Bytes = Encoding.UTF8.GetBytes("q=gaming&sort=-price&stock[gt]=0");

            // Act
            bool parsed = QueryRequest.TryParse((ReadOnlySpan<byte>)utf8Bytes, out QueryRequest request);

            // Assert
            Assert.True(parsed);
            Assert.Equal("gaming", request.Q.Value);
            Assert.Equal("-price", request.Sort.ToString());
            Assert.Single(request.Filters);
            Assert.Equal("stock", request.Filters[0].Field);
            Assert.Equal(QueryOperator.GreaterThan, request.Filters[0].Operator);
            Assert.Equal("0", request.Filters[0].RawValue);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("?")]
        [InlineData("?   ")]
        public void Should_Return_Empty_Request_For_Empty_Or_Whitespace_Inputs(string input) {
            // Act
            bool parsed = QueryRequest.TryParse(input, out QueryRequest request);

            // Assert
            Assert.True(parsed);
            Assert.True(request.IsEmpty);
        }
    }

    public sealed class Equality : QueryRequestTests {
        [Fact]
        public void Empty_Should_Equal_Default_Initialized_Instance() {
            // Arrange
            QueryRequest explicitlyEmpty = new();
            QueryRequest defaultInitialized = default;

            // Act & Assert
            Assert.Equal(explicitlyEmpty, defaultInitialized);
            Assert.True(explicitlyEmpty == defaultInitialized);
            Assert.Equal(explicitlyEmpty.GetHashCode(), defaultInitialized.GetHashCode());
        }

        [Fact]
        public void Requests_With_Same_Filter_Content_But_Different_Array_Instances_Should_Be_Equal() {
            // Arrange
            FilterConditionNode[] filters1 = [new("price", QueryOperator.GreaterThan, "50")];
            FilterConditionNode[] filters2 = [new("price", QueryOperator.GreaterThan, "50")];

            QueryRequest request1 = new(q: new Q("gaming"), filters: filters1);
            QueryRequest request2 = new(q: new Q("gaming"), filters: filters2);

            // Act & Assert
            Assert.Equal(request1, request2);
            Assert.Equal(request1.GetHashCode(), request2.GetHashCode());
        }

        [Fact]
        public void Requests_With_Different_Filter_Content_Should_Not_Be_Equal() {
            // Arrange
            FilterConditionNode[] filters1 = [new("price", QueryOperator.GreaterThan, "50")];
            FilterConditionNode[] filters2 = [new("price", QueryOperator.GreaterThan, "100")];

            QueryRequest request1 = new(q: new Q("gaming"), filters: filters1);
            QueryRequest request2 = new(q: new Q("gaming"), filters: filters2);

            // Act & Assert
            Assert.NotEqual(request1, request2);
        }
    }

    public sealed class DeterministicHashing : QueryRequestTests {
        [Fact]
        public void Identical_Requests_Should_Produce_Identical_QueryHash() {
            // Arrange
            FilterConditionNode[] filters1 = [new("price", QueryOperator.GreaterThan, "50")];
            FilterConditionNode[] filters2 = [new("price", QueryOperator.GreaterThan, "50")];
            Sort sort = new("-createdAt");

            // Act
            QueryRequest request1 = new(q: new Q("gaming"), sort: sort, filters: filters1);
            QueryRequest request2 = new(q: new Q("gaming"), sort: sort, filters: filters2);

            // Assert
            Assert.Equal(request1.QueryHash, request2.QueryHash);
        }

        [Fact]
        public void Different_Search_Term_Should_Produce_Different_QueryHash() {
            // Arrange & Act
            Sort sort = new("-createdAt");
            QueryRequest request1 = new(q: new Q("gaming"), sort: sort);
            QueryRequest request2 = new(q: new Q("office"), sort: sort);

            // Assert
            Assert.NotEqual(request1.QueryHash, request2.QueryHash);
        }

        [Fact]
        public void Different_Sort_Should_Produce_Different_QueryHash() {
            // Arrange & Act
            QueryRequest request1 = new(q: new Q("gaming"), sort: new Sort("-createdAt"));
            QueryRequest request2 = new(q: new Q("gaming"), sort: new Sort("createdAt"));

            // Assert
            Assert.NotEqual(request1.QueryHash, request2.QueryHash);
        }

        [Fact]
        public void Different_Filters_Should_Produce_Different_QueryHash() {
            // Arrange
            FilterConditionNode[] filters1 = [new("price", QueryOperator.GreaterThan, "50")];
            FilterConditionNode[] filters2 = [new("price", QueryOperator.GreaterThan, "100")];

            // Act
            QueryRequest request1 = new(q: new Q("gaming"), filters: filters1);
            QueryRequest request2 = new(q: new Q("gaming"), filters: filters2);

            // Assert
            Assert.NotEqual(request1.QueryHash, request2.QueryHash);
        }

        [Fact]
        public void Empty_Request_Should_Have_Empty_QueryHash() {
            // Arrange & Act
            QueryRequest request = QueryRequest.Empty;

            // Assert
            Assert.Equal(XxHash3.Empty, request.QueryHash);
        }

        [Fact]
        public void QueryHash_Should_Reflect_Changes_Made_Through_With_Expression() {
            // Arrange
            QueryRequest original = new(q: new Q("gaming"), sort: new Sort("-createdAt"));

            // Act
            QueryRequest modified = original with { Sort = new Sort("-price") };

            // Assert
            Assert.NotEqual(original.QueryHash, modified.QueryHash);
        }

        [Fact]
        public void QueryHash_Should_Stay_Equal_When_With_Expression_Does_Not_Change_State() {
            // Arrange
            QueryRequest original = new(q: new Q("gaming"), sort: new Sort("-createdAt"));

            // Act
            QueryRequest copy = original with { };

            // Assert
            Assert.Equal(original.QueryHash, copy.QueryHash);
        }
    }

    public sealed class Formatting : QueryRequestTests {
        [Fact]
        public void ToString_Should_Return_Empty_Marker_When_Empty() {
            // Arrange
            QueryRequest request = QueryRequest.Empty;

            // Act
            string result = request.ToString();

            // Assert
            Assert.Equal("[Empty QueryRequest]", result);
        }

        [Fact]
        public void ToString_Should_Include_Q_Sort_And_Filter_Count() {
            // Arrange
            FilterConditionNode[] filters = [
                new("price", QueryOperator.GreaterThan, "50")
            ];
            QueryRequest request = new(q: new Q("gaming"), sort: new Sort("-createdAt"), filters: filters);

            // Act
            string result = request.ToString();

            // Assert
            Assert.Equal("Q: gaming, Sort: -createdAt, Filters: 1", result);
        }

        [Fact]
        public void ToString_Should_Use_None_Marker_When_Sort_Is_Absent() {
            // Arrange
            QueryRequest request = new(q: new Q("gaming"));

            // Act
            string result = request.ToString();

            // Assert
            Assert.Equal("Q: gaming, Sort: [None], Filters: 0", result);
        }

        [Fact]
        public void TryFormat_Char_Should_Write_Same_Text_As_ToString() {
            // Arrange
            QueryRequest request = new(q: new Q("gaming"), sort: new Sort("-createdAt"));
            Span<char> destination = stackalloc char[128];

            // Act
            bool succeeded = request.TryFormat(destination, out int charsWritten);

            // Assert
            Assert.True(succeeded);
            Assert.Equal(request.ToString(), destination[..charsWritten].ToString());
        }

        [Fact]
        public void TryFormat_Utf8_Should_Write_Same_Text_As_ToString() {
            // Arrange
            QueryRequest request = new(q: new Q("gaming"), sort: new Sort("-createdAt"));
            Span<byte> destination = stackalloc byte[128];

            // Act
            bool succeeded = request.TryFormat(destination, out int bytesWritten);

            // Assert
            Assert.True(succeeded);
            Assert.Equal(request.ToString(), Encoding.UTF8.GetString(destination[..bytesWritten]));
        }
    }
}