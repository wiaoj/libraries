using Wiaoj.Querying.Extensions;

namespace Wiaoj.Querying.Tests.Unit;

/// <summary>
/// Comprehensive unit test suite for <see cref="QueryableExtensions.ApplyQuery{T}"/> validating
/// all 18 query operators, multi-column search, multi-field sorting, type conversions, security boundaries,
/// and malformed or edge-case inputs on in-memory queryables.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Feature", "QueryEngine")]
public class QueryableExtensionsTests {
    private enum Status { Active, Pending, Inactive, Archived }

    private sealed class NestedAddress {
        public string City { get; set; } = string.Empty;
        public string? Country { get; set; }
    }

    private sealed class NestedCompany {
        public string Name { get; set; } = string.Empty;
        public NestedAddress Address { get; set; } = new();
    }

    private sealed class Item {
        public long Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public decimal Price { get; set; }
        public decimal? DiscountPrice { get; set; }
        public int Stock { get; set; }
        public Status Status { get; set; }
        public Guid TenantId { get; set; }
        public Guid? OptionalTagId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? DeletedAt { get; set; }
        public NestedCompany Company { get; set; } = new();
    }

    private static readonly Guid TenantA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid TenantB = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static readonly List<Item> SeedItems = [
        new Item {
            Id = 1,
            Name = "Alpha Laptop",
            Description = "High-end gaming workstation",
            Price = 2500m,
            DiscountPrice = 2200m,
            Stock = 10,
            Status = Status.Active,
            TenantId = TenantA,
            OptionalTagId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            CreatedAt = new DateTime(2026, 1, 10, 0, 0, 0, DateTimeKind.Utc),
            DeletedAt = null,
            Company = new NestedCompany { Name = "TechCorp", Address = new NestedAddress { City = "Berlin", Country = "Germany" } }
        },
        new Item {
            Id = 2,
            Name = "Beta Mechanical Keyboard",
            Description = "RGB tactile keyboard",
            Price = 150m,
            DiscountPrice = null,
            Stock = 50,
            Status = Status.Active,
            TenantId = TenantA,
            OptionalTagId = null,
            CreatedAt = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc),
            DeletedAt = null,
            Company = new NestedCompany { Name = "KeyMaster", Address = new NestedAddress { City = "Munich", Country = "Germany" } }
        },
        new Item {
            Id = 3,
            Name = "Gamma Wireless Mouse",
            Description = null,
            Price = 80m,
            DiscountPrice = 70m,
            Stock = 0,
            Status = Status.Pending,
            TenantId = TenantB,
            OptionalTagId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            CreatedAt = new DateTime(2026, 2, 15, 0, 0, 0, DateTimeKind.Utc),
            DeletedAt = new DateTime(2026, 2, 20, 0, 0, 0, DateTimeKind.Utc),
            Company = new NestedCompany { Name = "TechCorp", Address = new NestedAddress { City = "Paris", Country = "France" } }
        },
        new Item {
            Id = 4,
            Name = "Delta Ergonomic Chair",
            Description = "Comfortable office chair",
            Price = 350m,
            DiscountPrice = null,
            Stock = 5,
            Status = Status.Inactive,
            TenantId = TenantB,
            OptionalTagId = null,
            CreatedAt = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc),
            DeletedAt = null,
            Company = new NestedCompany { Name = "FurniGroup", Address = new NestedAddress { City = "London", Country = "UK" } }
        },
        new Item {
            Id = 5,
            Name = "Epsilon Standing Desk",
            Description = "Motorized electric standing desk",
            Price = 700m,
            DiscountPrice = 650m,
            Stock = 2,
            Status = Status.Archived,
            TenantId = TenantA,
            OptionalTagId = null,
            CreatedAt = new DateTime(2026, 3, 10, 0, 0, 0, DateTimeKind.Utc),
            DeletedAt = new DateTime(2026, 3, 15, 0, 0, 0, DateTimeKind.Utc),
            Company = new NestedCompany { Name = "FurniGroup", Address = new NestedAddress { City = "Berlin", Country = null } }
        }
    ];

    private static QuerySchema<Item> CreateDefaultSchema() {
        return new QuerySchema<Item>()
            .AllowFilter(x => x.Id, x => x.Name, x => x.Description)
            .AllowFilter(x => x.Price, x => x.DiscountPrice, x => x.Stock)
            .AllowFilter(x => x.Status, x => x.TenantId, x => x.OptionalTagId)
            .AllowFilter(x => x.CreatedAt, x => x.DeletedAt)
            .AllowFilter(x => x.Company.Name, x => x.Company.Address.City, x => x.Company.Address.Country)
            .AllowSort(x => x.Id, x => x.Price, x => x.Stock)
            .AllowSort(x => x.CreatedAt, x => x.Name)
            .SearchIn(x => x.Name, x => x.Description, x => x.Company.Name);
    }

    public sealed class StandardComparisonOperators : QueryableExtensionsTests {
        [Fact]
        public void Should_Filter_By_Equal_Operator() {
            // Arrange
            QuerySchema<Item> schema = CreateDefaultSchema();
            QueryRequest request = new(filters: [new("Status", QueryOperator.Equal, "Active")]);

            // Act
            List<Item> result = SeedItems.AsQueryable().ApplyQuery(request, schema).ToList();

            // Assert
            Assert.Equal(2, result.Count);
            Assert.All(result, x => Assert.Equal(Status.Active, x.Status));
        }

        [Fact]
        public void Should_Filter_By_NotEqual_Operator() {
            // Arrange
            QuerySchema<Item> schema = CreateDefaultSchema();
            QueryRequest request = new(filters: [new("Status", QueryOperator.NotEqual, "Active")]);

            // Act
            List<Item> result = SeedItems.AsQueryable().ApplyQuery(request, schema).ToList();

            // Assert
            Assert.Equal(3, result.Count);
            Assert.All(result, x => Assert.NotEqual(Status.Active, x.Status));
        }

        [Theory]
        [InlineData(QueryOperator.GreaterThan, "150", 3)]
        [InlineData(QueryOperator.GreaterThanOrEqual, "150", 4)]
        [InlineData(QueryOperator.LessThan, "150", 1)]
        [InlineData(QueryOperator.LessThanOrEqual, "150", 2)]
        public void Should_Filter_By_Numeric_Comparison_Operators(
            QueryOperator op,
            string rawValue,
            int expectedCount) {
            // Arrange
            QuerySchema<Item> schema = CreateDefaultSchema();
            QueryRequest request = new(filters: [new("Price", op, rawValue)]);

            // Act
            List<Item> result = SeedItems.AsQueryable().ApplyQuery(request, schema).ToList();

            // Assert
            Assert.Equal(expectedCount, result.Count);
        }

        [Fact]
        public void Should_Filter_By_DateTime_Comparison_Operator() {
            // Arrange
            QuerySchema<Item> schema = CreateDefaultSchema();
            QueryRequest request = new(filters: [new("CreatedAt", QueryOperator.GreaterThanOrEqual, "2026-03-01T00:00:00Z")]);

            // Act
            List<Item> result = SeedItems.AsQueryable().ApplyQuery(request, schema).ToList();

            // Assert
            Assert.Equal(2, result.Count);
            Assert.All(result, x => Assert.True(x.CreatedAt >= new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc)));
        }

        [Fact]
        public void Should_Filter_By_Guid_Equality() {
            // Arrange
            QuerySchema<Item> schema = CreateDefaultSchema();
            QueryRequest request = new(filters: [new("TenantId", QueryOperator.Equal, TenantB.ToString())]);

            // Act
            List<Item> result = SeedItems.AsQueryable().ApplyQuery(request, schema).ToList();

            // Assert
            Assert.Equal(2, result.Count);
            Assert.All(result, x => Assert.Equal(TenantB, x.TenantId));
        }
    }

    public sealed class StringPatternAndExclusionOperators : QueryableExtensionsTests {
        [Fact]
        public void Should_Filter_By_Contains_Operator() {
            // Arrange
            QuerySchema<Item> schema = CreateDefaultSchema();
            QueryRequest request = new(filters: [new("Name", QueryOperator.Contains, "Desk")]);

            // Act
            List<Item> result = SeedItems.AsQueryable().ApplyQuery(request, schema).ToList();

            // Assert
            Item item = Assert.Single(result);
            Assert.Equal("Epsilon Standing Desk", item.Name);
        }

        [Fact]
        public void Should_Filter_By_NotContains_Operator() {
            // Arrange
            QuerySchema<Item> schema = CreateDefaultSchema();
            QueryRequest request = new(filters: [new("Name", QueryOperator.NotContains, "Desk")]);

            // Act
            List<Item> result = SeedItems.AsQueryable().ApplyQuery(request, schema).ToList();

            // Assert
            Assert.Equal(4, result.Count);
            Assert.DoesNotContain(result, x => x.Name.Contains("Desk"));
        }

        [Fact]
        public void Should_Filter_By_StartsWith_Operator() {
            // Arrange
            QuerySchema<Item> schema = CreateDefaultSchema();
            QueryRequest request = new(filters: [new("Name", QueryOperator.StartsWith, "Alpha")]);

            // Act
            List<Item> result = SeedItems.AsQueryable().ApplyQuery(request, schema).ToList();

            // Assert
            Item item = Assert.Single(result);
            Assert.Equal("Alpha Laptop", item.Name);
        }

        [Fact]
        public void Should_Filter_By_NotStartsWith_Operator() {
            // Arrange
            QuerySchema<Item> schema = CreateDefaultSchema();
            QueryRequest request = new(filters: [new("Name", QueryOperator.NotStartsWith, "Alpha")]);

            // Act
            List<Item> result = SeedItems.AsQueryable().ApplyQuery(request, schema).ToList();

            // Assert
            Assert.Equal(4, result.Count);
            Assert.DoesNotContain(result, x => x.Name.StartsWith("Alpha"));
        }

        [Fact]
        public void Should_Filter_By_EndsWith_Operator() {
            // Arrange
            QuerySchema<Item> schema = CreateDefaultSchema();
            QueryRequest request = new(filters: [new("Name", QueryOperator.EndsWith, "Chair")]);

            // Act
            List<Item> result = SeedItems.AsQueryable().ApplyQuery(request, schema).ToList();

            // Assert
            Item item = Assert.Single(result);
            Assert.Equal("Delta Ergonomic Chair", item.Name);
        }

        [Fact]
        public void Should_Filter_By_NotEndsWith_Operator() {
            // Arrange
            QuerySchema<Item> schema = CreateDefaultSchema();
            QueryRequest request = new(filters: [new("Name", QueryOperator.NotEndsWith, "Chair")]);

            // Act
            List<Item> result = SeedItems.AsQueryable().ApplyQuery(request, schema).ToList();

            // Assert
            Assert.Equal(4, result.Count);
            Assert.DoesNotContain(result, x => x.Name.EndsWith("Chair"));
        }
    }

    public sealed class CollectionAndRangeOperators : QueryableExtensionsTests {
        [Fact]
        public void Should_Filter_By_In_Operator_With_Enums() {
            // Arrange
            QuerySchema<Item> schema = CreateDefaultSchema();
            QueryRequest request = new(filters: [new("Status", QueryOperator.In, "Pending,Archived")]);

            // Act
            List<Item> result = SeedItems.AsQueryable().ApplyQuery(request, schema).ToList();

            // Assert
            Assert.Equal(2, result.Count);
            Assert.All(result, x => Assert.Contains(x.Status, new[] { Status.Pending, Status.Archived }));
        }

        [Fact]
        public void Should_Filter_By_NotIn_Operator() {
            // Arrange
            QuerySchema<Item> schema = CreateDefaultSchema();
            QueryRequest request = new(filters: [new("Status", QueryOperator.NotIn, "Active,Pending")]);

            // Act
            List<Item> result = SeedItems.AsQueryable().ApplyQuery(request, schema).ToList();

            // Assert
            Assert.Equal(2, result.Count);
            Assert.All(result, x => Assert.Contains(x.Status, new[] { Status.Inactive, Status.Archived }));
        }

        [Fact]
        public void Should_Filter_By_Between_Operator_Inclusive() {
            // Arrange: Price between 150 and 700 inclusive
            QuerySchema<Item> schema = CreateDefaultSchema();
            QueryRequest request = new(filters: [new("Price", QueryOperator.Between, "150..700")]);

            // Act
            List<Item> result = SeedItems.AsQueryable().ApplyQuery(request, schema).ToList();

            // Assert: 150, 350, 700 match
            Assert.Equal(3, result.Count);
            Assert.All(result, x => Assert.True(x.Price is >= 150m and <= 700m));
        }

        [Fact]
        public void Should_Filter_By_NotBetween_Operator() {
            // Arrange: Price outside 100..1000
            QuerySchema<Item> schema = CreateDefaultSchema();
            QueryRequest request = new(filters: [new("Price", QueryOperator.NotBetween, "100..1000")]);

            // Act
            List<Item> result = SeedItems.AsQueryable().ApplyQuery(request, schema).ToList();

            // Assert: 80 (<100) and 2500 (>1000) match
            Assert.Equal(2, result.Count);
            Assert.All(result, x => Assert.True(x.Price is < 100m or > 1000m));
        }
    }

    public sealed class NullAndPresenceOperators : QueryableExtensionsTests {
        [Fact]
        public void Should_Filter_Nullable_Value_Types_With_IsNull() {
            // Arrange
            QuerySchema<Item> schema = CreateDefaultSchema();
            QueryRequest request = new(filters: [new("DeletedAt", QueryOperator.IsNull)]);

            // Act
            List<Item> result = SeedItems.AsQueryable().ApplyQuery(request, schema).ToList();

            // Assert
            Assert.Equal(3, result.Count);
            Assert.All(result, x => Assert.Null(x.DeletedAt));
        }

        [Fact]
        public void Should_Filter_Nullable_Value_Types_With_IsNotNull() {
            // Arrange
            QuerySchema<Item> schema = CreateDefaultSchema();
            QueryRequest request = new(filters: [new("DeletedAt", QueryOperator.IsNotNull)]);

            // Act
            List<Item> result = SeedItems.AsQueryable().ApplyQuery(request, schema).ToList();

            // Assert
            Assert.Equal(2, result.Count);
            Assert.All(result, x => Assert.NotNull(x.DeletedAt));
        }

        [Fact]
        public void Should_Filter_Nullable_Strings_With_IsNull() {
            // Arrange
            QuerySchema<Item> schema = CreateDefaultSchema();
            QueryRequest request = new(filters: [new("Description", QueryOperator.IsNull)]);

            // Act
            List<Item> result = SeedItems.AsQueryable().ApplyQuery(request, schema).ToList();

            // Assert
            Item item = Assert.Single(result);
            Assert.Equal("Gamma Wireless Mouse", item.Name);
        }
    }

    public sealed class FreeTextSearch : QueryableExtensionsTests {
        [Fact]
        public void Should_Search_Across_Multiple_Registered_Fields_Using_Or() {
            // Arrange: "FurniGroup" appears in Company.Name of items 4 and 5
            QuerySchema<Item> schema = CreateDefaultSchema();
            QueryRequest request = new(q: new Q("FurniGroup"));

            // Act
            List<Item> result = SeedItems.AsQueryable().ApplyQuery(request, schema).ToList();

            // Assert
            Assert.Equal(2, result.Count);
            Assert.All(result, x => Assert.Equal("FurniGroup", x.Company.Name));
        }

        [Fact]
        public void Should_Ignore_Search_When_Q_Is_Empty() {
            // Arrange
            QuerySchema<Item> schema = CreateDefaultSchema();
            QueryRequest request = new(q: Q.Empty);

            // Act
            List<Item> result = SeedItems.AsQueryable().ApplyQuery(request, schema).ToList();

            // Assert
            Assert.Equal(SeedItems.Count, result.Count);
        }
    }

    public sealed class SortingAndOrdering : QueryableExtensionsTests {
        [Fact]
        public void Should_Sort_Ascending_By_Default() {
            // Arrange
            QuerySchema<Item> schema = CreateDefaultSchema();
            QueryRequest request = new(new Sort("Price"));

            // Act
            List<Item> result = SeedItems.AsQueryable().ApplyQuery(request, schema).ToList();

            // Assert
            Assert.Equal(80m, result.First().Price);
            Assert.Equal(2500m, result.Last().Price);
        }

        [Fact]
        public void Should_Sort_Descending_When_Prefix_Present() {
            // Arrange
            QuerySchema<Item> schema = CreateDefaultSchema();
            QueryRequest request = new(new Sort("-Price"));

            // Act
            List<Item> result = SeedItems.AsQueryable().ApplyQuery(request, schema).ToList();

            // Assert
            Assert.Equal(2500m, result.First().Price);
            Assert.Equal(80m, result.Last().Price);
        }

        [Fact]
        public void Should_Support_Multi_Field_Sorting() {
            // Arrange: Sort by Company.Name (if registered for sort, but using Stock and Price here)
            QuerySchema<Item> schema = CreateDefaultSchema();
            QueryRequest request = new(new Sort("Stock,-Price"));

            // Act
            List<Item> result = SeedItems.AsQueryable().ApplyQuery(request, schema).ToList();

            // Assert
            Assert.Equal(0, result[0].Stock);
            Assert.Equal(2, result[1].Stock);
            Assert.Equal(5, result[2].Stock);
        }
    }

    public sealed class NestedNavigationPaths : QueryableExtensionsTests {
        [Fact]
        public void Should_Filter_By_Deep_Nested_Property() {
            // Arrange
            QuerySchema<Item> schema = CreateDefaultSchema();
            QueryRequest request = new(filters: [new("Company.Address.City", QueryOperator.Equal, "Berlin")]);

            // Act
            List<Item> result = SeedItems.AsQueryable().ApplyQuery(request, schema).ToList();

            // Assert
            Assert.Equal(2, result.Count);
            Assert.All(result, x => Assert.Equal("Berlin", x.Company.Address.City));
        }
    }

    public sealed class SecurityAndBoundaryEnforcement : QueryableExtensionsTests {
        [Fact]
        public void Should_Silently_Ignore_Filters_On_Unregistered_Fields() {
            // Arrange
            QuerySchema<Item> schema = new QuerySchema<Item>().AllowFilter(x => x.Price);
            QueryRequest request = new(filters: [
                new("NonExistentField", QueryOperator.Equal, "123"),
                new("Price", QueryOperator.Equal, "2500")
            ]);

            // Act
            List<Item> result = SeedItems.AsQueryable().ApplyQuery(request, schema).ToList();

            // Assert
            Item item = Assert.Single(result);
            Assert.Equal(2500m, item.Price);
        }

        [Fact]
        public void Should_Silently_Ignore_Disallowed_Operators_For_Property() {
            // Arrange: Price only allows Equal
            QuerySchema<Item> schema = new();
            schema.Property(x => x.Price).AllowFilter(QueryOperator.Equal);

            QueryRequest request = new(filters: [new("Price", QueryOperator.GreaterThan, "100")]);

            // Act
            List<Item> result = SeedItems.AsQueryable().ApplyQuery(request, schema).ToList();

            // Assert: Condition skipped, all items returned
            Assert.Equal(SeedItems.Count, result.Count);
        }

        [Fact]
        public void Should_Enforce_MaxFilterCount_Limit() {
            // Arrange: Limit max filters to 1
            QuerySchema<Item> schema = new QuerySchema<Item>()
                .AllowFilter(x => x.Price, x => x.Status)
                .ConfigureLimits(maxFilters: 1, maxInValues: 50, maxSortFields: 5);

            QueryRequest request = new(filters: [
                new("Price", QueryOperator.GreaterThan, "100"),
                new("Status", QueryOperator.Equal, "Inactive")
            ]);

            // Act: Only the first filter (Price > 100) should be applied
            List<Item> result = SeedItems.AsQueryable().ApplyQuery(request, schema).ToList();

            // Assert: All items with Price > 100 (4 items)
            Assert.Equal(4, result.Count);
        }

        [Fact]
        public void Should_Enforce_MaxSortFields_Limit() {
            // Arrange: Max 1 sort field
            QuerySchema<Item> schema = new QuerySchema<Item>()
                .AllowSort(x => x.Price, x => x.Stock)
                .ConfigureLimits(maxFilters: 20, maxInValues: 50, maxSortFields: 1);

            QueryRequest request = new(new Sort("-Price,Stock"));

            // Act
            List<Item> result = SeedItems.AsQueryable().ApplyQuery(request, schema).ToList();

            // Assert: Primary sort is -Price
            Assert.Equal(2500m, result.First().Price);
        }
    }

    public sealed class EdgeCasesAndMalformedInputs : QueryableExtensionsTests {
        [Fact]
        public void Should_Safely_Ignore_Filter_When_Type_Conversion_Fails() {
            // Arrange: Pass invalid decimal value
            QuerySchema<Item> schema = CreateDefaultSchema();
            QueryRequest request = new(filters: [new("Price", QueryOperator.GreaterThan, "not_a_valid_number")]);

            // Act: Should not crash with FormatException
            List<Item> result = SeedItems.AsQueryable().ApplyQuery(request, schema).ToList();

            // Assert: Condition ignored, returns all items
            Assert.Equal(SeedItems.Count, result.Count);
        }

        [Fact]
        public void Should_Safely_Ignore_Filter_When_Enum_Conversion_Fails() {
            // Arrange: Pass non-existing enum member
            QuerySchema<Item> schema = CreateDefaultSchema();
            QueryRequest request = new(filters: [new("Status", QueryOperator.Equal, "InvalidStatusValue")]);

            // Act
            List<Item> result = SeedItems.AsQueryable().ApplyQuery(request, schema).ToList();

            // Assert
            Assert.Equal(SeedItems.Count, result.Count);
        }

        [Fact]
        public void Should_Safely_Ignore_Filter_When_Guid_Conversion_Fails() {
            // Arrange: Invalid Guid string
            QuerySchema<Item> schema = CreateDefaultSchema();
            QueryRequest request = new(filters: [new("TenantId", QueryOperator.Equal, "not-a-guid")]);

            // Act
            List<Item> result = SeedItems.AsQueryable().ApplyQuery(request, schema).ToList();

            // Assert
            Assert.Equal(SeedItems.Count, result.Count);
        }

        [Fact]
        public void Should_Safely_Ignore_Malformed_Between_Values() {
            // Arrange: Missing upper bound in range
            QuerySchema<Item> schema = CreateDefaultSchema();
            QueryRequest request = new(filters: [new("Price", QueryOperator.Between, "100..")]);

            // Act
            List<Item> result = SeedItems.AsQueryable().ApplyQuery(request, schema).ToList();

            // Assert
            Assert.Equal(SeedItems.Count, result.Count);
        }

        [Fact]
        public void Should_Return_Unmodified_Query_When_Request_Is_Empty() {
            // Arrange
            QuerySchema<Item> schema = CreateDefaultSchema();
            QueryRequest request = QueryRequest.Empty;

            // Act
            List<Item> result = SeedItems.AsQueryable().ApplyQuery(request, schema).ToList();

            // Assert
            Assert.Equal(SeedItems.Count, result.Count);
        }
    }
}