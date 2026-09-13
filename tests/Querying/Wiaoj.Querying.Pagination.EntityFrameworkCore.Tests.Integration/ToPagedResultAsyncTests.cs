using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Wiaoj.Pagination;
using Wiaoj.Querying.Pagination.EntityFrameworkCore.Tests.Integration.Fixtures;

namespace Wiaoj.Querying.Pagination.EntityFrameworkCore.Tests.Integration;

/// <summary>
/// One call applies a query contract and returns an offset page of the response shape (#81, offset half).
/// </summary>
[Trait("Category", "Integration")]
[Trait("Feature", "Querying")]
[Trait("Component", "Pagination")]
public sealed class ToPagedResultAsyncTests : IAsyncLifetime {
    private AssetContext _db = null!;
    private SqliteConnection _connection = null!;
    private CommandRecorder _recorder = null!;

    // Priority ties in three groups and FileSize ties in pairs, both seeded out of Id order. Id 10 is deleted.
    private static readonly (long Id, int Priority, long Size)[] Rows = [
        (4, 2, 300), (1, 1, 100), (7, 3, 300), (2, 2, 200), (9, 1, 100),
        (5, 3, 200), (3, 2, 400), (8, 1, 400), (6, 3, 500), (10, 1, 999)
    ];

    public async ValueTask InitializeAsync() {
        (this._db, this._connection, this._recorder) = AssetContext.Create();

        await this._db.Assets.AddRangeAsync(
            Rows.Select(r => new Asset {
                Id = r.Id, FileName = $"file{r.Id}.png", StoragePath = $"/secret/{r.Id}",
                FileSize = r.Size, Priority = r.Priority, IsDeleted = r.Id == 10
            }),
            TestContext.Current.CancellationToken);

        await this._db.SaveChangesAsync(TestContext.Current.CancellationToken);
        this._recorder.Commands.Clear();
    }

    public async ValueTask DisposeAsync() {
        await this._db.DisposeAsync();
        await this._connection.DisposeAsync();
    }

    private static QueryRequest Parse(string queryString) {
        return QueryRequest.Parse(queryString);
    }

    private async Task<List<long>> AllPagesAsync(QuerySchema<Asset, AssetSummary> schema, string queryString, int size = 2) {
        List<long> ids = [];
        for(int page = 1; page < 50; page++) {
            PagedResult<AssetSummary> result = await this._db.Assets
                .ToPagedResultAsync(Parse(queryString), schema, new PageRequest(page, size), TestContext.Current.CancellationToken);

            ids.AddRange(result.Items.AsSpan().ToArray().Select(x => x.Id));
            if(!result.Metadata.HasNext) {
                return ids;
            }
        }

        throw new InvalidOperationException("Paging did not terminate.");
    }

    [Fact]
    public async Task Should_Filter_Project_And_Page_In_One_Query_Per_Step() {
        PagedResult<AssetSummary> page = await this._db.Assets.ToPagedResultAsync(
            Parse("FileSize[gte]=200&sort=-FileSize"), new AssetSchema(), new PageRequest(1, 3), TestContext.Current.CancellationToken);

        // Sizes 200 to 500, excluding the deleted row: ids 2, 3, 4, 5, 6, 7, 8.
        Assert.Equal(7, page.Metadata.TotalCount);
        Assert.Equal([6L, 3L, 8L], page.Items.AsSpan().ToArray().Select(x => x.Id));

        string select = this._recorder.Commands[^1];
        Assert.DoesNotContain("StoragePath", select, StringComparison.Ordinal);
        Assert.DoesNotContain("Priority", select, StringComparison.Ordinal);
        Assert.Contains("ORDER BY \"a\".\"FileSize\" DESC, \"a\".\"Id\"", select, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Should_Return_Every_Row_Once_Across_Pages_When_The_Sort_Field_Repeats() {
        List<long> ids = await this.AllPagesAsync(new AssetSchema(), "sort=Priority");

        List<long> expected = [.. Rows.Where(r => r.Id != 10).OrderBy(r => r.Priority).ThenBy(r => r.Id).Select(r => r.Id)];
        Assert.Equal(expected, ids);
    }

    [Fact]
    public async Task Should_Order_By_The_Tie_Breaker_Alone_Without_Any_Sort() {
        List<long> ids = await this.AllPagesAsync(new AssetSchema(), "");

        Assert.Equal([1L, 2L, 3L, 4L, 5L, 6L, 7L, 8L, 9L], ids);
        Assert.Contains("ORDER BY \"a\".\"Id\"", this._recorder.Commands[^1], StringComparison.Ordinal);
    }

    [Fact]
    public async Task Should_Follow_The_Default_Sort_With_The_Tie_Breaker() {
        List<long> ids = await this.AllPagesAsync(new DefaultSortedAssetSchema(), "");

        List<long> expected = [.. Rows.Where(r => r.Id != 10).OrderByDescending(r => r.Size).ThenBy(r => r.Id).Select(r => r.Id)];
        Assert.Equal(expected, ids);
    }

    [Fact]
    public async Task Should_Apply_The_Schemas_Required_Filters() {
        PagedResult<AssetSummary> page = await this._db.Assets.ToPagedResultAsync(
            Parse(""), new AssetSchema(), new PageRequest(1, 50), TestContext.Current.CancellationToken);

        Assert.Equal(9, page.Metadata.TotalCount);
        Assert.DoesNotContain(page.Items.AsSpan().ToArray(), x => x.Id == 10);
    }

    [Fact]
    public async Task Should_Replace_An_Ordering_Applied_Before_The_Call() {
        PagedResult<AssetSummary> page = await this._db.Assets
            .OrderByDescending(a => a.Id)
            .ToPagedResultAsync(Parse(""), new AssetSchema(), new PageRequest(1, 3), TestContext.Current.CancellationToken);

        Assert.Equal([1L, 2L, 3L], page.Items.AsSpan().ToArray().Select(x => x.Id));
    }

    [Fact]
    public async Task Should_Throw_Rather_Than_Skip_A_Filter_The_Schema_Does_Not_Allow() {
        // ApplyQuery alone skips it and returns every row.
        QueryValidationException error = await Assert.ThrowsAsync<QueryValidationException>(() => this._db.Assets.ToPagedResultAsync(
            Parse("StoragePath=/secret/1"), new AssetSchema(), new PageRequest(1, 10), TestContext.Current.CancellationToken));

        Assert.Contains("StoragePath", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Should_Throw_When_The_Schema_Declares_No_Tie_Breaker() {
        InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(() => this._db.Assets.ToPagedResultAsync(
            Parse(""), new NoTieBreakerSchema(), new PageRequest(1, 10), TestContext.Current.CancellationToken));

        Assert.Contains("TieBreaker(e => e.Id)", error.Message, StringComparison.Ordinal);
        Assert.Empty(this._recorder.Commands);
    }

    [Fact]
    public async Task Should_Verify_A_Schema_Built_Outside_The_Container() {
        await Assert.ThrowsAsync<InvalidOperationException>(() => this._db.Assets.ToPagedResultAsync(
            Parse(""), new BrokenContractSchema(), new PageRequest(1, 10), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Should_Return_An_Empty_Page_With_Its_Metadata_Past_The_End() {
        PagedResult<AssetSummary> page = await this._db.Assets.ToPagedResultAsync(
            Parse(""), new AssetSchema(), new PageRequest(9, 10), TestContext.Current.CancellationToken);

        Assert.Equal(0, page.Count);
        Assert.Equal(9, page.Metadata.TotalCount);
        Assert.Equal(9, page.Metadata.Page);
    }
}
