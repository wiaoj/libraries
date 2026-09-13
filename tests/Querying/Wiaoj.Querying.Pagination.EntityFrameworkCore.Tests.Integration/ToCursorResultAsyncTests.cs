using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Wiaoj.Pagination;
using Wiaoj.Querying.Pagination.EntityFrameworkCore.Tests.Integration.Fixtures;

namespace Wiaoj.Querying.Pagination.EntityFrameworkCore.Tests.Integration;

/// <summary>
/// Keyset pages that seek on the sort the caller chose (#81, #73): the cursor carries the sort keys and the tie-breaker,
/// records which sort it belongs to, and is refused under any other.
/// </summary>
/// <remarks>
/// Ties are traversed under descending orders on purpose. SQLite returns tied rows in rowid order, which is the
/// ascending id, so an ascending traversal can come out right without a working tie-breaker; a descending one cannot.
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Feature", "Querying")]
[Trait("Component", "Pagination")]
public sealed class ToCursorResultAsyncTests : IAsyncLifetime {
    private AssetContext _db = null!;
    private SqliteConnection _connection = null!;
    private CommandRecorder _recorder = null!;

    // Priority ties in three groups, seeded out of id order. Id 10 is deleted.
    private static readonly (long Id, int Priority, long Size)[] Rows = [
        (4, 2, 300), (1, 1, 100), (7, 3, 300), (2, 2, 200), (9, 1, 100),
        (5, 3, 200), (3, 2, 400), (8, 1, 400), (6, 3, 500), (10, 1, 999)
    ];

    private static readonly IEnumerable<(long Id, int Priority, long Size)> Live = Rows.Where(r => r.Id != 10);

    public async ValueTask InitializeAsync() {
        (this._db, this._connection, this._recorder) = AssetContext.Create();

        await this._db.Assets.AddRangeAsync(
            Rows.Select(r => new Asset {
                Id = r.Id, FileName = $"file{r.Id:D2}.png", StoragePath = $"/secret/{r.Id}",
                FileSize = r.Size, Priority = r.Priority, IsDeleted = r.Id == 10
            }),
            TestContext.Current.CancellationToken);

        await this._db.Documents.AddRangeAsync(
            [
                new Document { Id = new DocumentId(3), Kind = Kind.Video, Title = "c" },
                new Document { Id = new DocumentId(1), Kind = Kind.Image, Title = "a" },
                new Document { Id = new DocumentId(5), Kind = Kind.Video, Title = null },
                new Document { Id = new DocumentId(2), Kind = Kind.Audio, Title = "b" },
                new Document { Id = new DocumentId(4), Kind = Kind.Image, Title = "d" }
            ],
            TestContext.Current.CancellationToken);

        await this._db.SaveChangesAsync(TestContext.Current.CancellationToken);
        this._recorder.Commands.Clear();
    }

    public async ValueTask DisposeAsync() {
        await this._db.DisposeAsync();
        await this._connection.DisposeAsync();
    }

    private Task<CursorResult<AssetSummary>> PageAsync(QuerySchema<Asset, AssetSummary> schema, string queryString, CursorRequest request) {
        return this._db.Assets.ToCursorResultAsync(QueryRequest.Parse(queryString), schema, request, TestContext.Current.CancellationToken);
    }

    /// <summary>Pages forward to the end, then backward from the last page's start to the beginning.</summary>
    private static async Task<(List<long> Forward, List<long> Backward)> TraverseAsync<T>(
        Func<CursorRequest, Task<CursorResult<T>>> fetch, Func<T, long> id, int limit = 2) {

        List<long> forward = [];
        CursorToken cursor = CursorToken.Empty;
        CursorToken lastStart = CursorToken.Empty;
        int lastCount = 0;

        for(int guard = 0; ; guard++) {
            Assert.True(guard < 50, "Forward traversal did not terminate.");
            CursorResult<T> page = await fetch(new CursorRequest(cursor, limit, CursorDirection.Forward));
            forward.AddRange(page.Items.AsSpan().ToArray().Select(id));
            lastStart = page.Metadata.StartCursor;
            lastCount = page.Count;
            if(!page.Metadata.HasNext) {
                break;
            }
            cursor = page.Metadata.EndCursor;
        }

        List<List<long>> pages = [];
        cursor = lastStart;
        for(int guard = 0; !cursor.IsEmpty; guard++) {
            Assert.True(guard < 50, "Backward traversal did not terminate.");
            CursorResult<T> page = await fetch(new CursorRequest(cursor, limit, CursorDirection.Backward));
            pages.Add([.. page.Items.AsSpan().ToArray().Select(id)]);
            if(!page.Metadata.HasPrevious) {
                break;
            }
            cursor = page.Metadata.StartCursor;
        }

        pages.Reverse();
        List<long> backward = [.. pages.SelectMany(p => p), .. forward.TakeLast(lastCount)];
        return (forward, backward);
    }

    public sealed class Traversal(ToCursorResultAsyncTests fixture) : IClassFixture<ToCursorResultAsyncTests> {
        [Fact]
        public async Task Should_Visit_Every_Row_Once_Under_A_Descending_Sort_With_Ties() {
            (List<long> forward, List<long> backward) = await TraverseAsync(
                request => fixture.PageAsync(new KeysetAssetSchema(), "sort=-Priority", request), x => x.Id);

            List<long> expected = [.. Live.OrderByDescending(r => r.Priority).ThenByDescending(r => r.Id).Select(r => r.Id)];
            Assert.Equal(expected, forward);
            Assert.Equal(expected, backward);
        }

        [Fact]
        public async Task Should_Visit_Every_Row_Once_Under_An_Ascending_String_Sort() {
            (List<long> forward, List<long> backward) = await TraverseAsync(
                request => fixture.PageAsync(new KeysetAssetSchema(), "sort=FileName", request), x => x.Id, limit: 3);

            Assert.Equal([1L, 2L, 3L, 4L, 5L, 6L, 7L, 8L, 9L], forward);
            Assert.Equal(forward, backward);
        }

        [Fact]
        public async Task Should_Seek_On_Several_Sort_Keys() {
            (List<long> forward, _) = await TraverseAsync(
                request => fixture.PageAsync(new KeysetAssetSchema(), "sort=Priority,-FileName", request), x => x.Id);

            List<long> expected = [.. Live.OrderBy(r => r.Priority).ThenByDescending(r => r.Id).Select(r => r.Id)];
            Assert.Equal(expected, forward);
        }

        [Fact]
        public async Task Should_Order_By_The_Tie_Breaker_Alone_Without_Any_Sort() {
            (List<long> forward, _) = await TraverseAsync(
                request => fixture.PageAsync(new KeysetAssetSchema(), "", request), x => x.Id, limit: 4);

            Assert.Equal([1L, 2L, 3L, 4L, 5L, 6L, 7L, 8L, 9L], forward);
        }

        [Fact]
        public async Task Should_Use_The_Default_Sort_When_None_Is_Requested() {
            (List<long> forward, _) = await TraverseAsync(
                request => fixture.PageAsync(new DefaultSortedKeysetSchema(), "", request), x => x.Id);

            List<long> expected = [.. Live.OrderByDescending(r => r.Priority).ThenByDescending(r => r.Id).Select(r => r.Id)];
            Assert.Equal(expected, forward);
        }

        [Fact]
        public async Task Should_Apply_Filters_Before_Seeking() {
            (List<long> forward, _) = await TraverseAsync(
                request => fixture.PageAsync(new KeysetAssetSchema(), "FileSize[gte]=300&sort=-Priority", request), x => x.Id);

            List<long> expected = [.. Live.Where(r => r.Size >= 300).OrderByDescending(r => r.Priority).ThenByDescending(r => r.Id).Select(r => r.Id)];
            Assert.Equal(expected, forward);
        }

        [Fact]
        public async Task Should_Report_Which_Neighbouring_Pages_Exist_In_Each_Direction() {
            KeysetAssetSchema schema = new();
            const string sort = "sort=-Priority";

            CursorResult<AssetSummary> first = await fixture.PageAsync(schema, sort, new CursorRequest(CursorToken.Empty, 4));
            CursorResult<AssetSummary> second = await fixture.PageAsync(schema, sort, new CursorRequest(first.Metadata.EndCursor, 4));
            CursorResult<AssetSummary> last = await fixture.PageAsync(schema, sort, new CursorRequest(second.Metadata.EndCursor, 4));
            CursorResult<AssetSummary> backToSecond = await fixture.PageAsync(schema, sort, new CursorRequest(last.Metadata.StartCursor, 4, CursorDirection.Backward));
            CursorResult<AssetSummary> backToFirst = await fixture.PageAsync(schema, sort, new CursorRequest(backToSecond.Metadata.StartCursor, 4, CursorDirection.Backward));

            Assert.Equal((false, true), (first.Metadata.HasPrevious, first.Metadata.HasNext));
            Assert.Equal((true, true), (second.Metadata.HasPrevious, second.Metadata.HasNext));
            Assert.Equal((1, true, false), (last.Count, last.Metadata.HasPrevious, last.Metadata.HasNext));
            Assert.Equal((true, true), (backToSecond.Metadata.HasPrevious, backToSecond.Metadata.HasNext));
            Assert.Equal((false, true), (backToFirst.Metadata.HasPrevious, backToFirst.Metadata.HasNext));
            Assert.Equal(first.Items.AsSpan().ToArray().Select(x => x.Id), backToFirst.Items.AsSpan().ToArray().Select(x => x.Id));
        }

        [Fact]
        public async Task Should_Page_An_Enum_Key_With_A_Strongly_Typed_Tie_Breaker() {
            (List<long> forward, List<long> backward) = await TraverseAsync(
                request => fixture._db.Documents.ToCursorResultAsync(QueryRequest.Parse("sort=-Kind"), new DocumentSchema(), request, TestContext.Current.CancellationToken),
                x => x.Id);

            // Audio 3, Video 2, Image 1 — descending by number, ties by id descending.
            Assert.Equal([2L, 5L, 3L, 4L, 1L], forward);
            Assert.Equal(forward, backward);
        }
    }

    public sealed class TheQuery(ToCursorResultAsyncTests fixture) : IClassFixture<ToCursorResultAsyncTests> {
        [Fact]
        public async Task Should_Read_Only_The_Projection_And_Keys_And_Order_By_The_Sort_Then_The_Tie_Breaker() {
            CursorResult<AssetSummary> first = await fixture.PageAsync(new KeysetAssetSchema(), "sort=-Priority", new CursorRequest(CursorToken.Empty, 2));
            fixture._recorder.Commands.Clear();
            await fixture.PageAsync(new KeysetAssetSchema(), "sort=-Priority", new CursorRequest(first.Metadata.EndCursor, 2));

            string sql = Assert.Single(fixture._recorder.Commands);
            Assert.DoesNotContain("StoragePath", sql, StringComparison.Ordinal);
            Assert.Contains("\"IsDeleted\"", sql, StringComparison.Ordinal);
            Assert.Contains("ORDER BY \"a\".\"Priority\" DESC, \"a\".\"Id\" DESC", sql, StringComparison.Ordinal);
        }
    }

    public sealed class TheCursor(ToCursorResultAsyncTests fixture) : IClassFixture<ToCursorResultAsyncTests> {
        private static QueryValidationError SingleError(QueryValidationException error) {
            return Assert.Single(error.Errors);
        }

        [Fact]
        public async Task Should_Be_Refused_Under_A_Different_Sort() {
            CursorResult<AssetSummary> first = await fixture.PageAsync(new KeysetAssetSchema(), "sort=-Priority", new CursorRequest(CursorToken.Empty, 2));

            QueryValidationException error = await Assert.ThrowsAsync<QueryValidationException>(() =>
                fixture.PageAsync(new KeysetAssetSchema(), "sort=FileName", new CursorRequest(first.Metadata.EndCursor, 2)));

            Assert.Equal(QueryValidationErrorCode.CursorSortChanged, SingleError(error).ErrorCode);
            Assert.Equal(PaginationParameters.Cursor, SingleError(error).PropertyName);
        }

        [Fact]
        public async Task Should_Be_Refused_When_Only_The_Direction_Of_The_Sort_Changed() {
            CursorResult<AssetSummary> first = await fixture.PageAsync(new KeysetAssetSchema(), "sort=-Priority", new CursorRequest(CursorToken.Empty, 2));

            QueryValidationException error = await Assert.ThrowsAsync<QueryValidationException>(() =>
                fixture.PageAsync(new KeysetAssetSchema(), "sort=Priority", new CursorRequest(first.Metadata.EndCursor, 2)));

            Assert.Equal(QueryValidationErrorCode.CursorSortChanged, SingleError(error).ErrorCode);
        }

        [Theory]
        [InlineData("not a cursor")]
        [InlineData("")]
        public async Task Should_Be_Refused_When_It_Cannot_Be_Read(string text) {
            CursorToken token = text.Length == 0 ? CursorToken.FromBytes([1, 2, 3]) : CursorToken.FromUtf8(text);

            QueryValidationException error = await Assert.ThrowsAsync<QueryValidationException>(() =>
                fixture.PageAsync(new KeysetAssetSchema(), "sort=-Priority", new CursorRequest(token, 2)));

            Assert.Equal(QueryValidationErrorCode.InvalidCursor, SingleError(error).ErrorCode);
        }

        [Fact]
        public async Task Should_Be_Refused_With_Trailing_Bytes() {
            CursorResult<AssetSummary> first = await fixture.PageAsync(new KeysetAssetSchema(), "sort=-Priority", new CursorRequest(CursorToken.Empty, 2));
            byte[] extended = [.. first.Metadata.EndCursor.ToBytes(), 0];

            QueryValidationException error = await Assert.ThrowsAsync<QueryValidationException>(() =>
                fixture.PageAsync(new KeysetAssetSchema(), "sort=-Priority", new CursorRequest(CursorToken.FromBytes(extended), 2)));

            Assert.Equal(QueryValidationErrorCode.InvalidCursor, SingleError(error).ErrorCode);
        }

        [Fact]
        public async Task Should_Be_Refused_When_A_Key_Value_Does_Not_Decode() {
            CursorResult<AssetSummary> first = await fixture.PageAsync(new KeysetAssetSchema(), "sort=-Priority", new CursorRequest(CursorToken.Empty, 2));
            byte[] bytes = first.Metadata.EndCursor.ToBytes();
            bytes[^1] = (byte)'x';   // the last byte belongs to the id, which must parse as a number

            QueryValidationException error = await Assert.ThrowsAsync<QueryValidationException>(() =>
                fixture.PageAsync(new KeysetAssetSchema(), "sort=-Priority", new CursorRequest(CursorToken.FromBytes(bytes), 2)));

            Assert.Equal(QueryValidationErrorCode.InvalidCursor, SingleError(error).ErrorCode);
        }
    }

    public sealed class TheSort(ToCursorResultAsyncTests fixture) : IClassFixture<ToCursorResultAsyncTests> {
        [Fact]
        public async Task Should_Refuse_A_Sortable_Field_Not_Declared_As_A_Cursor_Key() {
            QueryValidationException error = await Assert.ThrowsAsync<QueryValidationException>(() =>
                fixture.PageAsync(new KeysetAssetSchema(), "sort=FileSize", new CursorRequest(CursorToken.Empty, 2)));

            QueryValidationError single = Assert.Single(error.Errors);
            Assert.Equal(QueryValidationErrorCode.FieldNotCursorSortable, single.ErrorCode);
            Assert.Equal("FileSize", single.PropertyName);
        }

        [Fact]
        public async Task Should_Refuse_A_Field_That_Is_Not_Sortable_At_All() {
            QueryValidationException error = await Assert.ThrowsAsync<QueryValidationException>(() =>
                fixture.PageAsync(new KeysetAssetSchema(), "sort=StoragePath", new CursorRequest(CursorToken.Empty, 2)));

            Assert.Contains(error.Errors, e => e.ErrorCode == QueryValidationErrorCode.FieldNotSortable);
        }

        [Fact]
        public async Task Should_Refuse_A_Filter_The_Schema_Does_Not_Allow() {
            await Assert.ThrowsAsync<QueryValidationException>(() =>
                fixture.PageAsync(new KeysetAssetSchema(), "StoragePath=/secret/1", new CursorRequest(CursorToken.Empty, 2)));
        }
    }

    public sealed class TheSchema(ToCursorResultAsyncTests fixture) : IClassFixture<ToCursorResultAsyncTests> {
        [Fact]
        public async Task Should_Throw_When_A_Default_Sort_Field_Is_Not_A_Cursor_Key() {
            InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                fixture.PageAsync(new DefaultSortNotCursorSchema(), "", new CursorRequest(CursorToken.Empty, 2)));

            Assert.Contains("'FileSize'", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public async Task Should_Throw_When_The_Tie_Breaker_Has_No_Codec() {
            InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                fixture._db.Documents.ToCursorResultAsync(QueryRequest.Parse(""), new DocumentWithoutTieBreakerCodecSchema(),
                    new CursorRequest(CursorToken.Empty, 2), TestContext.Current.CancellationToken));

            Assert.Contains("no built-in cursor codec", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public async Task Should_Throw_When_The_Schema_Declares_No_Tie_Breaker() {
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                fixture._db.Assets.ToCursorResultAsync(QueryRequest.Parse(""), new NoTieBreakerSchema(),
                    new CursorRequest(CursorToken.Empty, 2), TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task Should_Throw_When_A_Page_Boundary_Has_A_Null_Key() {
            // Ascending by title puts the null first; seeking past it would compare NULL and end the traversal.
            InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                fixture._db.Documents.ToCursorResultAsync(QueryRequest.Parse("sort=Title"), new DocumentSchema(),
                    new CursorRequest(CursorToken.Empty, 2), TestContext.Current.CancellationToken));

            Assert.Contains("'Title' is null", error.Message, StringComparison.Ordinal);
        }
    }
}
