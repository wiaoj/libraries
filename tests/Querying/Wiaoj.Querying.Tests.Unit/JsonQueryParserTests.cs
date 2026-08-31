using System.Text;
using Wiaoj.Querying.Parsers;

namespace Wiaoj.Querying.Tests.Unit;

/// <summary>
/// Comprehensive unit test suite for <see cref="JsonQueryParser"/> validating payload structures,
/// syntax tolerances, operator resolutions, unicode safety, and malformed payload resilience.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Feature", "JsonParser")]
public class JsonQueryParserTests {
    public sealed class BasicAndFullParsing : JsonQueryParserTests {
        [Fact]
        public void Should_Parse_Complete_Json_Query_Payload() {
            // Arrange
            const string json = """
            {
              "q": "workstation",
              "sort": "-price,createdAt",
              "filters": [
                { "field": "category", "op": "eq", "value": "Electronics" },
                { "field": "price", "op": "gte", "value": "1500" }
              ]
            }
            """;

            // Act
            bool parsed = JsonQueryParser.TryParse(json, out QueryRequest result);

            // Assert
            Assert.True(parsed);
            Assert.Equal("workstation", result.Q.Value);
            Assert.Equal("-price,createdAt", result.Sort.ToString());
            Assert.Equal(2, result.Filters.Count);

            Assert.Equal("category", result.Filters[0].Field);
            Assert.Equal(QueryOperator.Equal, result.Filters[0].Operator);
            Assert.Equal("Electronics", result.Filters[0].RawValue);

            Assert.Equal("price", result.Filters[1].Field);
            Assert.Equal(QueryOperator.GreaterThanOrEqual, result.Filters[1].Operator);
            Assert.Equal("1500", result.Filters[1].RawValue);
        }

        [Fact]
        public void Should_Default_To_Equal_When_Operator_Is_Omitted() {
            // Arrange
            const string json = """
            {
              "filters": [
                { "field": "status", "value": "Active" }
              ]
            }
            """;

            // Act
            bool parsed = JsonQueryParser.TryParse(json, out QueryRequest result);

            // Assert
            Assert.True(parsed);
            var filter = Assert.Single(result.Filters);
            Assert.Equal("status", filter.Field);
            Assert.Equal(QueryOperator.Equal, filter.Operator);
            Assert.Equal("Active", filter.RawValue);
        }
    }

    public sealed class StructuralVariationsAndPropertyOrder : JsonQueryParserTests {
        [Fact]
        public void Should_Parse_When_Json_Keys_Are_In_Arbitrary_Order() {
            // Arrange: value and op placed before field, sort placed before q
            const string json = """
            {
              "sort": "createdAt",
              "filters": [
                { "value": "1500", "op": "gt", "field": "price" }
              ],
              "q": "laptop"
            }
            """;

            // Act
            bool parsed = JsonQueryParser.TryParse(json, out QueryRequest result);

            // Assert
            Assert.True(parsed);
            Assert.Equal("laptop", result.Q.Value);
            Assert.Equal("createdAt", result.Sort.ToString());
            var filter = Assert.Single(result.Filters);
            Assert.Equal("price", filter.Field);
            Assert.Equal(QueryOperator.GreaterThan, filter.Operator);
            Assert.Equal("1500", filter.RawValue);
        }

        [Fact]
        public void Should_Ignore_Unrecognized_Top_Level_And_Filter_Properties() {
            // Arrange: Unrecognized metadata, extra schema fields
            const string json = """
            {
              "clientVersion": 2,
              "q": "monitor",
              "filters": [
                { "field": "price", "op": "lte", "value": 500, "clientTag": "ui-filter" }
              ],
              "debugTraceId": "abc-123"
            }
            """;

            // Act
            bool parsed = JsonQueryParser.TryParse(json, out QueryRequest result);

            // Assert
            Assert.True(parsed);
            Assert.Equal("monitor", result.Q.Value);
            var filter = Assert.Single(result.Filters);
            Assert.Equal("price", filter.Field);
            Assert.Equal(QueryOperator.LessThanOrEqual, filter.Operator);
            Assert.Equal("500", filter.RawValue);
        }
    }

    public sealed class SyntaxToleranceAndComments : JsonQueryParserTests {
        [Fact]
        public void Should_Parse_Payload_With_Trailing_Commas() {
            // Arrange: Trailing commas in filters array and filter objects
            const string json = """
            {
              "q": "desk",
              "sort": "price,",
              "filters": [
                { "field": "stock", "op": "gt", "value": 0, },
                { "field": "category", "value": "Furniture", },
              ],
            }
            """;

            // Act
            bool parsed = JsonQueryParser.TryParse(json, out QueryRequest result);

            // Assert
            Assert.True(parsed);
            Assert.Equal("desk", result.Q.Value);
            Assert.Equal(2, result.Filters.Count);
        }

        [Fact]
        public void Should_Parse_Payload_With_Line_And_Block_Comments() {
            // Arrange
            const string json = """
            {
              // Search term for workspace items
              "q": "chair",
              /* Sort directive */
              "sort": "-price",
              "filters": [
                // Active products only
                { "field": "status", "value": "Active" }
              ]
            }
            """;

            // Act
            bool parsed = JsonQueryParser.TryParse(json, out QueryRequest result);

            // Assert
            Assert.True(parsed);
            Assert.Equal("chair", result.Q.Value);
            Assert.Equal("-price", result.Sort.ToString());
            Assert.Single(result.Filters);
        }
    }

    public sealed class CasingAndOperatorResolution : JsonQueryParserTests {
        [Theory]
        [InlineData("EQ", QueryOperator.Equal)]
        [InlineData("neq", QueryOperator.NotEqual)]
        [InlineData("GTE", QueryOperator.GreaterThanOrEqual)]
        [InlineData("gt", QueryOperator.GreaterThan)]
        [InlineData("LTE", QueryOperator.LessThanOrEqual)]
        [InlineData("lt", QueryOperator.LessThan)]
        [InlineData("CONTAINS", QueryOperator.Contains)]
        [InlineData("notcontains", QueryOperator.NotContains)]
        [InlineData("StartsWith", QueryOperator.StartsWith)]
        [InlineData("NOTSTARTSWITH", QueryOperator.NotStartsWith)]
        [InlineData("EndsWith", QueryOperator.EndsWith)]
        [InlineData("notendswith", QueryOperator.NotEndsWith)]
        [InlineData("IN", QueryOperator.In)]
        [InlineData("notin", QueryOperator.NotIn)]
        [InlineData("Between", QueryOperator.Between)]
        [InlineData("NOTBETWEEN", QueryOperator.NotBetween)]
        public void Should_Map_All_Operators_Regardless_Of_Casing(string opText, QueryOperator expectedOp) {
            // Arrange
            string json = $$"""
            {
              "filters": [
                { "field": "testField", "op": "{{opText}}", "value": "sample" }
              ]
            }
            """;

            // Act
            bool parsed = JsonQueryParser.TryParse(json, out QueryRequest result);

            // Assert
            Assert.True(parsed);
            var filter = Assert.Single(result.Filters);
            Assert.Equal(expectedOp, filter.Operator);
        }

        [Fact]
        public void Should_Parse_Keys_With_Uppercase_And_PascalCase() {
            // Arrange
            const string json = """
            {
              "Q": "keyboard",
              "SORT": "price",
              "FILTERS": [
                { "FIELD": "status", "OP": "eq", "VALUE": "Active" }
              ]
            }
            """;

            // Act
            bool parsed = JsonQueryParser.TryParse(json, out QueryRequest result);

            // Assert
            Assert.True(parsed);
            Assert.Equal("keyboard", result.Q.Value);
            Assert.Equal("price", result.Sort.ToString());
            var filter = Assert.Single(result.Filters);
            Assert.Equal("status", filter.Field);
            Assert.Equal("Active", filter.RawValue);
        }
    }

    public sealed class DataTypesAndNumberFormatting : JsonQueryParserTests {
        [Fact]
        public void Should_Parse_Unary_Null_Operators_With_Null_RawValue() {
            // Arrange
            const string json = """
            {
              "filters": [
                { "field": "deletedAt", "op": "isNull" },
                { "field": "assignedTo", "op": "isNotNull" }
              ]
            }
            """;

            // Act
            bool parsed = JsonQueryParser.TryParse(json, out QueryRequest result);

            // Assert
            Assert.True(parsed);
            Assert.Equal(2, result.Filters.Count);

            Assert.Equal("deletedAt", result.Filters[0].Field);
            Assert.Equal(QueryOperator.IsNull, result.Filters[0].Operator);
            Assert.Null(result.Filters[0].RawValue);

            Assert.Equal("assignedTo", result.Filters[1].Field);
            Assert.Equal(QueryOperator.IsNotNull, result.Filters[1].Operator);
            Assert.Null(result.Filters[1].RawValue);
        }

        [Theory]
        [InlineData(1500, "1500")]
        [InlineData(-45.75, "-45.75")]
        [InlineData(0, "0")]
        [InlineData(0.001, "0.001")]
        public void Should_Format_Numeric_Json_Values_To_Invariant_Strings(decimal numericValue, string expectedRaw) {
            // Arrange
            string json = $$"""
            {
              "filters": [
                { "field": "amount", "op": "gte", "value": {{numericValue.ToString(System.Globalization.CultureInfo.InvariantCulture)}} }
              ]
            }
            """;

            // Act
            bool parsed = JsonQueryParser.TryParse(json, out QueryRequest result);

            // Assert
            Assert.True(parsed);
            var filter = Assert.Single(result.Filters);
            Assert.Equal(expectedRaw, filter.RawValue);
        }

        [Theory]
        [InlineData(true, "true")]
        [InlineData(false, "false")]
        public void Should_Format_Boolean_Json_Values_To_Lowercase_Strings(bool boolValue, string expectedRaw) {
            // Arrange
            string json = $$"""
            {
              "filters": [
                { "field": "isArchived", "value": {{boolValue.ToString().ToLowerInvariant()}} }
              ]
            }
            """;

            // Act
            bool parsed = JsonQueryParser.TryParse(json, out QueryRequest result);

            // Assert
            Assert.True(parsed);
            var filter = Assert.Single(result.Filters);
            Assert.Equal(expectedRaw, filter.RawValue);
        }
    }

    public sealed class UnicodeAndSpecialCharacters : JsonQueryParserTests {
        [Fact]
        public void Should_Preserve_Turkish_Characters_And_Surrogate_Pair_Emojis() {
            // Arrange: Turkish characters and emojis
            const string json = """
            {
              "q": "Çalışma Masası",
              "filters": [
                { "field": "şehir", "op": "eq", "value": "İstanbul" },
                { "field": "tag", "op": "contains", "value": "🎉😀" }
              ]
            }
            """;

            // Act
            bool parsed = JsonQueryParser.TryParse(json, out QueryRequest result);

            // Assert
            Assert.True(parsed);
            Assert.Equal("Çalışma Masası", result.Q.Value);
            Assert.Equal("şehir", result.Filters[0].Field);
            Assert.Equal("İstanbul", result.Filters[0].RawValue);
            Assert.Equal("🎉😀", result.Filters[1].RawValue);
        }

        [Theory]
        [InlineData("formula", "x=y+z")]
        [InlineData("token", "dGVzdD1kYXRhPT0=")]
        [InlineData("url", "https://api.example.com/v1/resource?id=123")]
        [InlineData("regex", "^[a-z0-9_]+$")]
        public void Should_Preserve_Special_Characters_And_Delimiters_In_Values(string field, string complexValue) {
            // Arrange
            string json = $$"""
            {
              "filters": [
                { "field": "{{field}}", "value": "{{complexValue}}" }
              ]
            }
            """;

            // Act
            bool parsed = JsonQueryParser.TryParse(json, out QueryRequest result);

            // Assert
            Assert.True(parsed);
            var filter = Assert.Single(result.Filters);
            Assert.Equal(complexValue, filter.RawValue);
        }
    }

    public sealed class NestedPathsAndCollectionDelimiters : JsonQueryParserTests {
        [Fact]
        public void Should_Parse_Deep_Navigation_Property_Paths() {
            // Arrange
            const string json = """
            {
              "filters": [
                { "field": "company.department.manager.address.city", "op": "eq", "value": "Berlin" }
              ]
            }
            """;

            // Act
            bool parsed = JsonQueryParser.TryParse(json, out QueryRequest result);

            // Assert
            Assert.True(parsed);
            var filter = Assert.Single(result.Filters);
            Assert.Equal("company.department.manager.address.city", filter.Field);
        }

        [Fact]
        public void Should_Preserve_Range_And_In_Collection_Syntax_Strings() {
            // Arrange: Between range and IN collection
            const string json = """
            {
              "filters": [
                { "field": "price", "op": "between", "value": "100..500" },
                { "field": "status", "op": "in", "value": "Active,Pending,Archived" }
              ]
            }
            """;

            // Act
            bool parsed = JsonQueryParser.TryParse(json, out QueryRequest result);

            // Assert
            Assert.True(parsed);
            Assert.Equal(2, result.Filters.Count);
            Assert.Equal("100..500", result.Filters[0].RawValue);
            Assert.Equal("Active,Pending,Archived", result.Filters[1].RawValue);
        }
    }

    public sealed class EmptyAndMinimalPayloads : JsonQueryParserTests {
        [Fact]
        public void Should_Return_Empty_Request_For_Empty_Json_Object() {
            // Arrange
            const string json = "{}";

            // Act
            bool parsed = JsonQueryParser.TryParse(json, out QueryRequest result);

            // Assert
            Assert.True(parsed);
            Assert.True(result.IsEmpty);
        }

        [Fact]
        public void Should_Parse_When_Filters_Array_Is_Empty() {
            // Arrange
            const string json = """{"q": "phone", "filters": []}""";

            // Act
            bool parsed = JsonQueryParser.TryParse(json, out QueryRequest result);

            // Assert
            Assert.True(parsed);
            Assert.Equal("phone", result.Q.Value);
            Assert.Empty(result.Filters);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("\t\n")]
        public void Should_Return_False_For_Whitespace_And_Empty_Strings(string emptyInput) {
            // Act
            bool parsed = JsonQueryParser.TryParse(emptyInput, out QueryRequest result);

            // Assert
            Assert.False(parsed);
            Assert.True(result.IsEmpty);
        }
    }

    public sealed class MalformedPayloadResilience : JsonQueryParserTests {
        [Theory]
        [InlineData("{")]                                   // Unclosed object
        [InlineData("}")]                                   // Mismatched closing brace
        [InlineData("[{\"field\": \"price\"}]")]            // Root is array instead of object
        [InlineData("\"just_a_string\"")]                   // Root is primitive string
        [InlineData("12345")]                               // Root is primitive number
        [InlineData("{\"q\": 123}")]                        // q is a number instead of string
        [InlineData("{\"sort\": {}}")]                      // sort is an object instead of string
        [InlineData("{\"filters\": \"not_an_array\"}")]     // filters is not an array
        [InlineData("{\"filters\": [ \"scalar_item\" ]}")]  // filter item is not an object
        [InlineData("{\"filters\": [ { \"op\": \"eq\" } ]}")] // filter item is missing required "field"
        [InlineData("{\"filters\": [ { \"field\": \"   \" } ]}")] // field is whitespace only
        public void Should_Return_False_And_Never_Throw_On_Malformed_Json(string malformedJson) {
            // Act
            bool parsed = JsonQueryParser.TryParse(malformedJson, out QueryRequest result);

            // Assert
            Assert.False(parsed);
            Assert.True(result.IsEmpty);
        }

        [Fact]
        public void Should_Return_False_When_Utf8_Span_Is_Empty() {
            // Arrange
            ReadOnlySpan<byte> emptyUtf8 = [];

            // Act
            bool parsed = JsonQueryParser.TryParse(emptyUtf8, out QueryRequest result);

            // Assert
            Assert.False(parsed);
            Assert.True(result.IsEmpty);
        }
    }
}