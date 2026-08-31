using System.Text.Json;

namespace Wiaoj.Querying.Tests.Unit;

/// <summary>
/// Unit test suite verifying Native AOT-safe JSON serialization and deserialization
/// for <see cref="QueryOperator"/>, <see cref="Q"/>, <see cref="Sort"/>, <see cref="FilterConditionNode"/>,
/// and <see cref="QueryRequest"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Feature", "JsonConverters")]
public class QueryJsonConverterTests {
    private static readonly JsonSerializerOptions JsonOptions = new() {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public sealed class QueryOperatorSerialization : QueryJsonConverterTests {
        [Theory]
        [InlineData(QueryOperator.Equal, "\"eq\"")]
        [InlineData(QueryOperator.NotEqual, "\"neq\"")]
        [InlineData(QueryOperator.GreaterThan, "\"gt\"")]
        [InlineData(QueryOperator.GreaterThanOrEqual, "\"gte\"")]
        [InlineData(QueryOperator.LessThan, "\"lt\"")]
        [InlineData(QueryOperator.LessThanOrEqual, "\"lte\"")]
        [InlineData(QueryOperator.Contains, "\"contains\"")]
        [InlineData(QueryOperator.NotContains, "\"notContains\"")]
        [InlineData(QueryOperator.StartsWith, "\"startsWith\"")]
        [InlineData(QueryOperator.NotStartsWith, "\"notStartsWith\"")]
        [InlineData(QueryOperator.EndsWith, "\"endsWith\"")]
        [InlineData(QueryOperator.NotEndsWith, "\"notEndsWith\"")]
        [InlineData(QueryOperator.In, "\"in\"")]
        [InlineData(QueryOperator.NotIn, "\"notIn\"")]
        [InlineData(QueryOperator.Between, "\"between\"")]
        [InlineData(QueryOperator.NotBetween, "\"notBetween\"")]
        [InlineData(QueryOperator.IsNull, "\"isNull\"")]
        [InlineData(QueryOperator.IsNotNull, "\"isNotNull\"")]
        public void Should_Serialize_QueryOperator_To_Syntax_String(QueryOperator op, string expectedJson) {
            // Arrange & Act
            string json = JsonSerializer.Serialize(op, JsonOptions);

            // Assert
            Assert.Equal(expectedJson, json);
        }

        [Theory]
        [InlineData("\"eq\"", QueryOperator.Equal)]
        [InlineData("\"EQ\"", QueryOperator.Equal)]
        [InlineData("\"gte\"", QueryOperator.GreaterThanOrEqual)]
        [InlineData("\"GTE\"", QueryOperator.GreaterThanOrEqual)]
        [InlineData("\"isNull\"", QueryOperator.IsNull)]
        [InlineData("\"ISNULL\"", QueryOperator.IsNull)]
        public void Should_Deserialize_QueryOperator_Case_Insensitively(string json, QueryOperator expectedOp) {
            // Arrange & Act
            var op = JsonSerializer.Deserialize<QueryOperator>(json, JsonOptions);

            // Assert
            Assert.Equal(expectedOp, op);
        }

        [Fact]
        public void Should_Throw_JsonException_For_Unknown_QueryOperator_String() {
            // Arrange
            const string invalidJson = "\"unknown_operator\"";

            // Act & Assert
            Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<QueryOperator>(invalidJson, JsonOptions));
        }
    }

    public sealed class QSerialization : QueryJsonConverterTests {
        [Fact]
        public void Should_Serialize_Q_As_Primitive_Json_String() {
            // Arrange
            var q = new Q("laptop");

            // Act
            string json = JsonSerializer.Serialize(q, JsonOptions);

            // Assert
            Assert.Equal("\"laptop\"", json);
        }

        [Fact]
        public void Should_Deserialize_String_Into_Normalized_Q_Instance() {
            // Arrange
            const string json = "\"  gaming mouse  \"";

            // Act
            var q = JsonSerializer.Deserialize<Q>(json, JsonOptions);

            // Assert
            Assert.Equal("gaming mouse", q.Value);
        }

        [Fact]
        public void Should_Deserialize_Null_Token_As_Empty_Q() {
            // Arrange
            const string json = "null";

            // Act
            var q = JsonSerializer.Deserialize<Q>(json, JsonOptions);

            // Assert
            Assert.True(q.IsEmpty);
        }
    }

    public sealed class SortSerialization : QueryJsonConverterTests {
        [Fact]
        public void Should_Serialize_Sort_As_String_Expression() {
            // Arrange
            var sort = new Sort("-price,createdAt");

            // Act
            string json = JsonSerializer.Serialize(sort, JsonOptions);

            // Assert
            Assert.Equal("\"-price,createdAt\"", json);
        }

        [Fact]
        public void Should_Deserialize_String_Into_Structured_Sort_Instance() {
            // Arrange
            const string json = "\"-price,createdAt\"";

            // Act
            var sort = JsonSerializer.Deserialize<Sort>(json, JsonOptions);

            // Assert
            Assert.Equal(2, sort.Count);
            Assert.Equal("price", sort[0].Field);
            Assert.True(sort[0].IsDescending);
            Assert.Equal("createdAt", sort[1].Field);
            Assert.False(sort[1].IsDescending);
        }

        [Fact]
        public void Should_Deserialize_Null_Or_Empty_String_As_Empty_Sort() {
            // Arrange
            const string nullJson = "null";
            const string emptyJson = "\"\"";

            // Act
            var sortFromNull = JsonSerializer.Deserialize<Sort>(nullJson, JsonOptions);
            var sortFromEmpty = JsonSerializer.Deserialize<Sort>(emptyJson, JsonOptions);

            // Assert
            Assert.True(sortFromNull.IsEmpty);
            Assert.True(sortFromEmpty.IsEmpty);
        }
    }

    public sealed class FilterConditionNodeSerialization : QueryJsonConverterTests {
        [Fact]
        public void Should_Serialize_And_Deserialize_FilterConditionNode_With_Value() {
            // Arrange
            var originalNode = new FilterConditionNode("price", QueryOperator.GreaterThanOrEqual, "100");

            // Act
            string json = JsonSerializer.Serialize(originalNode, JsonOptions);
            var deserializedNode = JsonSerializer.Deserialize<FilterConditionNode>(json, JsonOptions);

            // Assert
            Assert.Equal(originalNode, deserializedNode);
            Assert.Equal("price", deserializedNode.Field);
            Assert.Equal(QueryOperator.GreaterThanOrEqual, deserializedNode.Operator);
            Assert.Equal("100", deserializedNode.RawValue);
        }

        [Fact]
        public void Should_Serialize_And_Deserialize_Unary_Null_FilterConditionNode() {
            // Arrange
            var originalNode = new FilterConditionNode("deletedAt", QueryOperator.IsNull);

            // Act
            string json = JsonSerializer.Serialize(originalNode, JsonOptions);
            var deserializedNode = JsonSerializer.Deserialize<FilterConditionNode>(json, JsonOptions);

            // Assert
            Assert.Equal(originalNode, deserializedNode);
            Assert.Equal("deletedAt", deserializedNode.Field);
            Assert.Equal(QueryOperator.IsNull, deserializedNode.Operator);
            Assert.Null(deserializedNode.RawValue);
        }
    }

    public sealed class QueryRequestSerialization : QueryJsonConverterTests {
        [Fact]
        public void Should_Perform_Complete_RoundTrip_Serialization_Of_QueryRequest() {
            // Arrange
            var originalRequest = new QueryRequest(
                q: new Q("laptop"),
                sort: new Sort("-price,createdAt"),
                filters: [
                    new FilterConditionNode("category", QueryOperator.Equal, "Electronics"),
                    new FilterConditionNode("price", QueryOperator.GreaterThanOrEqual, "1000"),
                    new FilterConditionNode("deletedAt", QueryOperator.IsNull)
                ]);

            // Act
            string json = JsonSerializer.Serialize(originalRequest, JsonOptions);
            var deserializedRequest = JsonSerializer.Deserialize<QueryRequest>(json, JsonOptions);

            // Assert
            Assert.Equal(originalRequest, deserializedRequest);
            Assert.Equal(originalRequest.QueryHash, deserializedRequest.QueryHash);
            Assert.Equal("laptop", deserializedRequest.Q.Value);
            Assert.Equal("-price,createdAt", deserializedRequest.Sort.ToString());
            Assert.Equal(3, deserializedRequest.Filters.Count);
        }

        [Fact]
        public void Should_Deserialize_Empty_Json_Object_Into_Empty_QueryRequest() {
            // Arrange
            const string json = "{}";

            // Act
            var request = JsonSerializer.Deserialize<QueryRequest>(json, JsonOptions);

            // Assert
            Assert.True(request.IsEmpty);
            Assert.True(request.Q.IsEmpty);
            Assert.True(request.Sort.IsEmpty);
            Assert.Empty(request.Filters);
        }

        [Fact]
        public void Should_Deserialize_Partial_Json_Payloads_Correctly() {
            // Arrange
            const string json = "{\"q\":\"desk\",\"sort\":\"price\"}";

            // Act
            var request = JsonSerializer.Deserialize<QueryRequest>(json, JsonOptions);

            // Assert
            Assert.False(request.IsEmpty);
            Assert.Equal("desk", request.Q.Value);
            Assert.Equal("price", request.Sort.ToString());
            Assert.Empty(request.Filters);
        }
    }
}