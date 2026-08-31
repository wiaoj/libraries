using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Wiaoj.Querying.AspNetCore.Binders;

namespace Wiaoj.Querying.AspNetCore.Tests.Unit;

/// <summary>
/// Unit test suite for <see cref="QueryRequestBinder"/> validating HTTP parameter binding,
/// case insensitivity, multi-value combinations, malformed tokens, and edge cases.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Feature", "AspNetCoreBinding")]
public class QueryRequestBinderTests {
    public sealed class BasicAndImplicitBinding : QueryRequestBinderTests {
        [Fact]
        public async Task Should_Bind_Complete_QueryRequest_From_Standard_QueryCollection() {
            // Arrange
            DefaultHttpContext context = new();
            context.Request.Query = new QueryCollection(new Dictionary<string, StringValues> {
                ["q"] = "workstation",
                ["sort"] = "-price,createdAt",
                ["category[eq]"] = "Electronics",
                ["price[gte]"] = "1500"
            });

            // Act
            QueryRequest result = await QueryRequestBinder.BindAsync(context);

            // Assert
            Assert.False(result.IsEmpty);
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

        [Theory]
        [InlineData("status", "Active", "status", QueryOperator.Equal, "Active")]
        [InlineData("category", "Books", "category", QueryOperator.Equal, "Books")]
        [InlineData("isFeatured", "true", "isFeatured", QueryOperator.Equal, "true")]
        public void Should_Bind_Implicit_Equality_When_Brackets_Are_Omitted(
            string key,
            string value,
            string expectedField,
            QueryOperator expectedOp,
            string expectedValue) {
            // Arrange
            DefaultHttpContext context = new();
            context.Request.Query = new QueryCollection(new Dictionary<string, StringValues> {
                [key] = value
            });

            // Act
            QueryRequest result = QueryRequestBinder.BindAsync(context).Result;

            // Assert
            var filter = Assert.Single(result.Filters);
            Assert.Equal(expectedField, filter.Field);
            Assert.Equal(expectedOp, filter.Operator);
            Assert.Equal(expectedValue, filter.RawValue);
        }

        [Fact]
        public async Task Should_Return_Empty_QueryRequest_When_QueryCollection_Is_Empty() {
            // Arrange
            DefaultHttpContext context = new();

            // Act
            QueryRequest result = await QueryRequestBinder.BindAsync(context);

            // Assert
            Assert.True(result.IsEmpty);
            Assert.True(result.Q.IsEmpty);
            Assert.True(result.Sort.IsEmpty);
            Assert.Empty(result.Filters);
        }
    }

    public sealed class EmptyAndExplicitValueHandling : QueryRequestBinderTests {
        [Theory]
        [InlineData("description[eq]", "", "description", QueryOperator.Equal, "")]
        [InlineData("notes", "", "notes", QueryOperator.Equal, "")]
        [InlineData("code[eq]", "   ", "code", QueryOperator.Equal, "")]
        public async Task Should_Parse_Explicit_Empty_Values_As_Empty_String_For_Binary_Operators(
            string key,
            string value,
            string expectedField,
            QueryOperator expectedOp,
            string expectedValue) {
            // Arrange
            DefaultHttpContext context = new();
            context.Request.Query = new QueryCollection(new Dictionary<string, StringValues> {
                [key] = value
            });

            // Act
            QueryRequest result = await QueryRequestBinder.BindAsync(context);

            // Assert
            var filter = Assert.Single(result.Filters);
            Assert.Equal(expectedField, filter.Field);
            Assert.Equal(expectedOp, filter.Operator);
            Assert.Equal(expectedValue, filter.RawValue);
        }

        [Theory]
        [InlineData("deletedAt[isNull]", "")]
        [InlineData("deletedAt[isNull]", null)]
        [InlineData("assignedTo[isNotNull]", "")]
        [InlineData("assignedTo[isNotNull]", null)]
        public async Task Should_Bind_Unary_Null_Operators_With_Null_RawValue(string key, string? value) {
            // Arrange
            DefaultHttpContext context = new();
            context.Request.Query = new QueryCollection(new Dictionary<string, StringValues> {
                [key] = value is null ? StringValues.Empty : new StringValues(value)
            });

            // Act
            QueryRequest result = await QueryRequestBinder.BindAsync(context);

            // Assert
            var filter = Assert.Single(result.Filters);
            Assert.True(filter.IsUnary);
            Assert.Null(filter.RawValue);
        }
    }

    public sealed class CaseInsensitiveParameterBinding : QueryRequestBinderTests {
        [Theory]
        [InlineData("Q", "laptop", "laptop")]
        [InlineData("q", "laptop", "laptop")]
        [InlineData("Sort", "-Price,CreatedAt", "-price,createdAt")]
        [InlineData("SORT", "+PRICE", "price")]
        public async Task Should_Bind_Search_And_Sort_Regardless_Of_Key_Casing(
            string key,
            string value,
            string expectedValue) {
            // Arrange
            DefaultHttpContext context = new();
            context.Request.Query = new QueryCollection(new Dictionary<string, StringValues> {
                [key] = value
            });

            // Act
            QueryRequest result = await QueryRequestBinder.BindAsync(context);

            // Assert
            if(key.Equals("q", StringComparison.OrdinalIgnoreCase)) {
                Assert.Equal(expectedValue, result.Q.Value);
            }
            else {
                Assert.Equal(expectedValue, result.Sort.ToString(), ignoreCase: true);
            }
        }

        [Theory]
        [InlineData("price[GTE]", "300", QueryOperator.GreaterThanOrEqual)]
        [InlineData("price[gte]", "300", QueryOperator.GreaterThanOrEqual)]
        [InlineData("status[EQ]", "Active", QueryOperator.Equal)]
        [InlineData("title[CONTAINS]", "Desk", QueryOperator.Contains)]
        [InlineData("title[NOTCONTAINS]", "Chair", QueryOperator.NotContains)]
        [InlineData("sku[STARTSWITH]", "ABC", QueryOperator.StartsWith)]
        [InlineData("sku[NOTSTARTSWITH]", "XYZ", QueryOperator.NotStartsWith)]
        [InlineData("file[ENDSWITH]", ".pdf", QueryOperator.EndsWith)]
        [InlineData("file[NOTENDSWITH]", ".tmp", QueryOperator.NotEndsWith)]
        [InlineData("deletedAt[ISNULL]", "", QueryOperator.IsNull)]
        [InlineData("deletedAt[ISNOTNULL]", "", QueryOperator.IsNotNull)]
        public async Task Should_Bind_Operators_Regardless_Of_Operator_Casing(
            string key,
            string value,
            QueryOperator expectedOp) {
            // Arrange
            DefaultHttpContext context = new();
            context.Request.Query = new QueryCollection(new Dictionary<string, StringValues> {
                [key] = value
            });

            // Act
            QueryRequest result = await QueryRequestBinder.BindAsync(context);

            // Assert
            var filter = Assert.Single(result.Filters);
            Assert.Equal(expectedOp, filter.Operator);
        }
    }

    public sealed class MultiValueAndCompositeKeys : QueryRequestBinderTests {
        [Fact]
        public async Task Should_Bind_Multiple_Values_Under_Same_Field_Key() {
            // Arrange: Duplicate keys in query string (?status[eq]=Active&status[eq]=Pending)
            DefaultHttpContext context = new();
            context.Request.Query = new QueryCollection(new Dictionary<string, StringValues> {
                ["status[eq]"] = new StringValues(["Active", "Pending", "Archived"])
            });

            // Act
            QueryRequest result = await QueryRequestBinder.BindAsync(context);

            // Assert
            Assert.Equal(3, result.Filters.Count);
            Assert.Equal("Active", result.Filters[0].RawValue);
            Assert.Equal("Pending", result.Filters[1].RawValue);
            Assert.Equal("Archived", result.Filters[2].RawValue);
        }

        [Fact]
        public async Task Should_Bind_Multiple_Different_Operators_For_Same_Property() {
            // Arrange: price >= 100 AND price <= 1000
            DefaultHttpContext context = new();
            context.Request.Query = new QueryCollection(new Dictionary<string, StringValues> {
                ["price[gte]"] = "100",
                ["price[lte]"] = "1000"
            });

            // Act
            QueryRequest result = await QueryRequestBinder.BindAsync(context);

            // Assert
            Assert.Equal(2, result.Filters.Count);
            Assert.Equal("price", result.Filters[0].Field);
            Assert.Equal(QueryOperator.GreaterThanOrEqual, result.Filters[0].Operator);
            Assert.Equal("100", result.Filters[0].RawValue);

            Assert.Equal("price", result.Filters[1].Field);
            Assert.Equal(QueryOperator.LessThanOrEqual, result.Filters[1].Operator);
            Assert.Equal("1000", result.Filters[1].RawValue);
        }

        [Fact]
        public async Task Should_Bind_Deep_Nested_Navigation_Properties() {
            // Arrange: Nested entity property dot paths
            DefaultHttpContext context = new();
            context.Request.Query = new QueryCollection(new Dictionary<string, StringValues> {
                ["company.department.manager.address.city[eq]"] = "Berlin",
                ["items.product.category.code[startsWith]"] = "TECH"
            });

            // Act
            QueryRequest result = await QueryRequestBinder.BindAsync(context);

            // Assert
            Assert.Equal(2, result.Filters.Count);
            Assert.Equal("company.department.manager.address.city", result.Filters[0].Field);
            Assert.Equal("Berlin", result.Filters[0].RawValue);
            Assert.Equal("items.product.category.code", result.Filters[1].Field);
            Assert.Equal("TECH", result.Filters[1].RawValue);
        }
    }

    public sealed class SpecialCharactersAndDelimiters : QueryRequestBinderTests {
        [Theory]
        [InlineData("formula[eq]", "x=y+z", "x=y+z")]
        [InlineData("token[eq]", "dGVzdD1kYXRhPT0=", "dGVzdD1kYXRhPT0=")]
        [InlineData("regex[contains]", "[a-z0-9]+", "[a-z0-9]+")]
        [InlineData("range[between]", "-20.5..150.0", "-20.5..150.0")]
        [InlineData("list[in]", "A,B,C,D", "A,B,C,D")]
        [InlineData("city[eq]", "İstanbul", "İstanbul")]
        [InlineData("currency[eq]", "€", "€")]
        public async Task Should_Preserve_Values_With_Equals_Signs_Brackets_And_Unicode(
            string key,
            string value,
            string expectedValue) {
            // Arrange
            DefaultHttpContext context = new();
            context.Request.Query = new QueryCollection(new Dictionary<string, StringValues> {
                [key] = value
            });

            // Act
            QueryRequest result = await QueryRequestBinder.BindAsync(context);

            // Assert
            var filter = Assert.Single(result.Filters);
            Assert.Equal(expectedValue, filter.RawValue);
        }
    }

    public sealed class MalformedInputResilience : QueryRequestBinderTests {
        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("price[=")]
        [InlineData("price]100")]
        [InlineData("price[gte=100")]
        [InlineData("price[unknownOp]")]
        [InlineData("[eq]")]
        [InlineData("price[]")]
        [InlineData("price[[gte]]")]
        public async Task Should_Silently_Skip_Malformed_Query_Parameters_Without_Exceptions(string malformedKey) {
            // Arrange
            DefaultHttpContext context = new();
            context.Request.Query = new QueryCollection(new Dictionary<string, StringValues> {
                [malformedKey] = "some_value",
                ["validField[eq]"] = "ValidValue"
            });

            // Act
            QueryRequest result = await QueryRequestBinder.BindAsync(context);

            // Assert: Only the valid parameter is bound
            var filter = Assert.Single(result.Filters);
            Assert.Equal("validField", filter.Field);
            Assert.Equal("ValidValue", filter.RawValue);
        }
    }

    public sealed class PreconditionEnforcement : QueryRequestBinderTests {
        [Fact]
        public async Task Should_Throw_ArgumentNullException_When_HttpContext_Is_Null() {
            // Act & Assert
            await Assert.ThrowsAnyAsync<ArgumentNullException>(() => QueryRequestBinder.BindAsync(null!).AsTask());
        }
    }
}