namespace Wiaoj.Querying.Tests.Unit;

/// <summary>
/// Comprehensive unit test suite for <see cref="QuerySchema{T}"/> validating whitelist rules, 
/// fine-grained operator restrictions, DoS limits, nested navigation paths, alias lifecycle, 
/// expression rejections, and thread safety.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Feature", "QuerySchema")]
public class QuerySchemaTests {
    private enum Priority { Low, Medium, High }

    private class BaseEntity {
        public long Id { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsDeleted { get; set; }
        public virtual string Code { get; set; } = string.Empty;
    }

    private sealed class Company {
        public string Name { get; set; } = string.Empty;
        public Department Department { get; set; } = new();
    }

    private sealed class Department {
        public string Code { get; set; } = string.Empty;
        public Manager Manager { get; set; } = new();
    }

    private sealed class Manager {
        public Address Address { get; set; } = new();
    }

    private sealed class Address {
        public string City { get; set; } = string.Empty;
        public string? PostalCode { get; set; }
    }

    private class ComplexProduct : BaseEntity {
        public string Title { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public decimal? DiscountPrice { get; set; }
        public int StockCount { get; set; }
        public int? OptionalRating { get; set; }
        public Priority Priority { get; set; }
        public Priority? SecondaryPriority { get; set; }
        public Guid TenantId { get; set; }
        public Guid? AssignedAgentId { get; set; }
        public List<string> Tags { get; set; } = [];
        public Company Company { get; set; } = new();

        public int ComputeValue() {
            return 100;
        }
    }

    private sealed class ShadowedProduct : ComplexProduct {
        // Explicitly shadow base property to test new keyword handling
        public new int Code { get; set; }
    }

    public sealed class InheritanceAndTypeSupport : QuerySchemaTests {
        [Fact]
        public void Should_Support_Properties_Inherited_From_Base_Class() {
            // Arrange & Act
            QuerySchema<ComplexProduct> schema = new QuerySchema<ComplexProduct>()
                .AllowFilter(x => x.Id, x => x.CreatedAt, x => x.IsDeleted)
                .AllowSort(x => x.CreatedAt);

            // Assert
            Assert.True(schema.IsFilterAllowed("Id"));
            Assert.True(schema.IsFilterAllowed("CreatedAt"));
            Assert.True(schema.IsFilterAllowed("IsDeleted"));
            Assert.True(schema.IsSortAllowed("CreatedAt"));
        }

        [Theory]
        [InlineData("DiscountPrice")]
        [InlineData("OptionalRating")]
        [InlineData("SecondaryPriority")]
        [InlineData("AssignedAgentId")]
        public void Should_Properly_Register_Nullable_Value_Types(string propertyName) {
            // Arrange & Act
            QuerySchema<ComplexProduct> schema = new QuerySchema<ComplexProduct>()
                .AllowFilter(x => x.DiscountPrice)
                .AllowFilter(x => x.OptionalRating)
                .AllowFilter(x => x.SecondaryPriority)
                .AllowFilter(x => x.AssignedAgentId);

            // Assert
            Assert.True(schema.IsFilterAllowed(propertyName));
            Assert.True(schema.TryGetProperty(propertyName, out QueryProperty<ComplexProduct>? prop));
            Assert.NotNull(Nullable.GetUnderlyingType(prop.PropertyType));
        }

        [Fact]
        public void Should_Resolve_Shadowed_Property_Type_From_Derived_Class() {
            // Arrange & Act
            QuerySchema<ShadowedProduct> schema = new QuerySchema<ShadowedProduct>()
                .AllowFilter(x => x.Code);

            // Assert
            Assert.True(schema.TryGetProperty("Code", out QueryProperty<ShadowedProduct>? prop));
            Assert.Equal(typeof(int), prop.PropertyType);
        }
    }

    public sealed class NestedNavigationProperties : QuerySchemaTests {
        [Fact]
        public void Should_Extract_Deep_Multi_Level_Navigation_Paths() {
            // Arrange
            const string expectedPath = "Company.Department.Manager.Address.City";

            // Act
            QuerySchema<ComplexProduct> schema = new QuerySchema<ComplexProduct>()
                .AllowFilter(x => x.Company.Department.Manager.Address.City)
                .AllowFilter(x => x.Company.Department.Manager.Address.PostalCode);

            // Assert
            Assert.True(schema.IsFilterAllowed(expectedPath));
            Assert.True(schema.IsFilterAllowed("company.department.manager.address.city"));
            Assert.True(schema.TryGetProperty(expectedPath, out QueryProperty<ComplexProduct>? prop));
            Assert.Equal(expectedPath, prop.MemberName);
        }
    }

    public sealed class RuleMergingAndChaining : QuerySchemaTests {
        [Fact]
        public void Should_Merge_Filter_And_Sort_Rules_When_Configured_Sequentially() {
            // Arrange
            QuerySchema<ComplexProduct> schema = new();

            // Act
            schema.AllowFilter(x => x.Price);
            schema.AllowSort(x => x.Price);

            // Assert
            Assert.True(schema.IsFilterAllowed("Price"));
            Assert.True(schema.IsSortAllowed("Price"));
            Assert.Equal(1, schema.PropertyCount);
        }

        [Fact]
        public void Should_Support_Fluent_Chaining_Across_Multiple_Properties() {
            // Arrange & Act
            QuerySchema<ComplexProduct> schema = new QuerySchema<ComplexProduct>()
                .AllowFilter(x => x.Title, x => x.Price)
                .AllowSort(x => x.Price, x => x.StockCount)
                .Property(x => x.TenantId, p => p.AllowFilter());

            // Assert
            Assert.True(schema.IsFilterAllowed("Title"));
            Assert.True(schema.IsFilterAllowed("Price"));
            Assert.True(schema.IsSortAllowed("Price"));
            Assert.True(schema.IsSortAllowed("StockCount"));
            Assert.False(schema.IsFilterAllowed("StockCount"));
            Assert.True(schema.IsFilterAllowed("TenantId"));
        }
    }

    public sealed class OperatorLevelWhitelisting : QuerySchemaTests {
        [Fact]
        public void Should_Allow_All_Operators_By_Default_When_No_Operators_Specified() {
            // Arrange & Act
            QuerySchema<ComplexProduct> schema = new QuerySchema<ComplexProduct>()
                .AllowFilter(x => x.Price);

            // Assert
            Assert.True(schema.IsFilterAllowed("Price", QueryOperator.Equal));
            Assert.True(schema.IsFilterAllowed("Price", QueryOperator.GreaterThan));
            Assert.True(schema.IsFilterAllowed("Price", QueryOperator.Between));
            Assert.True(schema.IsFilterAllowed("Price", QueryOperator.In));
        }

        [Fact]
        public void Should_Restrict_Filtering_To_Specifically_Allowed_Operators() {
            // Arrange & Act
            QuerySchema<ComplexProduct> schema = new();
            schema.Property(x => x.Price)
                  .AllowFilter(QueryOperator.Equal, QueryOperator.In);

            // Assert
            Assert.True(schema.IsFilterAllowed("Price", QueryOperator.Equal));
            Assert.True(schema.IsFilterAllowed("Price", QueryOperator.In));
            Assert.False(schema.IsFilterAllowed("Price", QueryOperator.GreaterThan));
            Assert.False(schema.IsFilterAllowed("Price", QueryOperator.Between));
        }

        [Fact]
        public void Should_Merge_Allowed_Operators_When_Configured_Multiple_Times() {
            // Arrange
            QuerySchema<ComplexProduct> schema = new();

            // Act
            schema.Property(x => x.Price).AllowFilter(QueryOperator.Equal);
            schema.Property(x => x.Price).AllowFilter(QueryOperator.GreaterThan);

            // Assert
            Assert.True(schema.IsFilterAllowed("Price", QueryOperator.Equal));
            Assert.True(schema.IsFilterAllowed("Price", QueryOperator.GreaterThan));
            Assert.False(schema.IsFilterAllowed("Price", QueryOperator.LessThan));
        }
    }

    public sealed class SecurityLimitsAndConfiguration : QuerySchemaTests {
        [Fact]
        public void Should_Have_Sensible_Default_Security_Limits() {
            // Arrange & Act
            QuerySchema<ComplexProduct> schema = new();

            // Assert
            Assert.Equal(20, schema.MaxFilterCount);
            Assert.Equal(50, schema.MaxInValuesCount);
            Assert.Equal(5, schema.MaxSortFieldsCount);
        }

        [Fact]
        public void Should_Allow_Customizing_Security_Limits() {
            // Arrange
            QuerySchema<ComplexProduct> schema = new();

            // Act
            schema.ConfigureLimits(maxFilters: 10, maxInValues: 25, maxSortFields: 2);

            // Assert
            Assert.Equal(10, schema.MaxFilterCount);
            Assert.Equal(25, schema.MaxInValuesCount);
            Assert.Equal(2, schema.MaxSortFieldsCount);
        }

        [Theory]
        [InlineData(0, 10, 5)]
        [InlineData(-1, 10, 5)]
        [InlineData(10, 0, 5)]
        [InlineData(10, 10, 0)]
        public void Should_Throw_ArgumentOutOfRangeException_When_Limits_Are_Invalid(
            int maxFilters,
            int maxInValues,
            int maxSortFields) {
            // Arrange
            QuerySchema<ComplexProduct> schema = new();

            // Act & Assert
            Assert.ThrowsAny<ArgumentOutOfRangeException>(() =>
                schema.ConfigureLimits(maxFilters, maxInValues, maxSortFields));
        }
    }

    public sealed class EmptyFilterPolicyConfiguration : QuerySchemaTests {
        [Fact]
        public void Should_Have_IgnoreEmptyFilterValues_False_By_Default() {
            // Arrange & Act
            QuerySchema<ComplexProduct> schema = new();

            // Assert
            Assert.False(schema.IgnoreEmptyFilterValues);
        }

        [Fact]
        public void Should_Configure_IgnoreEmptyFilters_Fluently() {
            // Arrange
            QuerySchema<ComplexProduct> schema = new();

            // Act
            schema.IgnoreEmptyFilters(true);

            // Assert
            Assert.True(schema.IgnoreEmptyFilterValues);
        }

        [Fact]
        public void Should_Configure_Property_Level_AllowEmpty() {
            // Arrange
            QuerySchema<ComplexProduct> schema = new();

            // Act
            schema.Property(x => x.Title, p => p.AllowFilter().AllowEmpty(true));

            // Assert
            Assert.True(schema.TryGetProperty("Title", out var prop));
            Assert.True(prop.AllowEmptyString);
        }
    }

    public sealed class AliasMappingAndCollisions : QuerySchemaTests {
        [Fact]
        public void Should_Replace_Previous_Alias_When_Property_Is_Renamed_Sequentially() {
            // Arrange
            QuerySchema<ComplexProduct> schema = new();

            // Act
            schema.Property(x => x.Price, p => p.HasName("cost").AllowFilter());
            schema.Property(x => x.Price, p => p.HasName("unit_cost").AllowSort());

            // Assert
            Assert.False(schema.IsFilterAllowed("Price"));
            Assert.False(schema.IsFilterAllowed("cost"));
            Assert.True(schema.IsFilterAllowed("unit_cost"));
            Assert.True(schema.IsSortAllowed("unit_cost"));
        }

        [Fact]
        public void Should_Throw_InvalidOperationException_When_Alias_Collides_With_Another_Property() {
            // Arrange
            QuerySchema<ComplexProduct> schema = new();
            schema.Property(x => x.Price, p => p.HasName("amount"));

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() =>
                schema.Property(x => x.StockCount, p => p.HasName("amount")));
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void Should_Throw_ArgumentException_When_Alias_Is_Empty_Or_Whitespace(string invalidAlias) {
            // Arrange
            QuerySchema<ComplexProduct> schema = new();

            // Act & Assert
            Assert.ThrowsAny<ArgumentException>(() =>
                schema.Property(x => x.Price, p => p.HasName(invalidAlias)));
        }

        [Fact]
        public void Should_Resolve_Properties_Regardless_Of_Input_Casing() {
            // Arrange
            QuerySchema<ComplexProduct> schema = new QuerySchema<ComplexProduct>()
                .Property(x => x.Title, p => p.HasName("product_title").AllowFilter());

            // Act & Assert
            Assert.True(schema.IsFilterAllowed("PRODUCT_TITLE"));
            Assert.True(schema.IsFilterAllowed("product_title"));
            Assert.True(schema.IsFilterAllowed("Product_Title"));
        }
    }

    public sealed class InvalidExpressionEnforcement : QuerySchemaTests {
        [Fact]
        public void Should_Throw_ArgumentException_When_Selector_Is_Method_Call() {
            // Arrange
            QuerySchema<ComplexProduct> schema = new();

            // Act & Assert
            Assert.Throws<ArgumentException>(() => schema.Property(x => x.ComputeValue()));
        }

        [Fact]
        public void Should_Throw_ArgumentException_When_Selector_Is_Binary_Expression() {
            // Arrange
            QuerySchema<ComplexProduct> schema = new();

            // Act & Assert
            Assert.Throws<ArgumentException>(() => schema.Property(x => x.Price + 10m));
        }

        [Fact]
        public void Should_Throw_ArgumentException_When_Selector_Is_Constant() {
            // Arrange
            QuerySchema<ComplexProduct> schema = new();

            // Act & Assert
            Assert.Throws<ArgumentException>(() => schema.Property(x => 100));
        }

        [Fact]
        public void Should_Throw_ArgumentException_When_Selector_Uses_Indexer() {
            // Arrange
            QuerySchema<ComplexProduct> schema = new();

            // Act & Assert
            Assert.Throws<ArgumentException>(() => schema.Property(x => x.Tags[0]));
        }

        [Fact]
        public void Should_Throw_ArgumentException_When_Selector_Uses_Captured_Closure() {
            // Arrange
            QuerySchema<ComplexProduct> schema = new();
            string externalValue = "test";

            // Act & Assert
            Assert.Throws<ArgumentException>(() => schema.Property(x => externalValue));
        }

        [Fact]
        public void Should_Throw_ArgumentNullException_When_Property_Selector_Is_Null() {
            // Arrange
            QuerySchema<ComplexProduct> schema = new();

            // Act & Assert
            Assert.ThrowsAny<ArgumentNullException>(() => schema.Property<string>(null!));
        }

        [Fact]
        public void Should_Throw_ArgumentNullException_When_Search_Selectors_Are_Null() {
            // Arrange
            QuerySchema<ComplexProduct> schema = new();

            // Act & Assert
            Assert.ThrowsAny<ArgumentNullException>(() => schema.SearchIn(null!));
            Assert.ThrowsAny<ArgumentNullException>(() => schema.SearchIn(x => x.Title, null!));
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(null)]
        public void Should_Return_False_When_Field_Name_Is_Null_Or_Whitespace(string? fieldName) {
            // Arrange
            QuerySchema<ComplexProduct> schema = new QuerySchema<ComplexProduct>().AllowFilter(x => x.Price);

            // Act & Assert
            Assert.False(schema.IsFilterAllowed(fieldName!));
            Assert.False(schema.IsSortAllowed(fieldName!));
            Assert.False(schema.TryGetProperty(fieldName!, out _));
        }
    }

    public sealed class ConcurrencySafety : QuerySchemaTests {
        [Fact]
        public void Should_Allow_Concurrent_Reads_Without_Exceptions() {
            // Arrange
            QuerySchema<ComplexProduct> schema = new QuerySchema<ComplexProduct>()
                .AllowFilter(x => x.Title, x => x.Price, x => x.StockCount)
                .AllowSort(x => x.Price, x => x.CreatedAt);

            // Act & Assert
            Parallel.For(0, 1000, _ => {
                Assert.True(schema.IsFilterAllowed("Price"));
                Assert.True(schema.IsSortAllowed("CreatedAt"));
                Assert.False(schema.IsFilterAllowed("UnregisteredField"));
            });
        }
    }

    public sealed class TextComparisonAndLengthLimits : QuerySchemaTests {
        [Fact]
        public void Should_Have_Case_Insensitive_Text_Comparisons_Enabled_By_Default() {
            // Arrange & Act
            QuerySchema<ComplexProduct> schema = new();

            // Assert
            Assert.True(schema.UseCaseInsensitiveTextComparisons);
        }

        [Fact]
        public void Should_Configure_UseCaseInsensitiveText_Fluently() {
            // Arrange
            QuerySchema<ComplexProduct> schema = new();

            // Act
            schema.UseCaseInsensitiveText(false);

            // Assert
            Assert.False(schema.UseCaseInsensitiveTextComparisons);
        }

        [Fact]
        public void Should_Have_Sensible_Default_Value_Length_Limits() {
            // Arrange & Act
            QuerySchema<ComplexProduct> schema = new();

            // Assert
            Assert.Equal(512, schema.MaxFilterValueLength);
            Assert.Equal(256, schema.MaxSearchTermLength);
        }

        [Fact]
        public void Should_Allow_Customizing_Value_Length_Limits_Via_ConfigureLimits() {
            // Arrange
            QuerySchema<ComplexProduct> schema = new();

            // Act
            schema.ConfigureLimits(maxFilters: 10, maxInValues: 25, maxSortFields: 2, maxFilterValueLength: 64, maxSearchTermLength: 32);

            // Assert
            Assert.Equal(64, schema.MaxFilterValueLength);
            Assert.Equal(32, schema.MaxSearchTermLength);
        }

        [Fact]
        public void ConfigureLimits_Should_Remain_Backward_Compatible_With_Three_Arguments() {
            // Arrange
            QuerySchema<ComplexProduct> schema = new();

            // Act: legacy 3-argument call site should still compile and use new defaults
            schema.ConfigureLimits(maxFilters: 10, maxInValues: 25, maxSortFields: 2);

            // Assert
            Assert.Equal(512, schema.MaxFilterValueLength);
            Assert.Equal(256, schema.MaxSearchTermLength);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-5)]
        public void Should_Throw_ArgumentOutOfRangeException_When_MaxFilterValueLength_Is_Invalid(int invalidLength) {
            // Arrange
            QuerySchema<ComplexProduct> schema = new();

            // Act & Assert
            Assert.ThrowsAny<ArgumentOutOfRangeException>(() =>
                schema.ConfigureLimits(maxFilters: 10, maxInValues: 10, maxSortFields: 5, maxFilterValueLength: invalidLength));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-5)]
        public void Should_Throw_ArgumentOutOfRangeException_When_MaxSearchTermLength_Is_Invalid(int invalidLength) {
            // Arrange
            QuerySchema<ComplexProduct> schema = new();

            // Act & Assert
            Assert.ThrowsAny<ArgumentOutOfRangeException>(() =>
                schema.ConfigureLimits(maxFilters: 10, maxInValues: 10, maxSortFields: 5, maxSearchTermLength: invalidLength));
        }
    }

    public sealed class RequiredAndDefaultRules : QuerySchemaTests {
        [Fact]
        public void Should_Register_RequireFilter_Predicate() {
            // Arrange & Act
            var schema = new QuerySchema<ComplexProduct>()
                .RequireFilter(x => !x.IsDeleted);

            // Assert
            Assert.Single(schema.RequiredFilters);
        }

        [Fact]
        public void Should_Accumulate_Multiple_RequireFilter_Predicates_In_Registration_Order() {
            // Arrange & Act
            var schema = new QuerySchema<ComplexProduct>()
                .RequireFilter(x => !x.IsDeleted)
                .RequireFilter(x => x.Id > 0);

            // Assert
            Assert.Equal(2, schema.RequiredFilters.Count);
        }

        [Fact]
        public void Should_Throw_ArgumentNullException_When_RequireFilter_Predicate_Is_Null() {
            // Arrange
            QuerySchema<ComplexProduct> schema = new();

            // Act & Assert
            Assert.ThrowsAny<ArgumentNullException>(() => schema.RequireFilter(null!));
        }

        [Fact]
        public void Should_Register_DefaultFilter_With_Member_Path() {
            // Arrange & Act
            var schema = new QuerySchema<ComplexProduct>()
                .DefaultFilter(x => x.Priority, x => x.Priority == Priority.Medium);

            // Assert
            var rule = Assert.Single(schema.DefaultFilterRules);
            Assert.Equal("Priority", rule.MemberPath);
        }

        [Fact]
        public void Should_Throw_ArgumentNullException_When_DefaultFilter_Arguments_Are_Null() {
            // Arrange
            QuerySchema<ComplexProduct> schema = new();

            // Act & Assert
            Assert.ThrowsAny<ArgumentNullException>(() => schema.DefaultFilter<decimal>(null!, x => x.Price > 0));
            Assert.ThrowsAny<ArgumentNullException>(() => schema.DefaultFilter(x => x.Price, null!));
        }

        [Fact]
        public void Should_Resolve_DefaultFilter_Exposed_Name_Through_Alias_Applied_Afterward() {
            // Arrange: DefaultFilter registered before HasName is applied to the same property
            var schema = new QuerySchema<ComplexProduct>()
                .DefaultFilter(x => x.Priority, x => x.Priority == Priority.Medium);

            schema.Property(x => x.Priority, p => p.HasName("prio").AllowFilter());

            // Act
            string resolved = schema.ResolveExposedName(schema.DefaultFilterRules[0].MemberPath);

            // Assert: the later alias is still picked up correctly
            Assert.Equal("prio", resolved);
        }

        [Fact]
        public void Should_Fall_Back_To_Member_Path_When_DefaultFilter_Field_Was_Never_Separately_Registered() {
            // Arrange
            var schema = new QuerySchema<ComplexProduct>()
                .DefaultFilter(x => x.Priority, x => x.Priority == Priority.Medium);

            // Act
            string resolved = schema.ResolveExposedName("Priority");

            // Assert
            Assert.Equal("Priority", resolved);
        }

        [Fact]
        public void Should_Register_DefaultSort_Applier() {
            // Arrange & Act
            var schema = new QuerySchema<ComplexProduct>()
                .DefaultSort(x => x.CreatedAt, SortDirection.Descending);

            // Assert
            Assert.Single(schema.DefaultSortAppliers);
        }

        [Fact]
        public void Should_Accumulate_Multiple_DefaultSort_Fields_In_Registration_Order() {
            // Arrange & Act
            var schema = new QuerySchema<ComplexProduct>()
                .DefaultSort(x => x.Priority)
                .DefaultSort(x => x.CreatedAt, SortDirection.Descending);

            // Assert
            Assert.Equal(2, schema.DefaultSortAppliers.Count);
        }

        [Fact]
        public void Should_Throw_ArgumentNullException_When_DefaultSort_Selector_Is_Null() {
            // Arrange
            QuerySchema<ComplexProduct> schema = new();

            // Act & Assert
            Assert.ThrowsAny<ArgumentNullException>(() => schema.DefaultSort<decimal>(null!));
        }
    }

    public sealed class IgnoredParametersConfiguration : QuerySchemaTests {
        [Fact]
        public void Should_Register_Ignored_Parameters_With_Span_Fluently() {
            // Arrange & Act
            var schema = new QuerySchema<ComplexProduct>()
                .IgnoreParameters("page", "size");

            // Assert
            Assert.True(schema.IsParameterIgnored("page"));
            Assert.True(schema.IsParameterIgnored("PAGE"));
            Assert.True(schema.IsParameterIgnored("size"));
            Assert.False(schema.IsParameterIgnored("title"));
        }

        [Fact]
        public void Should_Register_Ignored_Parameters_With_Enumerable() {
            // Arrange
            List<string> list = ["cursor", "limit"];

            // Act
            var schema = new QuerySchema<ComplexProduct>()
                .IgnoreParameters(list);

            // Assert
            Assert.True(schema.IsParameterIgnored("cursor"));
            Assert.True(schema.IsParameterIgnored("limit"));
            Assert.False(schema.IsParameterIgnored("offset"));
        }

        [Fact]
        public void Should_Throw_ArgumentNullException_When_Enumerable_Is_Null() {
            // Arrange
            var schema = new QuerySchema<ComplexProduct>();

            // Act & Assert
            Assert.ThrowsAny<ArgumentNullException>(() => schema.IgnoreParameters((IEnumerable<string>)null!));
        }

        [Fact]
        public void Should_Safely_Handle_Null_And_Whitespace_Inputs() {
            // Arrange
            var schema = new QuerySchema<ComplexProduct>();

            // Act & Assert: checking null or whitespace names returns false safely
            Assert.False(schema.IsParameterIgnored(null!));
            Assert.False(schema.IsParameterIgnored(string.Empty));
            Assert.False(schema.IsParameterIgnored("   "));

            // Act: passing whitespace/null in span overload does not crash
            schema.IgnoreParameters("   ", string.Empty, null!);
            Assert.False(schema.IsParameterIgnored(string.Empty));

            // Act: passing dirty enumerable filters out null and whitespace elements
            schema.IgnoreParameters(["page", null!, "   ", "size"]);
            Assert.True(schema.IsParameterIgnored("page"));
            Assert.True(schema.IsParameterIgnored("size"));
        }
    }
}