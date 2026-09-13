namespace Wiaoj.Querying.Tests.Unit;

/// <summary>
/// Declaring cursor keys on a schema: which fields may be one, how their values survive a cursor, and what a request's
/// sort resolves to.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Feature", "Querying")]
[Trait("Component", "CursorKeys")]
public sealed class QueryCursorKeyTests {

    public enum Status : short { Draft = 1, Live = 7 }

    public readonly record struct Ref(long Value);

    public sealed class Row {
        public long Id { get; set; }
        public int? Rank { get; set; }
        public Ref Reference { get; set; }
        public Status Status { get; set; }
        public decimal Price { get; set; }
        public string Name { get; set; } = "";
    }

    private static QuerySchema<Row> Schema() {
        QuerySchema<Row> schema = new();
        schema.Property(r => r.Status).AsCursor();
        schema.Property(r => r.Price).AsCursor();
        schema.AllowSort(r => r.Name);
        schema.TieBreaker(r => r.Id);
        return schema;
    }

    public sealed class Declaring {
        [Fact]
        public void Should_Refuse_A_Nullable_Value_Type() {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => new QuerySchema<Row>().Property(r => r.Rank).AsCursor());

            Assert.Contains("nullable", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void Should_Refuse_A_Type_Without_A_Built_In_Codec() {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => new QuerySchema<Row>().Property(r => r.Reference).AsCursor());

            Assert.Contains("AsCursor(value => ..., text => ...)", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void Should_Accept_Such_A_Type_With_A_Codec_And_Make_It_Sortable() {
            QuerySchema<Row> schema = new();
            schema.Property(r => r.Reference).AsCursor(r => r.Value.ToString(System.Globalization.CultureInfo.InvariantCulture), t => new Ref(long.Parse(t, System.Globalization.CultureInfo.InvariantCulture)));

            Assert.True(schema.IsSortAllowed("Reference"));
        }
    }

    public sealed class Codecs {
        [Theory]
        [InlineData(typeof(decimal))]
        [InlineData(typeof(double))]
        [InlineData(typeof(DateTimeOffset))]
        [InlineData(typeof(Guid))]
        [InlineData(typeof(Status))]
        [InlineData(typeof(DateOnly))]
        [InlineData(typeof(bool))]
        public void Should_Round_Trip_Exactly(Type type) {
            object value = type switch {
                _ when type == typeof(decimal) => 12345.678901234567890123456789m,
                _ when type == typeof(double) => 0.1 + 0.2,
                _ when type == typeof(DateTimeOffset) => new DateTimeOffset(2026, 9, 13, 10, 11, 12, 345, TimeSpan.FromHours(3)).AddTicks(6789),
                _ when type == typeof(Guid) => Guid.Parse("0f8fad5b-d9cb-469f-a165-70867728950e"),
                _ when type == typeof(Status) => Status.Live,
                _ when type == typeof(DateOnly) => new DateOnly(2026, 9, 13),
                _ => true
            };

            IQueryKeyCodec codec = (IQueryKeyCodec)typeof(BuiltInQueryKeyCodecs).GetMethod(nameof(BuiltInQueryKeyCodecs.For))!
                .MakeGenericMethod(type).Invoke(null, null)!;

            Assert.Equal(value, codec.Decode(codec.Encode(value)));
        }

        [Fact]
        public void Should_Report_Unreadable_Text_As_A_Format_Error() {
            IQueryKeyCodec codec = BuiltInQueryKeyCodecs.For<long>()!;

            Assert.Throws<FormatException>(() => codec.Decode("twelve"));
        }

        [Fact]
        public void A_Custom_Codec_Should_Report_Any_Failure_As_A_Format_Error() {
            QueryKeyCodec<Ref> codec = new(r => r.Value.ToString(System.Globalization.CultureInfo.InvariantCulture), t => throw new InvalidCastException());

            Assert.Throws<FormatException>(() => codec.Decode("1"));
        }
    }

    public sealed class Resolving {
        [Fact]
        public void Should_Append_The_Tie_Breaker_In_The_Direction_Of_The_Last_Key() {
            IReadOnlyList<QueryCursorKey> keys = Schema().ResolveCursorKeys(Sort.Parse("Status,-Price"));

            Assert.Equal(["Status", "Price", "Id"], keys.Select(k => k.Name));
            Assert.Equal([false, true, true], keys.Select(k => k.IsDescending));
        }

        [Fact]
        public void Should_Not_Append_The_Tie_Breaker_Twice_When_Sorted_By_It() {
            QuerySchema<Row> schema = Schema();
            schema.Property(r => r.Id).AsCursor();

            IReadOnlyList<QueryCursorKey> keys = schema.ResolveCursorKeys(Sort.Parse("-Id,Status"));

            Assert.Equal(["Id", "Status"], keys.Select(k => k.Name));
        }

        [Fact]
        public void Should_Collect_Every_Refused_Sort_Field() {
            QueryValidationException error = Assert.Throws<QueryValidationException>(() => Schema().ResolveCursorKeys(Sort.Parse("Name,Missing")));

            Assert.Equal(
                [QueryValidationErrorCode.FieldNotCursorSortable, QueryValidationErrorCode.FieldNotSortable],
                error.Errors.Select(e => e.ErrorCode));
        }

        [Fact]
        public void Should_Use_The_Tie_Breaker_Alone_Without_A_Sort() {
            IReadOnlyList<QueryCursorKey> keys = Schema().ResolveCursorKeys(Sort.Empty);

            QueryCursorKey key = Assert.Single(keys);
            Assert.Equal(("Id", false), (key.Name, key.IsDescending));
        }
    }
}
