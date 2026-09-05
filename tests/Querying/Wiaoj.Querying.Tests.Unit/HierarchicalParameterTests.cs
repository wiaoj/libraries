using Wiaoj.Querying.Extensions;

namespace Wiaoj.Querying.Tests.Unit;

[Trait("Category", "Unit")]
[Trait("Feature", "HierarchicalParameters")]
public sealed class HierarchicalParameterTests {
    private sealed class QuotaEntity {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Limit { get; set; }
    }

    [Fact]
    public void Should_Unignore_Globally_Ignored_Parameter_When_AllowParameters_Configured_On_Schema() {
        // Arrange
        QueryOptions globalOptions = new();
        globalOptions.IgnoredParameters.Add("limit");

        var schema = new QuerySchema<QuotaEntity>()
            .AllowFilter(x => x.Limit)
            .AllowParameters("limit");

        QueryRequest request = new(filters: [
            new FilterConditionNode("limit", QueryOperator.Equal, "100")
        ]);

        // Act
        QueryValidationResult validation = schema.Validate(request, globalOptions);

        // Assert
        Assert.True(validation.IsValid);
        Assert.True(schema.IsParameterAllowed("limit"));
        Assert.False(schema.IsParameterIgnored("limit", globalOptions));
    }

    [Fact]
    public void Should_Apply_LINQ_Filter_For_Unignored_Parameter() {
        // Arrange
        var schema = new QuerySchema<QuotaEntity>()
            .AllowFilter(x => x.Limit)
            .AllowParameters("limit");

        List<QuotaEntity> data = [
            new() { Id = 1, Name = "Basic", Limit = 50 },
            new() { Id = 2, Name = "Pro", Limit = 100 },
            new() { Id = 3, Name = "Enterprise", Limit = 200 }
        ];

        QueryRequest request = new(filters: [
            new FilterConditionNode("limit", QueryOperator.GreaterThanOrEqual, "100")
        ]);

        // Act
        List<QuotaEntity> result = [.. data.AsQueryable().ApplyQuery(request, schema)];

        // Assert
        Assert.Equal(2, result.Count);
        Assert.DoesNotContain(result, x => x.Limit < 100);
    }

    [Fact]
    public void Should_Enforce_Schema_Rules_On_Unignored_Parameter() {
        // Arrange: limit is un-ignored, but only Equal operator is allowed
        var schema = new QuerySchema<QuotaEntity>()
            .AllowFilter(x => x.Limit, QueryOperator.Equal)
            .AllowParameters("limit");

        QueryRequest request = new(filters: [
            new FilterConditionNode("limit", QueryOperator.GreaterThan, "50")
        ]);

        // Act
        QueryValidationResult validation = schema.Validate(request);

        // Assert
        Assert.False(validation.IsValid);
        Assert.Contains(validation.Errors, e => e.ErrorCode == QueryValidationErrorCode.OperatorNotAllowed && e.PropertyName == "limit");
    }

    [Fact]
    public void Should_Bypass_All_Global_Ignored_Parameters_When_IgnoreGlobalParameters_Is_Active() {
        // Arrange: page and size are globally ignored, but schema opts out of global parameters
        QueryOptions globalOptions = new();
        globalOptions.IgnoredParameters.Add("page");
        globalOptions.IgnoredParameters.Add("size");

        var schema = new QuerySchema<QuotaEntity>()
            .AllowFilter(x => x.Name)
            .IgnoreGlobalParameters();

        QueryRequest request = new(filters: [
            new FilterConditionNode("page", QueryOperator.Equal, "1")
        ]);

        // Act
        QueryValidationResult validation = schema.Validate(request, globalOptions);

        // Assert: page is NOT ignored because global options were bypassed, and since page is not in schema, it fails
        Assert.False(validation.IsValid);
        Assert.Contains(validation.Errors, e => e.ErrorCode == QueryValidationErrorCode.FieldNotFilterable && e.PropertyName == "page");
        Assert.True(schema.IgnoresGlobalParameters);
        Assert.False(schema.IsParameterIgnored("page", globalOptions));
    }

    [Fact]
    public void Should_Allow_Case_Insensitive_Parameter_Names_In_AllowParameters() {
        // Arrange
        var schema = new QuerySchema<QuotaEntity>()
            .AllowParameters("LIMIT");

        // Act & Assert
        Assert.True(schema.IsParameterAllowed("limit"));
        Assert.True(schema.IsParameterAllowed("LiMiT"));
        Assert.True(schema.IsParameterAllowed("LIMIT"));
        Assert.False(schema.IsParameterAllowed("other"));
    }

    [Fact]
    public void Should_Prioritize_AllowParameters_Over_IgnoreParameters_Within_Same_Schema() {
        // Arrange: calling IgnoreParameters then AllowParameters
        var schema = new QuerySchema<QuotaEntity>()
            .IgnoreParameters("customField")
            .AllowParameters("customField");

        // Act & Assert
        Assert.True(schema.IsParameterAllowed("customField"));
        Assert.False(schema.IsParameterIgnored("customField"));
    }

    [Fact]
    public void Should_Prioritize_IgnoreParameters_When_Called_After_AllowParameters() {
        // Arrange: calling AllowParameters then IgnoreParameters
        var schema = new QuerySchema<QuotaEntity>()
            .AllowParameters("customField")
            .IgnoreParameters("customField");

        // Act & Assert
        Assert.False(schema.IsParameterAllowed("customField"));
        Assert.True(schema.IsParameterIgnored("customField"));
    }

    [Fact]
    public void Should_Include_Unignored_Parameters_In_MaxFilterCount_Quota() {
        // Arrange: limit is unignored, max filters is 1
        var schema = new QuerySchema<QuotaEntity>()
            .AllowFilter(x => x.Name)
            .AllowFilter(x => x.Limit)
            .AllowParameters("limit")
            .ConfigureLimits(maxFilters: 1, maxInValues: 50, maxSortFields: 5);

        QueryRequest request = new(filters: [
            new FilterConditionNode("name", QueryOperator.Equal, "Test"),
            new FilterConditionNode("limit", QueryOperator.Equal, "100")
        ]);

        // Act
        QueryValidationResult validation = schema.Validate(request);

        // Assert
        Assert.False(validation.IsValid);
        Assert.Contains(validation.Errors, e => e.ErrorCode == QueryValidationErrorCode.MaxFilterCountExceeded);
    }

    [Fact]
    public void Should_Handle_Dirty_Inputs_Gracefully_In_AllowParameters() {
        // Arrange
        var schema = new QuerySchema<QuotaEntity>();

        // Act & Assert: checking null or whitespace names returns false safely
        Assert.False(schema.IsParameterAllowed(null!));
        Assert.False(schema.IsParameterAllowed(string.Empty));
        Assert.False(schema.IsParameterAllowed("   "));

        // Act: passing whitespace/null in span overload does not crash
        schema.AllowParameters("   ", string.Empty, null!);
        Assert.False(schema.IsParameterAllowed(string.Empty));

        // Act: passing dirty enumerable filters out null and whitespace elements
        schema.AllowParameters(["limit", null!, "   ", "size"]);
        Assert.True(schema.IsParameterAllowed("limit"));
        Assert.True(schema.IsParameterAllowed("size"));

        // Act: null enumerable throws ArgumentNullException
        Assert.ThrowsAny<ArgumentNullException>(() => schema.AllowParameters((IEnumerable<string>)null!));
    }

    [Fact]
    public void Should_Implement_IQuerySchemaParameters_Polymorphically() {
        // Arrange
        var schema = new QuerySchema<QuotaEntity>()
            .IgnoreParameters("format")
            .AllowParameters("limit")
            .IgnoreGlobalParameters();

        // Act
        IQuerySchemaParameters parameters = schema;

        // Assert
        Assert.True(parameters.IgnoresGlobalParameters);
        Assert.True(parameters.IsParameterIgnored("format"));
        Assert.False(parameters.IsParameterIgnored("limit"));
        Assert.True(parameters.IsParameterAllowed("limit"));
        Assert.False(parameters.IsParameterAllowed("format"));
    }
}
