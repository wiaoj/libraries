using Wiaoj.Querying.Extensions;

namespace Wiaoj.Querying.Tests.Unit;

/// <summary>
/// Building, splitting, subsetting and merging requests — and applying them strictly.
/// </summary>
/// <remarks>
/// Most assertions here apply the request to data rather than inspect the string it renders to. The failures
/// this code exists to prevent are all of the form "the request parsed, and meant something else", which a
/// string comparison cannot see.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Feature", "Querying")]
[Trait("Component", "Composition")]
public sealed class QueryRequestCompositionTests {

    private enum EntryStatus { Draft, Review, Approved }

    private sealed class Entry {
        public long Id { get; set; }
        public string KeyId { get; set; } = "";
        public string Locale { get; set; } = "";
        public EntryStatus Status { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
    }

    private sealed class EntrySchema : QuerySchema<Entry> {
        public EntrySchema() {
            Property(e => e.KeyId).HasName("keyId").AllowFilter(QueryOperator.In, QueryOperator.Equal).AllowSort();
            Property(e => e.Locale).HasName("locale").AllowFilter(QueryOperator.In, QueryOperator.Equal).AllowSort();
            Property(e => e.Status).HasName("status").AllowFilter(QueryOperator.Equal);
            Property(e => e.UpdatedAt).HasName("updatedAt").AllowFilter(QueryOperator.GreaterThanOrEqual, QueryOperator.Between);
            ConfigureLimits(maxFilters: 10, maxInValues: 3, maxSortFields: 3);
        }
    }

    private static readonly DateTimeOffset T0 = new(2026, 9, 1, 10, 30, 15, 123, TimeSpan.Zero);

    private static readonly List<Entry> Data = [
        new() { Id = 1, KeyId = "k1", Locale = "tr", Status = EntryStatus.Approved, UpdatedAt = T0 },
        new() { Id = 2, KeyId = "k1", Locale = "en", Status = EntryStatus.Draft, UpdatedAt = T0.AddMinutes(1) },
        new() { Id = 3, KeyId = "k2", Locale = "tr", Status = EntryStatus.Review, UpdatedAt = T0.AddMinutes(2) },
        new() { Id = 4, KeyId = "k3", Locale = "de", Status = EntryStatus.Approved, UpdatedAt = T0.AddMinutes(3) }
    ];

    private static readonly EntrySchema Schema = new();

    private static long[] Apply(QueryRequest request) {
        return [.. Data.AsQueryable().ApplyValidatedQuery(request, Schema).Select(e => e.Id).OrderBy(id => id)];
    }

    public sealed class TheBuilder {
        [Fact]
        public void Should_Build_An_In_List_That_Applies_As_Written() {
            QueryRequest request = QueryRequest.CreateBuilder()
                .In("keyId", ["k1", "k2"])
                .In("locale", ["tr"])
                .Build();

            Assert.Equal([1L, 3L], Apply(request));
        }

        [Fact]
        public void Should_Reject_A_List_Value_Containing_The_Separator() {
            // Would otherwise arrive as the two values "a" and "b".
            ArgumentException error = Assert.ThrowsAny<ArgumentException>(
                () => QueryRequest.CreateBuilder().In("keyId", ["a,b"]));

            Assert.Contains("a,b", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void Should_Reject_An_Empty_List() {
            // An empty in-list is ignored when applied, and so matches every row.
            Assert.ThrowsAny<ArgumentException>(() => QueryRequest.CreateBuilder().In("keyId", Array.Empty<string>()));
        }

        [Fact]
        public void An_Empty_Raw_In_List_Does_Match_Every_Row_Which_Is_Why_The_Builder_Refuses_It() {
            // Pins the behaviour the builder guards against, so the guard's reason stays visible.
            QueryRequest raw = new([new FilterConditionNode("keyId", QueryOperator.In, "")]);

            Assert.Equal(Data.Count, Data.AsQueryable().ApplyQuery(raw, Schema).Count());
        }

        [Fact]
        public void Should_Render_Dates_So_They_Survive_To_The_Millisecond() {
            // The invariant general format drops sub-second precision, which would move the bound.
            QueryRequest request = QueryRequest.CreateBuilder()
                .GreaterThanOrEqual("updatedAt", T0.AddMinutes(1))
                .Build();

            Assert.Equal([2L, 3L, 4L], Apply(request));
        }

        [Fact]
        public void Should_Render_A_Range_The_Parser_Reads() {
            QueryRequest request = QueryRequest.CreateBuilder()
                .Between("updatedAt", T0.AddMinutes(1), T0.AddMinutes(2))
                .Build();

            Assert.Equal([2L, 3L], Apply(request));
        }

        [Fact]
        public void Should_Render_Enums_By_Name() {
            QueryRequest request = QueryRequest.CreateBuilder().Equal("status", EntryStatus.Approved).Build();

            Assert.Equal([1L, 4L], Apply(request));
        }

        [Fact]
        public void Should_Keep_Sort_Directives_In_The_Order_Added() {
            QueryRequest request = QueryRequest.CreateBuilder().OrderByDescending("locale").OrderBy("keyId").Build();

            Assert.Equal(["locale", "keyId"], request.Sort.Nodes.Select(n => n.Field));
            Assert.Equal(SortDirection.Descending, request.Sort.Nodes[0].Direction);
        }

    }

    /// <summary>
    /// A <see cref="QueryRequest"/> exists to cross a service boundary inside a contract, and JSON is how most of
    /// those boundaries are crossed. Before the converter, deserialising any request — even one without a sort —
    /// threw, because <c>Sort</c> is a read-only collection.
    /// </summary>
    public sealed class OverJson {
        private static readonly System.Text.Json.JsonSerializerOptions Web = new(System.Text.Json.JsonSerializerDefaults.Web);

        private static QueryRequest RoundTrip(QueryRequest request) {
            string json = System.Text.Json.JsonSerializer.Serialize(request, Web);
            return System.Text.Json.JsonSerializer.Deserialize<QueryRequest>(json, Web);
        }

        [Fact]
        public void Should_Round_Trip_Filters_Sort_And_Search() {
            QueryRequest sent = QueryRequest.CreateBuilder()
                .In("keyId", ["k1", "k3"])
                .Equal("locale", "tr")
                .OrderByDescending("locale")
                .OrderBy("keyId")
                .Search("hello")
                .Build();

            QueryRequest received = RoundTrip(sent);

            Assert.Equal(sent, received);
            Assert.Equal(Apply(sent.Without()), Apply(received.Without()));
        }

        [Fact]
        public void Should_Round_Trip_The_Exact_Shape_The_Workspace_Endpoint_Sends() {
            QueryRequest sent = new([
                new FilterConditionNode("keyId", QueryOperator.In, "k1,k3"),
                new FilterConditionNode("locale", QueryOperator.In, "tr,en")
            ]);

            Assert.Equal(sent, RoundTrip(sent));
        }

        [Fact]
        public void Should_Round_Trip_Unary_Operators() {
            QueryRequest sent = QueryRequest.CreateBuilder().IsNull("keyId").Build();

            Assert.Equal(QueryOperator.IsNull, RoundTrip(sent).Filters.Single().Operator);
        }

        [Fact]
        public void Should_Write_The_Body_Shape_The_Json_Parser_Reads() {
            QueryRequest sent = QueryRequest.CreateBuilder().In("keyId", ["k1"]).OrderByDescending("locale").Build();

            string json = System.Text.Json.JsonSerializer.Serialize(sent, Web);

            Assert.Equal("""{"sort":"-locale","filters":[{"field":"keyId","op":"in","value":"k1"}]}""", json);
            Assert.True(Parsers.JsonQueryParser.TryParse(json, out QueryRequest parsed));
            Assert.Equal(sent, parsed);
        }

        [Fact]
        public void Should_Reject_A_Malformed_Payload_Rather_Than_Read_It_As_Empty() {
            // An empty request applies no filters, so reading garbage as empty would return every row.
            const string malformed = """{"filters":[{"field":"keyId","op":"nope","value":"k1"}]}""";

            Assert.ThrowsAny<System.Text.Json.JsonException>(
                () => System.Text.Json.JsonSerializer.Deserialize<QueryRequest>(malformed, Web));
        }
    }

    public sealed class Subsetting {
        private static readonly QueryRequest Incoming = QueryRequest.CreateBuilder()
            .Equal("locale", "tr")
            .Equal("status", EntryStatus.Approved)
            .OrderBy("locale")
            .OrderBy("keyId")
            .Search("hello")
            .Build();

        [Fact]
        public void Only_Should_Keep_Just_The_Named_Fields_And_Drop_The_Search() {
            QueryRequest only = Incoming.Only("LOCALE");

            Assert.Equal(["locale"], only.Filters.Select(f => f.Field));
            Assert.Equal(["locale"], only.Sort.Nodes.Select(n => n.Field));
            Assert.True(only.Q.IsEmpty);
        }

        [Fact]
        public void Without_Should_Remove_The_Named_Fields_And_Keep_The_Search() {
            QueryRequest without = Incoming.Without("status", "keyId");

            Assert.Equal(["locale"], without.Filters.Select(f => f.Field));
            Assert.Equal(["locale"], without.Sort.Nodes.Select(n => n.Field));
            Assert.Equal("hello", without.Q.Value);
        }
    }

    public sealed class Merging {
        [Fact]
        public void Should_Require_Every_Filter_From_Both() {
            QueryRequest merged = QueryRequest.Merge(
                QueryRequest.CreateBuilder().Equal("locale", "tr").Build(),
                QueryRequest.CreateBuilder().Equal("keyId", "k2").Build());

            Assert.Equal([3L], Apply(merged));
        }

        [Fact]
        public void Two_Filters_On_One_Field_Should_Both_Hold() {
            QueryRequest merged = QueryRequest.Merge(
                QueryRequest.CreateBuilder().Equal("locale", "tr").Build(),
                QueryRequest.CreateBuilder().Equal("locale", "en").Build());

            Assert.Empty(Apply(merged));
        }

        [Fact]
        public void Should_Let_The_First_Sort_Win_And_The_Second_Refine_It() {
            QueryRequest merged = QueryRequest.Merge(
                QueryRequest.CreateBuilder().OrderByDescending("locale").Build(),
                QueryRequest.CreateBuilder().OrderBy("locale").OrderBy("keyId").Build());

            Assert.Equal(["locale", "keyId"], merged.Sort.Nodes.Select(n => n.Field));
            Assert.Equal(SortDirection.Descending, merged.Sort.Nodes[0].Direction);
        }

        [Fact]
        public void Should_Refuse_Two_Different_Search_Terms() {
            Assert.ThrowsAny<ArgumentException>(() => QueryRequest.Merge(
                QueryRequest.CreateBuilder().Search("one").Build(),
                QueryRequest.CreateBuilder().Search("two").Build()));
        }

        [Fact]
        public void Should_Take_The_Search_From_Whichever_Side_Has_One() {
            QueryRequest merged = QueryRequest.Merge(
                QueryRequest.Empty,
                QueryRequest.CreateBuilder().Search("hello").Build());

            Assert.Equal("hello", merged.Q.Value);
        }
    }

    public sealed class Partitioning {
        [Fact]
        public void Should_Split_By_Which_Fields_The_Schema_Declares() {
            QueryRequest incoming = QueryRequest.CreateBuilder()
                .Equal("locale", "tr")
                .Equal("hasScreenshot", true)
                .In("statuses", ["draft", "review"])
                .OrderBy("keyId")
                .Build();

            (QueryRequest owned, QueryRequest remainder) = incoming.Partition(Schema);

            Assert.Equal(["locale"], owned.Filters.Select(f => f.Field));
            Assert.Equal(["keyId"], owned.Sort.Nodes.Select(n => n.Field));
            Assert.Equal(["hasScreenshot", "statuses"], remainder.Filters.Select(f => f.Field));
        }

        [Fact]
        public void Should_Keep_An_Owned_Field_Local_Even_When_Its_Operator_Is_Refused() {
            // locale does not allow Contains. Forwarding it would turn a 400 into a different query elsewhere.
            QueryRequest incoming = new([new FilterConditionNode("locale", QueryOperator.Contains, "t")]);

            (QueryRequest owned, QueryRequest remainder) = incoming.Partition(Schema);

            Assert.Single(owned.Filters);
            Assert.Empty(remainder.Filters);
            Assert.False(Schema.Validate(owned).IsValid);
        }

        [Fact]
        public void The_Owned_Part_Should_Validate_Where_The_Whole_Would_Not() {
            QueryRequest incoming = QueryRequest.CreateBuilder().Equal("locale", "tr").Equal("hasScreenshot", true).Build();

            (QueryRequest owned, _) = incoming.Partition(Schema);

            Assert.False(Schema.Validate(incoming).IsValid);
            Assert.True(Schema.Validate(owned).IsValid);
        }

        [Fact]
        public void Should_Send_The_Search_To_The_Remainder_When_The_Schema_Does_Not_Search() {
            (QueryRequest owned, QueryRequest remainder) =
                QueryRequest.CreateBuilder().Search("hello").Build().Partition(Schema);

            Assert.True(owned.Q.IsEmpty);
            Assert.Equal("hello", remainder.Q.Value);
        }
    }

    /// <summary>
    /// <c>ApplyQuery</c> skips what the schema does not permit, silently. Behind the HTTP validation filter
    /// that is covered; at a service boundary nothing is, and every skip widens the result.
    /// </summary>
    public sealed class StrictApplication {
        [Fact]
        public void ApplyQuery_Should_Silently_Ignore_An_Unknown_Field_And_Return_Everything() {
            // The hazard, pinned: a misspelt field name is not an error, it is no filter at all.
            QueryRequest misspelt = QueryRequest.CreateBuilder().In("keyIds", ["k1"]).Build();

            Assert.Equal(Data.Count, Data.AsQueryable().ApplyQuery(misspelt, Schema).Count());
        }

        [Fact]
        public void ApplyValidatedQuery_Should_Throw_For_The_Same_Request() {
            QueryRequest misspelt = QueryRequest.CreateBuilder().In("keyIds", ["k1"]).Build();

            QueryValidationException error = Assert.Throws<QueryValidationException>(
                () => Data.AsQueryable().ApplyValidatedQuery(misspelt, Schema));

            Assert.NotEmpty(error.Errors);
        }

        [Fact]
        public void ApplyQuery_Should_Silently_Truncate_An_Oversized_In_List() {
            QueryRequest oversized = new([new FilterConditionNode("keyId", QueryOperator.In, "x,y,z,k3")]);

            Assert.Empty(Data.AsQueryable().ApplyQuery(oversized, Schema));
            Assert.Throws<QueryValidationException>(() => Data.AsQueryable().ApplyValidatedQuery(oversized, Schema));
        }
    }
}
