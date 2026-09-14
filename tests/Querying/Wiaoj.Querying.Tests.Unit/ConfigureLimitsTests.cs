using Wiaoj.Preconditions.Exceptions;

namespace Wiaoj.Querying.Tests.Unit;

/// <summary>
/// Zero is a valid filter or sort count — "the caller may send none" — while lengths and list sizes stay positive (#118).
/// </summary>
[Trait("Category", "Unit")]
[Trait("Feature", "Querying")]
[Trait("Component", "Limits")]
public sealed class ConfigureLimitsTests {

    public sealed class Entry {
        public string KeyId { get; set; } = "";
        public string Locale { get; set; } = "";
        public int Order { get; set; }
    }

    private static QuerySchema<Entry> Schema(int maxFilters, int maxSortFields) {
        QuerySchema<Entry> schema = new();
        schema.Property(e => e.KeyId).AllowFilter(QueryOperator.Equal, QueryOperator.In);
        schema.Property(e => e.Locale).AllowFilter(QueryOperator.Equal).AllowSort();
        schema.ConfigureLimits(maxFilters: maxFilters, maxInValues: 200, maxSortFields: maxSortFields, maxFilterValueLength: 8192);
        return schema;
    }

    [Fact]
    public void Should_Accept_Zero_Sort_Fields_And_Zero_Filters() {
        QuerySchema<Entry> schema = Schema(maxFilters: 0, maxSortFields: 0);

        Assert.Equal(0, schema.MaxFilterCount);
        Assert.Equal(0, schema.MaxSortFieldsCount);
    }

    [Fact]
    public void Should_Refuse_Any_Sort_When_Zero_Sort_Fields_Are_Allowed() {
        QueryValidationResult result = Schema(maxFilters: 10, maxSortFields: 0).Validate(QueryRequest.Parse("sort=locale"));

        Assert.Contains(result.Errors, e => e.ErrorCode == QueryValidationErrorCode.MaxSortFieldsCountExceeded);
    }

    [Fact]
    public void Should_Refuse_Any_Filter_When_Zero_Filters_Are_Allowed() {
        QueryValidationResult result = Schema(maxFilters: 0, maxSortFields: 1).Validate(QueryRequest.Parse("keyId=a"));

        Assert.Contains(result.Errors, e => e.ErrorCode == QueryValidationErrorCode.MaxFilterCountExceeded);
    }

    [Fact]
    public void Should_Accept_A_Request_Without_Sort_Or_Filters_When_Both_Are_Zero() {
        Assert.True(Schema(maxFilters: 0, maxSortFields: 0).Validate(QueryRequest.Parse("q=hello")).IsValid);
    }

    [Fact]
    public void Should_Still_Apply_The_Default_Sort_When_The_Caller_May_Not_Sort() {
        QuerySchema<Entry> schema = Schema(maxFilters: 10, maxSortFields: 0);
        schema.DefaultSort(e => e.Order, SortDirection.Descending);

        List<Entry> result = new[] { new Entry { Order = 1 }, new Entry { Order = 3 }, new Entry { Order = 2 } }
            .AsQueryable()
            .ApplyQuery(QueryRequest.Empty, schema)
            .ToList();

        Assert.Equal([3, 2, 1], result.Select(e => e.Order));
    }

    [Theory]
    [InlineData(-1, 1)]
    [InlineData(1, -1)]
    public void Should_Refuse_A_Negative_Count(int maxFilters, int maxSortFields) {
        Assert.Throws<PrecaArgumentOutOfRangeException>(() =>
            new QuerySchema<Entry>().ConfigureLimits(maxFilters, maxInValues: 1, maxSortFields));
    }

    [Theory]
    [InlineData(0, 1, 1)]
    [InlineData(1, 0, 1)]
    [InlineData(1, 1, 0)]
    public void Should_Refuse_Zero_For_A_List_Size_Or_Length(int maxInValues, int maxFilterValueLength, int maxSearchTermLength) {
        // An IN list of at most zero values, or a value of at most zero characters, allows nothing a filter could use.
        Assert.ThrowsAny<ArgumentOutOfRangeException>(() =>
            new QuerySchema<Entry>().ConfigureLimits(1, maxInValues, 1, maxFilterValueLength, maxSearchTermLength));
    }
}
