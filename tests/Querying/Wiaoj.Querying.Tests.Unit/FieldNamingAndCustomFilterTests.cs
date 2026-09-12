using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using Wiaoj.Querying.Extensions;

namespace Wiaoj.Querying.Tests.Unit;

/// <summary>
/// Field names that follow the application's naming policy, and filters that are not entity members.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Feature", "Querying")]
[Trait("Component", "Schema")]
public sealed class FieldNamingAndCustomFilterTests {

    public enum Status { Draft, Review, Approved }

    public sealed class Key {
        public int Id { get; set; }
        public string ContentType { get; set; } = "";
        public bool IsDeprecated { get; set; }
        public Status Status { get; set; }
        public int ScreenshotCount { get; set; }
        public string Locale { get; set; } = "";
    }

    private static readonly List<Key> Data = [
        new() { Id = 1, ContentType = "text", IsDeprecated = false, Status = Status.Draft, ScreenshotCount = 0, Locale = "tr" },
        new() { Id = 2, ContentType = "html", IsDeprecated = true, Status = Status.Review, ScreenshotCount = 2, Locale = "en" },
        new() { Id = 3, ContentType = "text", IsDeprecated = false, Status = Status.Approved, ScreenshotCount = 1, Locale = "de" }
    ];

    private static int[] Apply(QuerySchema<Key> schema, QueryRequest request) {
        return [.. Data.AsQueryable().ApplyValidatedQuery(request, schema).Select(k => k.Id).OrderBy(id => id)];
    }

    private static QueryRequest Filter(string field, QueryOperator op, string value) {
        return new QueryRequest([new FilterConditionNode(field, op, value)]);
    }

    public sealed class TheNamingPolicy {
        private static QuerySchema<Key> Schema() {
            QuerySchema<Key> schema = new();
            schema.Property(k => k.ContentType).AllowFilter(QueryOperator.Equal).AllowSort();
            schema.Property(k => k.IsDeprecated).HasName("deprecated").AllowFilter(QueryOperator.Equal);
            return schema;
        }

        [Fact]
        public void Should_Accept_The_Rendered_Name_A_Case_Only_Match_Would_Miss() {
            // content_type does not equal ContentType case-insensitively. Without the alias, a document
            // advertising it would describe a parameter the server rejects.
            QuerySchema<Key> schema = Schema().UseFieldNamingPolicy(JsonNamingPolicy.SnakeCaseLower);

            Assert.Equal([1, 3], Apply(schema, Filter("content_type", QueryOperator.Equal, "text")));
        }

        [Fact]
        public void Should_Keep_Accepting_The_Original_Name() {
            QuerySchema<Key> schema = Schema().UseFieldNamingPolicy(JsonNamingPolicy.SnakeCaseLower);

            Assert.Equal([1, 3], Apply(schema, Filter("ContentType", QueryOperator.Equal, "text")));
        }

        [Fact]
        public void Should_Publish_The_Rendered_Name_And_Leave_Explicit_Names_Alone() {
            QuerySchema<Key> schema = Schema().UseFieldNamingPolicy(JsonNamingPolicy.SnakeCaseLower);

            string[] names = [.. schema.DescribeFields().Select(f => f.Name)];

            Assert.Contains("content_type", names);
            Assert.Contains("deprecated", names);
            Assert.DoesNotContain("ContentType", names);
        }

        [Fact]
        public void Should_Sort_By_The_Rendered_Name() {
            QuerySchema<Key> schema = Schema().UseFieldNamingPolicy(JsonNamingPolicy.SnakeCaseLower);
            QueryRequest request = new(new Sort("-content_type"));

            Assert.True(schema.Validate(request).IsValid);
        }

        [Fact]
        public void Should_Resolve_Rules_Added_After_The_Policy() {
            // Property records are replaced on every builder call; an alias holding one would go stale.
            QuerySchema<Key> schema = new();
            schema.Property(k => k.ScreenshotCount);
            schema.UseFieldNamingPolicy(JsonNamingPolicy.SnakeCaseLower);
            schema.Property(k => k.ScreenshotCount).AllowFilter(QueryOperator.GreaterThan);

            Assert.Equal([2, 3], Apply(schema, Filter("screenshot_count", QueryOperator.GreaterThan, "0")));
        }

        [Fact]
        public void Should_Refuse_A_Policy_That_Renders_Two_Fields_Alike() {
            QuerySchema<Key> schema = new();
            schema.Property(k => k.ContentType).AllowFilter();
            schema.Property(k => k.Locale).HasName("content_type").AllowFilter();

            Assert.Throws<InvalidOperationException>(() => schema.UseFieldNamingPolicy(JsonNamingPolicy.SnakeCaseLower));
        }

        [Fact]
        public void A_Default_Filter_Should_Not_Stack_On_A_Caller_Who_Used_The_Alias() {
            QuerySchema<Key> schema = new();
            schema.Property(k => k.IsDeprecated).AllowFilter(QueryOperator.Equal);
            schema.DefaultFilter(k => k.IsDeprecated, k => !k.IsDeprecated);
            schema.UseFieldNamingPolicy(JsonNamingPolicy.SnakeCaseLower);

            // The caller asked for deprecated keys; the default ("not deprecated") must step aside.
            Assert.Equal([2], Apply(schema, Filter("is_deprecated", QueryOperator.Equal, "true")));
        }

        [Fact]
        public void Should_Be_Applied_By_The_Container_To_Every_Registration_Path() {
            ServiceCollection services = new();
            services.AddQuerying(querying => querying
                .UseFieldNamingPolicy(JsonNamingPolicy.SnakeCaseLower)
                .AddSchema<Key>(schema => schema.Property(k => k.ContentType).AllowFilter()));

            QuerySchema<Key> resolved = services.BuildServiceProvider().GetRequiredService<QuerySchema<Key>>();

            Assert.Equal(JsonNamingPolicy.SnakeCaseLower, resolved.FieldNamingPolicy);
            Assert.True(resolved.Validate(Filter("content_type", QueryOperator.Equal, "text")).IsValid);
        }
    }

    public sealed class CustomFilters {
        private static QuerySchema<Key> Schema() {
            QuerySchema<Key> schema = new();
            schema.Property(k => k.Locale).AllowFilter(QueryOperator.Equal, QueryOperator.In);
            schema.CustomFilter<bool>("hasScreenshot").AllowFilter(QueryOperator.Equal).Describe("Keys with at least one screenshot.");
            schema.CustomFilter<Status>("statuses").AllowFilter(QueryOperator.In);
            return schema;
        }

        [Fact]
        public void Should_Be_Validated_Like_A_Field() {
            QuerySchema<Key> schema = Schema();

            Assert.True(schema.Validate(Filter("hasScreenshot", QueryOperator.Equal, "true")).IsValid);
            Assert.False(schema.Validate(Filter("hasScreenshot", QueryOperator.Equal, "maybe")).IsValid);
            Assert.False(schema.Validate(Filter("hasScreenshot", QueryOperator.In, "true")).IsValid);
            Assert.False(schema.Validate(Filter("statuses", QueryOperator.In, "draft,unknown")).IsValid);
        }

        [Fact]
        public void Should_Not_Be_Applied_By_The_Engine_Without_A_Predicate() {
            // The endpoint owns it: the engine validates and describes it, then leaves the rows alone.
            Assert.Equal([1, 2, 3], Apply(Schema(), Filter("hasScreenshot", QueryOperator.Equal, "true")));
        }

        [Fact]
        public void Should_Hand_The_Endpoint_A_Typed_Value() {
            QueryRequest request = QueryRequest.CreateBuilder()
                .Equal("hasScreenshot", true)
                .In("statuses", [Status.Draft, Status.Approved])
                .Build();

            Assert.True(Schema().TryGetFilterValue(request, "hasScreenshot", out bool hasScreenshot));
            Assert.True(hasScreenshot);
            Assert.Equal([Status.Draft, Status.Approved], Schema().GetFilterValues<Status>(request, "statuses"));
        }

        [Fact]
        public void Should_Report_Absence_Rather_Than_A_Default() {
            Assert.False(Schema().TryGetFilterValue(QueryRequest.Empty, "hasScreenshot", out bool _));
            Assert.Empty(Schema().GetFilterValues<Status>(QueryRequest.Empty, "statuses"));
        }

        [Fact]
        public void Should_Use_The_Parser_It_Was_Given() {
            QuerySchema<Key> schema = new();
            schema.CustomFilter<Status>("statuses")
                .AllowFilter(QueryOperator.In)
                .WithParser(raw => Enum.Parse<Status>(raw.Replace("-", ""), ignoreCase: true));

            QueryRequest request = Filter("statuses", QueryOperator.In, "draft,app-roved");

            Assert.True(schema.Validate(request).IsValid);
            Assert.Equal([Status.Draft, Status.Approved], schema.GetFilterValues<Status>(request, "statuses"));
        }

        [Fact]
        public void Should_Refuse_To_Read_As_A_Different_Type() {
            Assert.Throws<InvalidOperationException>(
                () => Schema().TryGetFilterValue(QueryRequest.Empty, "hasScreenshot", out int _));
        }

        [Fact]
        public void Should_Be_Kept_On_This_Side_When_Partitioning() {
            (QueryRequest owned, QueryRequest remainder) = Filter("hasScreenshot", QueryOperator.Equal, "true").Partition(Schema());

            Assert.Single(owned.Filters);
            Assert.Empty(remainder.Filters);
        }

        [Fact]
        public void Should_Be_Described_With_Its_Type_And_Text() {
            QueryFieldDescriptor field = Schema().DescribeFields().Single(f => f.Name == "hasScreenshot");

            Assert.True(field.IsCustom);
            Assert.Equal(typeof(bool), field.Type);
            Assert.Equal("Keys with at least one screenshot.", field.Description);
        }

        [Fact]
        public void Should_Refuse_Sorting() {
            Assert.Throws<InvalidOperationException>(() => new QuerySchema<Key>().CustomFilter<bool>("hasScreenshot").AllowSort());
        }

        [Fact]
        public void Should_Refuse_A_Name_A_Field_Already_Has() {
            QuerySchema<Key> schema = new();
            schema.Property(k => k.Locale).AllowFilter();

            Assert.Throws<InvalidOperationException>(() => schema.CustomFilter<string>("Locale"));
        }
    }

    public sealed class CustomFiltersWithAPredicate {
        private static QuerySchema<Key> Schema() {
            QuerySchema<Key> schema = new();
            schema.CustomFilter<bool>("hasScreenshot", (key, has) => (key.ScreenshotCount > 0) == has).AllowFilter();
            // A predicate containing && — NotIn must negate the whole per-value result, not rewrite its insides.
            schema.CustomFilter<Status>("liveStatus", (key, status) => key.Status == status && !key.IsDeprecated).AllowFilter();
            return schema;
        }

        [Fact]
        public void Should_Apply_Equality() {
            Assert.Equal([2, 3], Apply(Schema(), Filter("hasScreenshot", QueryOperator.Equal, "true")));
            Assert.Equal([1], Apply(Schema(), Filter("hasScreenshot", QueryOperator.Equal, "false")));
        }

        [Fact]
        public void Should_Apply_Set_Membership_As_Any() {
            Assert.Equal([1, 3], Apply(Schema(), Filter("liveStatus", QueryOperator.In, "draft,approved")));
        }

        [Fact]
        public void Should_Apply_Exclusion_As_None_Even_When_The_Predicate_Contains_And() {
            // liveStatus matches draft (1, live) and approved (3, live). Key 2 is review and deprecated, so it
            // matches neither and is the only one NOT IN (draft, approved).
            Assert.Equal([2], Apply(Schema(), Filter("liveStatus", QueryOperator.NotIn, "draft,approved")));
        }

        [Fact]
        public void Should_Refuse_Operators_A_Predicate_Cannot_Express() {
            Assert.Throws<InvalidOperationException>(() => new QuerySchema<Key>()
                .CustomFilter<int>("minShots", (key, n) => key.ScreenshotCount >= n)
                .AllowFilter(QueryOperator.GreaterThan));
        }
    }
}
