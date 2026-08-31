namespace Wiaoj.Querying.Tests.Unit;

/// <summary>
/// Unit test suite for <see cref="QuerySchema{T}.Validate"/> verifying structured validation error codes,
/// boundary breaches, type format errors, and diagnostics dictionary formatting.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Feature", "Validation")]
public class QueryValidationTests {
    private enum Priority { Low, Medium, High }

    private sealed class Product {
        public long Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public Priority Priority { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? Category { get; set; }
    }

    private static QuerySchema<Product> CreateSampleSchema() =>
        new QuerySchema<Product>()
            .AllowFilter(x => x.Name)
            .Property(x => x.Price, p => p.AllowFilter(QueryOperator.Equal, QueryOperator.GreaterThanOrEqual).AllowSort())
            .AllowFilter(x => x.Priority, x => x.CreatedAt)
            .AllowSort(x => x.CreatedAt)
            .ConfigureLimits(maxFilters: 3, maxInValues: 5, maxSortFields: 2);

    public sealed class FilterFieldValidation : QueryValidationTests {
        [Fact]
        public void Should_Pass_When_All_Filter_Fields_Are_Allowed() {
            // Arrange
            var schema = CreateSampleSchema();
            var request = new QueryRequest(filters: [
                FilterConditionNode.Equal("name", "Laptop"),
                FilterConditionNode.GreaterThanOrEqual("price", 1000)
            ]);

            // Act
            QueryValidationResult result = schema.Validate(request);

            // Assert
            Assert.True(result.IsValid);
            Assert.Empty(result.Errors);
        }

        [Fact]
        public void Should_Fail_With_FieldNotFilterable_When_Field_Is_Not_In_Schema() {
            // Arrange: Category is in Product class but NOT registered as filterable in schema
            var schema = CreateSampleSchema();
            var request = new QueryRequest(filters: [
                FilterConditionNode.Equal("Category", "Electronics")
            ]);

            // Act
            QueryValidationResult result = schema.Validate(request);

            // Assert
            Assert.False(result.IsValid);
            var error = Assert.Single(result.Errors);
            Assert.Equal("Category", error.PropertyName);
            Assert.Equal(QueryValidationErrorCode.FieldNotFilterable, error.ErrorCode);
        }
    }

    public sealed class OperatorValidation : QueryValidationTests {
        [Fact]
        public void Should_Fail_With_OperatorNotAllowed_When_Operator_Is_Restricted_By_Bitmask() {
            // Arrange: Price only permits Equal and GreaterThanOrEqual; sending LessThan
            var schema = CreateSampleSchema();
            var request = new QueryRequest(filters: [
                FilterConditionNode.LessThan("price", 500)
            ]);

            // Act
            QueryValidationResult result = schema.Validate(request);

            // Assert
            Assert.False(result.IsValid);
            var error = Assert.Single(result.Errors);
            Assert.Equal("price", error.PropertyName);
            Assert.Equal(QueryValidationErrorCode.OperatorNotAllowed, error.ErrorCode);
        }
    }

    public sealed class ValueFormatValidation : QueryValidationTests {
        [Fact]
        public void Should_Fail_With_InvalidValueFormat_When_Numeric_Parsing_Fails() {
            // Arrange: Non-numeric string for decimal Price
            var schema = CreateSampleSchema();
            var request = new QueryRequest(filters: [
                new("price", QueryOperator.Equal, "not_a_valid_number")
            ]);

            // Act
            QueryValidationResult result = schema.Validate(request);

            // Assert
            Assert.False(result.IsValid);
            var error = Assert.Single(result.Errors);
            Assert.Equal("price", error.PropertyName);
            Assert.Equal(QueryValidationErrorCode.InvalidValueFormat, error.ErrorCode);
            Assert.Equal("not_a_valid_number", error.AttemptedValue);
        }

        [Fact]
        public void Should_Fail_With_InvalidValueFormat_When_Enum_Parsing_Fails() {
            // Arrange: Invalid Priority enum string
            var schema = CreateSampleSchema();
            var request = new QueryRequest(filters: [
                new("priority", QueryOperator.Equal, "InvalidPriority")
            ]);

            // Act
            QueryValidationResult result = schema.Validate(request);

            // Assert
            Assert.False(result.IsValid);
            var error = Assert.Single(result.Errors);
            Assert.Equal("priority", error.PropertyName);
            Assert.Equal(QueryValidationErrorCode.InvalidValueFormat, error.ErrorCode);
        }

        [Theory]
        [InlineData("100..")]
        [InlineData("..500")]
        [InlineData("100")]
        public void Should_Fail_With_MalformedRange_When_Between_Syntax_Is_Invalid(string malformedRange) {
            // Arrange
            var schema = new QuerySchema<Product>().AllowFilter(x => x.Price);
            var request = new QueryRequest(filters: [
                new("price", QueryOperator.Between, malformedRange)
            ]);

            // Act
            QueryValidationResult result = schema.Validate(request);

            // Assert
            Assert.False(result.IsValid);
            var error = Assert.Single(result.Errors);
            Assert.Equal("price", error.PropertyName);
            Assert.Equal(QueryValidationErrorCode.MalformedRange, error.ErrorCode);
        }
    }

    public sealed class SortFieldValidation : QueryValidationTests {
        [Fact]
        public void Should_Fail_With_FieldNotSortable_When_Sort_Field_Is_Not_Allowed() {
            // Arrange: Name is filterable but NOT sortable
            var schema = CreateSampleSchema();
            var request = new QueryRequest(sort: new Sort("Name"));

            // Act
            QueryValidationResult result = schema.Validate(request);

            // Assert
            Assert.False(result.IsValid);
            var error = Assert.Single(result.Errors);
            Assert.Equal("Name", error.PropertyName);
            Assert.Equal(QueryValidationErrorCode.FieldNotSortable, error.ErrorCode);
        }
    }

    public sealed class SecurityLimitsValidation : QueryValidationTests {
        [Fact]
        public void Should_Fail_With_MaxFilterCountExceeded_When_Filter_Limit_Is_Breached() {
            // Arrange: Limit is 3, request contains 4 filters
            var schema = CreateSampleSchema();
            var request = new QueryRequest(filters: [
                FilterConditionNode.Equal("name", "A"),
                FilterConditionNode.Equal("name", "B"),
                FilterConditionNode.Equal("name", "C"),
                FilterConditionNode.Equal("name", "D")
            ]);

            // Act
            QueryValidationResult result = schema.Validate(request);

            // Assert
            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, e => e.ErrorCode == QueryValidationErrorCode.MaxFilterCountExceeded);
        }

        [Fact]
        public void Should_Fail_With_MaxSortFieldsCountExceeded_When_Sort_Limit_Is_Breached() {
            // Arrange: Limit is 2, request contains 3 sort fields
            var schema = CreateSampleSchema();
            var request = new QueryRequest(sort: new Sort("price,createdAt,name"));

            // Act
            QueryValidationResult result = schema.Validate(request);

            // Assert
            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, e => e.ErrorCode == QueryValidationErrorCode.MaxSortFieldsCountExceeded);
        }

        [Fact]
        public void Should_Fail_With_MaxInValuesCountExceeded_When_In_Collection_Exceeds_Bound() {
            // Arrange: Limit is 5 items, request provides 6 values
            var schema = CreateSampleSchema();
            var request = new QueryRequest(filters: [
                FilterConditionNode.In("priority", "Low,Medium,High,Low,Medium,High")
            ]);

            // Act
            QueryValidationResult result = schema.Validate(request);

            // Assert
            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, e => e.ErrorCode == QueryValidationErrorCode.MaxInValuesCountExceeded);
        }
    }

    public sealed class OverallResultAndFormatting : QueryValidationTests {
        [Fact]
        public void ToDictionary_Should_Group_Errors_By_Property_Name_For_ValidationProblem_Compatibility() {
            // Arrange
            var schema = CreateSampleSchema();
            var request = new QueryRequest(
                sort: new Sort("UnregisteredSortField"),
                filters: [
                    new("price", QueryOperator.Equal, "invalid_num"),
                    FilterConditionNode.Equal("UnregisteredField", "value")
                ]);

            // Act
            QueryValidationResult result = schema.Validate(request);
            Dictionary<string, string[]> errorDict = result.ToDictionary();

            // Assert
            Assert.False(result.IsValid);
            Assert.True(errorDict.ContainsKey("price"));
            Assert.True(errorDict.ContainsKey("UnregisteredField"));
            Assert.True(errorDict.ContainsKey("UnregisteredSortField"));
        }
    }
}