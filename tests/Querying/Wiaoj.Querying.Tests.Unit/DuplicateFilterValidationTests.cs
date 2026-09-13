using System.Text.Json;

namespace Wiaoj.Querying.Tests.Unit;

/// <summary>
/// A field filtered with the same operator more than once is refused: the repeated values have no single meaning.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Feature", "Querying")]
[Trait("Component", "Validation")]
public sealed class DuplicateFilterValidationTests {

    public sealed class Memory {
        public long UsageCount { get; set; }
        public string TargetLocale { get; set; } = "";
    }

    private static QuerySchema<Memory> Schema() {
        QuerySchema<Memory> schema = new();
        schema.AllowFilter(m => m.UsageCount, QueryOperator.Equal, QueryOperator.GreaterThanOrEqual, QueryOperator.LessThanOrEqual, QueryOperator.IsNull);
        schema.Property(m => m.TargetLocale).AllowFilter(QueryOperator.Equal, QueryOperator.In).HasName("locale");
        return schema;
    }

    private static QueryValidationResult Validate(string query, QuerySchema<Memory>? schema = null, QueryOptions? options = null) {
        IReadOnlyList<QueryValidationError> errors = (schema ?? Schema()).FindDuplicateFilters(QueryRequest.Parse(query), options);
        return errors.Count == 0 ? QueryValidationResult.Success : new QueryValidationResult(errors);
    }

    [Fact]
    public void Should_Refuse_The_Same_Field_And_Operator_Written_In_Two_Casings() {
        // The request that returned only usageCount >= 4 while the client believed it asked for >= 2.
        QueryValidationResult result = Validate("UsageCount[gte]=4&usageCount[gte]=2");

        QueryValidationError error = Assert.Single(result.Errors);
        Assert.Equal(QueryValidationErrorCode.DuplicateFilter, error.ErrorCode);
        Assert.Equal("usageCount", error.PropertyName);
        Assert.Contains("also as 'UsageCount'", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Should_Refuse_The_Same_Field_Repeated_Exactly() {
        Assert.Equal(QueryValidationErrorCode.DuplicateFilter, Assert.Single(Validate("UsageCount=4&UsageCount=5").Errors).ErrorCode);
    }

    [Fact]
    public void Should_Refuse_A_Field_And_Its_Naming_Policy_Alias_With_The_Same_Operator() {
        QueryValidationResult result = Validate("usage_count=1&UsageCount=2", Schema().UseFieldNamingPolicy(JsonNamingPolicy.SnakeCaseLower));

        Assert.Equal(QueryValidationErrorCode.DuplicateFilter, Assert.Single(result.Errors).ErrorCode);
    }

    [Fact]
    public void Should_Treat_Equality_Shorthand_And_Explicit_Eq_As_The_Same_Operator() {
        Assert.Equal(QueryValidationErrorCode.DuplicateFilter, Assert.Single(Validate("UsageCount=4&UsageCount[eq]=4").Errors).ErrorCode);
    }

    [Fact]
    public void Should_Refuse_A_Repeated_Unary_Operator() {
        Assert.Equal(QueryValidationErrorCode.DuplicateFilter, Assert.Single(Validate("UsageCount[isnull]&UsageCount[isnull]").Errors).ErrorCode);
    }

    [Fact]
    public void Should_Allow_Different_Operators_On_One_Field_For_A_Range() {
        Assert.True(Validate("UsageCount[gte]=2&UsageCount[lte]=10").IsValid);
    }

    [Fact]
    public void Should_Allow_The_Same_Operator_On_Different_Fields() {
        Assert.True(Validate("UsageCount=2&locale=tr-TR").IsValid);
    }

    [Fact]
    public void Should_Not_Count_Ignored_Parameters() {
        QueryOptions options = new();
        options.IgnoredParameters.Add("UsageCount");

        Assert.True(Validate("UsageCount=4&UsageCount=5", options: options).IsValid);
    }

    [Fact]
    public void Should_Not_Be_Part_Of_Schema_Validation_So_A_Merged_Request_Still_Applies() {
        // Merge narrowing a caller's filter with a server-side one repeats the field on purpose, and means both.
        QueryRequest merged = QueryRequest.Merge(QueryRequest.Parse("UsageCount[gte]=2"), QueryRequest.Parse("UsageCount[gte]=4"));
        Memory[] rows = [new() { UsageCount = 2 }, new() { UsageCount = 4 }];

        Assert.True(Schema().Validate(merged).IsValid);
        Assert.Equal([4L], rows.AsQueryable().ApplyValidatedQuery(merged, Schema()).Select(m => m.UsageCount));
    }
}
