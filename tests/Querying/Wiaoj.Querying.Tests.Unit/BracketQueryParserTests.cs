using Wiaoj.Querying.Parsers;

namespace Wiaoj.Querying.Tests.Unit;
/// <summary>
/// Unit test suite for Stripe/Bracket-style query parser (<c>field[op]=value</c>).
/// </summary>
[Trait("Category", "Unit")]
[Trait("Feature", "BracketParser")]
public class BracketQueryParserTests { 

    /// <summary>
    /// Tests for standard and implicit equality conditions.
    /// </summary>
    public sealed class BasicAndImplicitParsing : BracketQueryParserTests {
        [Fact]
        public void Should_Parse_Explicit_Equality_Condition_When_Input_Is_Valid() {
            // Arrange
            const string input = "status[eq]=Active";

            // Act
            bool isParsed = BracketQueryParser.TryParse(input, out var result);

            // Assert
            Assert.True(isParsed);
            Assert.Equal("status", result.Field);
            Assert.Equal(QueryOperator.Equal, result.Operator);
            Assert.Equal("Active", result.RawValue);
        }

        [Fact]
        public void Should_Default_To_Equal_Operator_When_Brackets_Are_Omitted() {
            // Arrange
            const string input = "status=Active";

            // Act
            bool isParsed = BracketQueryParser.TryParse(input, out var result);

            // Assert
            Assert.True(isParsed);
            Assert.Equal("status", result.Field);
            Assert.Equal(QueryOperator.Equal, result.Operator);
            Assert.Equal("Active", result.RawValue);
        }

        [Theory]
        [InlineData("description[eq]=", "description", QueryOperator.Equal, "")]
        [InlineData("description=", "description", QueryOperator.Equal, "")]
        public void Should_Parse_Empty_Value_As_Empty_String(
            string input,
            string expectedField,
            QueryOperator expectedOperator,
            string expectedValue) {
            // Act
            bool isParsed = BracketQueryParser.TryParse(input, out var result);

            // Assert
            Assert.True(isParsed);
            Assert.Equal(expectedField, result.Field);
            Assert.Equal(expectedOperator, result.Operator);
            Assert.Equal(expectedValue, result.RawValue);
        }
    }

    /// <summary>
    /// Tests for comparison operators with numbers, decimals, negatives, and ISO timestamps.
    /// </summary>
    public sealed class ComparisonOperators : BracketQueryParserTests {
        [Theory]
        [InlineData("price[eq]=100", "price", QueryOperator.Equal, "100")]
        [InlineData("price[neq]=250", "price", QueryOperator.NotEqual, "250")]
        [InlineData("total[gt]=50.5", "total", QueryOperator.GreaterThan, "50.5")]
        [InlineData("temperature[gt]=-15.4", "temperature", QueryOperator.GreaterThan, "-15.4")]
        [InlineData("age[gte]=18", "age", QueryOperator.GreaterThanOrEqual, "18")]
        [InlineData("stock[lt]=5", "stock", QueryOperator.LessThan, "5")]
        [InlineData("discount[lte]=0.2", "discount", QueryOperator.LessThanOrEqual, "0.2")]
        [InlineData("createdAt[gte]=2026-08-30T10:00:00Z", "createdAt", QueryOperator.GreaterThanOrEqual, "2026-08-30T10:00:00Z")]
        [InlineData("expireDate[lt]=2026-12-31", "expireDate", QueryOperator.LessThan, "2026-12-31")]
        public void Should_Parse_All_Comparison_Operators_With_Various_Data_Formats(
            string input,
            string expectedField,
            QueryOperator expectedOperator,
            string expectedValue) {
            // Act
            bool isParsed = BracketQueryParser.TryParse(input, out var result);

            // Assert
            Assert.True(isParsed);
            Assert.Equal(expectedField, result.Field);
            Assert.Equal(expectedOperator, result.Operator);
            Assert.Equal(expectedValue, result.RawValue);
        }
    }

    /// <summary>
    /// Tests for pattern matching operators: contains, startsWith, endsWith.
    /// </summary>
    public sealed class StringPatternOperators : BracketQueryParserTests {
        [Theory]
        [InlineData("name[contains]=john", "name", QueryOperator.Contains, "john")]
        [InlineData("email[startsWith]=admin@", "email", QueryOperator.StartsWith, "admin@")]
        [InlineData("sku[endsWith]=-TR", "sku", QueryOperator.EndsWith, "-TR")]
        [InlineData("path[startsWith]=/api/v1/", "path", QueryOperator.StartsWith, "/api/v1/")]
        [InlineData("url[contains]=https://example.com", "url", QueryOperator.Contains, "https://example.com")]
        public void Should_Parse_String_Operators_With_Special_Characters(
            string input,
            string expectedField,
            QueryOperator expectedOperator,
            string expectedValue) {
            // Act
            bool isParsed = BracketQueryParser.TryParse(input, out var result);

            // Assert
            Assert.True(isParsed);
            Assert.Equal(expectedField, result.Field);
            Assert.Equal(expectedOperator, result.Operator);
            Assert.Equal(expectedValue, result.RawValue);
        }
    }

    /// <summary>
    /// Tests for negative/exclusion pattern operators: notContains, notStartsWith, notEndsWith.
    /// </summary>
    public sealed class StringExclusionOperators : BracketQueryParserTests {
        [Theory]
        [InlineData("title[notContains]=Outlet", "title", QueryOperator.NotContains, "Outlet")]
        [InlineData("name[notContains]=refurbished", "name", QueryOperator.NotContains, "refurbished")]
        [InlineData("sku[notStartsWith]=TEMP-", "sku", QueryOperator.NotStartsWith, "TEMP-")]
        [InlineData("email[notEndsWith]=@spam.com", "email", QueryOperator.NotEndsWith, "@spam.com")]
        [InlineData("file[notEndsWith]=.tmp", "file", QueryOperator.NotEndsWith, ".tmp")]
        public void Should_Parse_String_Exclusion_Operators_Correctly(
            string input,
            string expectedField,
            QueryOperator expectedOperator,
            string expectedValue) {
            // Act
            bool isParsed = BracketQueryParser.TryParse(input, out var result);

            // Assert
            Assert.True(isParsed);
            Assert.Equal(expectedField, result.Field);
            Assert.Equal(expectedOperator, result.Operator);
            Assert.Equal(expectedValue, result.RawValue);
        }
    }

    /// <summary>
    /// Tests for collection and range operators: in, notIn, between, notBetween.
    /// </summary>
    public sealed class CollectionAndRangeOperators : BracketQueryParserTests {
        [Theory]
        [InlineData("status[in]=Active,Pending,Shipped", "status", QueryOperator.In, "Active,Pending,Shipped")]
        [InlineData("roleId[in]=1,2,3,4", "roleId", QueryOperator.In, "1,2,3,4")]
        [InlineData("category[notIn]=Electronics,Clothing", "category", QueryOperator.NotIn, "Electronics,Clothing")]
        [InlineData("tenantId[in]=d3b07384-d113-4a0b-90f7-d4642d991b10", "tenantId", QueryOperator.In, "d3b07384-d113-4a0b-90f7-d4642d991b10")]
        [InlineData("price[between]=100..500", "price", QueryOperator.Between, "100..500")]
        [InlineData("price[notBetween]=100..500", "price", QueryOperator.NotBetween, "100..500")]
        [InlineData("age[notBetween]=18..65", "age", QueryOperator.NotBetween, "18..65")]
        [InlineData("temperature[between]=-20.5..40.0", "temperature", QueryOperator.Between, "-20.5..40.0")]
        [InlineData("date[between]=2026-01-01..2026-12-31", "date", QueryOperator.Between, "2026-01-01..2026-12-31")]
        public void Should_Parse_Collection_And_Range_Operators_Correctly(
            string input,
            string expectedField,
            QueryOperator expectedOperator,
            string expectedValue) {
            // Act
            bool isParsed = BracketQueryParser.TryParse(input, out var result);

            // Assert
            Assert.True(isParsed);
            Assert.Equal(expectedField, result.Field);
            Assert.Equal(expectedOperator, result.Operator);
            Assert.Equal(expectedValue, result.RawValue);
        }
    }

    /// <summary>
    /// Tests for null and presence checks (both unary operators and literal null values).
    /// </summary>
    public sealed class NullAndPresenceChecks : BracketQueryParserTests {
        [Theory]
        [InlineData("deletedAt[isNull]", "deletedAt", QueryOperator.IsNull, null)]
        [InlineData("deletedAt[isNull]=", "deletedAt", QueryOperator.IsNull, null)]
        [InlineData("deletedAt[isNotNull]", "deletedAt", QueryOperator.IsNotNull, null)]
        [InlineData("deletedAt[isNotNull]=", "deletedAt", QueryOperator.IsNotNull, null)]
        public void Should_Parse_Unary_Null_Operators_Without_Value(
            string input,
            string expectedField,
            QueryOperator expectedOperator,
            string? expectedValue) {
            // Act
            bool isParsed = BracketQueryParser.TryParse(input, out var result);

            // Assert
            Assert.True(isParsed);
            Assert.Equal(expectedField, result.Field);
            Assert.Equal(expectedOperator, result.Operator);
            Assert.Equal(expectedValue, result.RawValue);
        }

        [Theory]
        [InlineData("deletedAt[eq]=null", "deletedAt", QueryOperator.Equal, "null")]
        [InlineData("deletedAt[neq]=null", "deletedAt", QueryOperator.NotEqual, "null")]
        [InlineData("deletedAt=null", "deletedAt", QueryOperator.Equal, "null")]
        [InlineData("deletedAt[eq]=NULL", "deletedAt", QueryOperator.Equal, "NULL")]
        public void Should_Parse_Literal_Null_Values_As_Valid_Raw_Values(
            string input,
            string expectedField,
            QueryOperator expectedOperator,
            string expectedValue) {
            // Act
            bool isParsed = BracketQueryParser.TryParse(input, out var result);

            // Assert
            Assert.True(isParsed);
            Assert.Equal(expectedField, result.Field);
            Assert.Equal(expectedOperator, result.Operator);
            Assert.Equal(expectedValue, result.RawValue);
        }
    }

    /// <summary>
    /// Tests for complex values containing special characters, Base64 strings, and Unicode text.
    /// </summary>
    public sealed class ComplexAndUnicodeValues : BracketQueryParserTests {
        [Theory]
        [InlineData("formula[eq]=x=y+z", "formula", QueryOperator.Equal, "x=y+z")]
        [InlineData("token[eq]=dGVzdD1kYXRhPT0=", "token", QueryOperator.Equal, "dGVzdD1kYXRhPT0=")]
        [InlineData("regex[contains]=[a-z0-9]+", "regex", QueryOperator.Contains, "[a-z0-9]+")]
        [InlineData("city[eq]=İstanbul", "city", QueryOperator.Equal, "İstanbul")]
        [InlineData("title[contains]=Çalışma Masası", "title", QueryOperator.Contains, "Çalışma Masası")]
        [InlineData("currency[eq]=€", "currency", QueryOperator.Equal, "€")]
        [InlineData("tag[contains]=C# 14", "tag", QueryOperator.Contains, "C# 14")]
        public void Should_Preserve_Complex_And_Unicode_Values(
            string input,
            string expectedField,
            QueryOperator expectedOperator,
            string expectedValue) {
            // Act
            bool isParsed = BracketQueryParser.TryParse(input, out var result);

            // Assert
            Assert.True(isParsed);
            Assert.Equal(expectedField, result.Field);
            Assert.Equal(expectedOperator, result.Operator);
            Assert.Equal(expectedValue, result.RawValue);
        }
    }

    /// <summary>
    /// Tests for nested / navigation property dot paths.
    /// </summary>
    public sealed class NestedProperties : BracketQueryParserTests {
        [Theory]
        [InlineData("customer.address.city[eq]=Istanbul", "customer.address.city", QueryOperator.Equal, "Istanbul")]
        [InlineData("items.product.category.slug[eq]=electronics", "items.product.category.slug", QueryOperator.Equal, "electronics")]
        [InlineData("a.b.c.d.e[gt]=10", "a.b.c.d.e", QueryOperator.GreaterThan, "10")]
        public void Should_Parse_Deep_Nested_Property_Paths(
            string input,
            string expectedField,
            QueryOperator expectedOperator,
            string expectedValue) {
            // Act
            bool isParsed = BracketQueryParser.TryParse(input, out var result);

            // Assert
            Assert.True(isParsed);
            Assert.Equal(expectedField, result.Field);
            Assert.Equal(expectedOperator, result.Operator);
            Assert.Equal(expectedValue, result.RawValue);
        }
    }

    /// <summary>
    /// Tests for whitespace resilience around fields, brackets, and delimiters.
    /// </summary>
    public sealed class WhitespaceResilience : BracketQueryParserTests {
        [Theory]
        [InlineData("  price[gte]=100  ", "price", QueryOperator.GreaterThanOrEqual, "100")]
        [InlineData("status[eq]=  Active  ", "status", QueryOperator.Equal, "Active")]
        [InlineData("  total = 500  ", "total", QueryOperator.Equal, "500")]
        public void Should_Trim_Surrounding_Whitespace_Gracefully(
            string input,
            string expectedField,
            QueryOperator expectedOperator,
            string expectedValue) {
            // Act
            bool isParsed = BracketQueryParser.TryParse(input, out var result);

            // Assert
            Assert.True(isParsed);
            Assert.Equal(expectedField, result.Field);
            Assert.Equal(expectedOperator, result.Operator);
            Assert.Equal(expectedValue, result.RawValue);
        }
    }

    /// <summary>
    /// Tests for case-insensitivity in operator names (including exclusion operators).
    /// </summary>
    public sealed class CaseInsensitiveOperators : BracketQueryParserTests {
        [Theory]
        [InlineData("price[GTE]=100", "price", QueryOperator.GreaterThanOrEqual, "100")]
        [InlineData("status[EQ]=Active", "status", QueryOperator.Equal, "Active")]
        [InlineData("name[CONTAINS]=john", "name", QueryOperator.Contains, "john")]
        [InlineData("title[NOTCONTAINS]=outlet", "title", QueryOperator.NotContains, "outlet")]
        [InlineData("sku[NOTSTARTSWITH]=temp", "sku", QueryOperator.NotStartsWith, "temp")]
        [InlineData("email[NOTENDSWITH]=.org", "email", QueryOperator.NotEndsWith, ".org")]
        [InlineData("category[NOTIN]=A,B", "category", QueryOperator.NotIn, "A,B")]
        [InlineData("range[NOTBETWEEN]=1..10", "range", QueryOperator.NotBetween, "1..10")]
        [InlineData("deletedAt[ISNULL]", "deletedAt", QueryOperator.IsNull, null)]
        [InlineData("deletedAt[ISNOTNULL]", "deletedAt", QueryOperator.IsNotNull, null)]
        public void Should_Map_Operators_Regardless_Of_Casing(
            string input,
            string expectedField,
            QueryOperator expectedOperator,
            string expectedValue) {
            // Act
            bool isParsed = BracketQueryParser.TryParse(input, out var result);

            // Assert
            Assert.True(isParsed);
            Assert.Equal(expectedField, result.Field);
            Assert.Equal(expectedOperator, result.Operator);
            Assert.Equal(expectedValue, result.RawValue);
        }
    }

    /// <summary>
    /// Tests for malformed inputs, unclosed brackets, missing keys, and invalid tokens.
    /// </summary>
    public sealed class MalformedInputHandling : BracketQueryParserTests {
        [Theory]
        [InlineData("")]                          // Empty input
        [InlineData("   ")]                       // Whitespace only
        [InlineData("=")]                         // Missing key and value
        [InlineData("=100")]                      // Missing key
        [InlineData("price[=")]                   // Unclosed bracket without operator
        [InlineData("price]=100")]                // Missing opening bracket
        [InlineData("price[gte=100")]             // Missing closing bracket
        [InlineData("price[unknown]=100")]        // Unsupported operator
        [InlineData("[eq]=100")]                  // Missing field name
        [InlineData("price[]=100")]               // Empty brackets
        [InlineData("price[[gte]]=100")]          // Double open brackets
        [InlineData("price[gte][extra]=100")]     // Multiple bracket segments
        [InlineData("price[gte]")]                // Non-unary operator missing '=' and value
        [InlineData("price[eq]extra=100")]        // Trailing characters between bracket and '='
        [InlineData("[isNull]")]                  // Unary operator with missing field name
        public void Should_Return_False_When_Input_Is_Malformed(string malformedInput) {
            // Act
            bool isParsed = BracketQueryParser.TryParse(malformedInput, out var result);

            // Assert
            Assert.False(isParsed);
            Assert.Equal(default, result);
        }
    }
}