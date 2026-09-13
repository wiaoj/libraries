using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using System.Buffers.Binary;

namespace Wiaoj.Pagination.EntityFrameworkCore.Tests.Integration;

/// <summary>
/// Paging a projection rather than the whole entity (#76): the key is carried beside the response in an anonymous
/// wrapper, the SQL reads only what the projection uses, and <c>Select</c> unwraps the page without losing metadata.
/// </summary>
/// <remarks>
/// These pin behaviour that belongs to EF Core's translation, not to this library — which columns are read, and
/// that a non-translatable <c>Encode()</c> is evaluated on the client in the final projection. An EF Core upgrade that
/// changes either would otherwise go unnoticed until endpoints started over-fetching or failing.
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Subsystem", "EntityFrameworkCore")]
[Trait("Component", "Projection")]
public sealed class ToCursorResultAsyncProjectionTests : IAsyncLifetime {
    private ProjectionContext _db = null!;
    private SqliteConnection _connection = null!;
    private CommandRecorder _recorder = null!;

    public async ValueTask InitializeAsync() {
        (this._db, this._connection, this._recorder) = ProjectionContext.Create();

        // Priority ties in three groups, seeded out of Id order.
        int[] priorities = [2, 1, 3, 2, 1, 3, 2, 1, 3];
        await this._db.Assets.AddRangeAsync(
            priorities.Select((priority, index) => new Asset {
                Id = new AssetId((index * 7 % 9) + 1),
                FileName = $"file{index}.png",
                StoragePath = $"/secret/{index}",
                FileSize = 100 * (index + 1),
                Priority = priority
            }),
            TestContext.Current.CancellationToken);

        await this._db.SaveChangesAsync(TestContext.Current.CancellationToken);
        this._recorder.Commands.Clear();
    }

    public async ValueTask DisposeAsync() {
        await this._db.DisposeAsync();
        await this._connection.DisposeAsync();
    }

    private static CursorToken EncodeId(AssetId id) {
        Span<byte> buffer = stackalloc byte[sizeof(long)];
        BinaryPrimitives.WriteInt64BigEndian(buffer, id.Value);
        return CursorToken.FromBytes(buffer);
    }

    private static AssetId DecodeId(CursorToken token) {
        Span<byte> buffer = stackalloc byte[sizeof(long)];
        return token.TryDecode(buffer, out int written) && written == sizeof(long)
            ? new AssetId(BinaryPrimitives.ReadInt64BigEndian(buffer))
            : throw new FormatException();
    }

    private static CursorToken EncodePriorityAndId(int priority, AssetId id) {
        Span<byte> buffer = stackalloc byte[sizeof(int) + sizeof(long)];
        BinaryPrimitives.WriteInt32BigEndian(buffer, priority);
        BinaryPrimitives.WriteInt64BigEndian(buffer[sizeof(int)..], id.Value);
        return CursorToken.FromBytes(buffer);
    }

    private static (int, AssetId) DecodePriorityAndId(CursorToken token) {
        Span<byte> buffer = stackalloc byte[sizeof(int) + sizeof(long)];
        return token.TryDecode(buffer, out int written) && written == buffer.Length
            ? (BinaryPrimitives.ReadInt32BigEndian(buffer), new AssetId(BinaryPrimitives.ReadInt64BigEndian(buffer[sizeof(int)..])))
            : throw new FormatException();
    }

    private static async Task<List<T>> TraverseForwardAsync<T>(Func<CursorRequest, Task<CursorResult<T>>> fetch, int limit = 2) {
        List<T> seen = [];
        CursorToken cursor = CursorToken.Empty;

        for(int guard = 0; guard < 50; guard++) {
            CursorResult<T> page = await fetch(new CursorRequest(cursor, limit, CursorDirection.Forward));
            seen.AddRange(page.Items.AsSpan().ToArray());
            if(!page.Metadata.HasNext) {
                return seen;
            }
            cursor = page.Metadata.EndCursor;
        }

        throw new InvalidOperationException("Traversal did not terminate.");
    }

    public sealed class TheWrapperPattern(ToCursorResultAsyncProjectionTests fixture) : IClassFixture<ToCursorResultAsyncProjectionTests> {
        private readonly ProjectionContext _db = fixture._db;
        private readonly CommandRecorder _recorder = fixture._recorder;

        private async Task<CursorResult<AssetSummaryResponse>> PageAsync(CursorRequest request) {
            var page = await this._db.Assets
                .OrderBy(a => a.Id)
                .Select(a => new {
                    Key = a.Id,
                    Item = new AssetSummaryResponse(a.Id.Encode(), a.FileName, a.FileSize)
                })
                .ToCursorResultAsync(request, x => x.Key, EncodeId, DecodeId, TestContext.Current.CancellationToken);

            return page.Select(x => x.Item);
        }

        [Fact]
        public async Task Should_Read_Only_The_Columns_The_Projection_Uses() {
            this._recorder.Commands.Clear();

            await this.PageAsync(new CursorRequest(CursorToken.Empty, 3, CursorDirection.Forward));

            string sql = Assert.Single(this._recorder.Commands);
            Assert.Contains("\"FileName\"", sql, StringComparison.Ordinal);
            Assert.Contains("\"FileSize\"", sql, StringComparison.Ordinal);
            Assert.DoesNotContain("StoragePath", sql, StringComparison.Ordinal);
            Assert.DoesNotContain("Priority", sql, StringComparison.Ordinal);
        }

        [Fact]
        public async Task Should_Seek_On_The_Key_Column_And_Carry_The_Cursor_Across_Pages() {
            List<AssetSummaryResponse> seen = await TraverseForwardAsync(this.PageAsync);

            Assert.Equal([.. Enumerable.Range(1, 9).Select(i => $"as_{i}")], seen.Select(x => x.Id));

            string seek = this._recorder.Commands[^1];
            Assert.Contains("WHERE \"a\".\"Id\" >", seek, StringComparison.Ordinal);
        }

        [Fact]
        public async Task Should_Evaluate_A_Non_Translatable_Id_Encoding_In_The_Final_Projection() {
            CursorResult<AssetSummaryResponse> page = await this.PageAsync(new CursorRequest(CursorToken.Empty, 2, CursorDirection.Forward));

            Assert.Equal(["as_1", "as_2"], page.Items.AsSpan().ToArray().Select(x => x.Id));
        }

        [Fact]
        public async Task Should_Need_Equality_Operators_On_An_Id_Encoded_In_The_Projection() {
            // EF Core guards a client-evaluated instance call with a null check, built with ==. A plain struct without
            // the operator fails there, before any SQL is sent; a record struct has it. Pinned so the README's note is
            // revisited if EF Core stops requiring it.
            this._db.OperatorlessAssets.Add(new OperatorlessAsset { Id = new OperatorlessAssetId(1), FileName = "a.png" });
            await this._db.SaveChangesAsync(TestContext.Current.CancellationToken);

            InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(() => this._db.OperatorlessAssets
                .OrderBy(a => a.Id)
                .Select(a => new { Key = a.Id, Id = a.Id.Encode() })
                .ToCursorResultAsync(new CursorRequest(CursorToken.Empty, 2, CursorDirection.Forward), x => x.Key,
                    id => CursorToken.FromUtf8(id.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                    token => new OperatorlessAssetId(long.Parse(token.ToUtf8String(), System.Globalization.CultureInfo.InvariantCulture)),
                    TestContext.Current.CancellationToken));

            Assert.Contains("operator Equal is not defined", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public async Task Select_Should_Keep_The_Cursors_And_Flags() {
            CursorResult<AssetSummaryResponse> first = await this.PageAsync(new CursorRequest(CursorToken.Empty, 2, CursorDirection.Forward));
            CursorResult<AssetSummaryResponse> second = await this.PageAsync(new CursorRequest(first.Metadata.EndCursor, 2, CursorDirection.Forward));

            Assert.Equal(EncodeId(new AssetId(2)), first.Metadata.EndCursor);
            Assert.True(first.Metadata.HasNext);
            Assert.True(second.Metadata.HasPrevious);
            Assert.Equal(["as_3", "as_4"], second.Items.AsSpan().ToArray().Select(x => x.Id));
        }
    }

    /// <summary>
    /// A low-cardinality sort key through the wrapper. The built-in overloads find the <c>Id</c> tie-breaker by name on
    /// the element type, and an anonymous wrapper has none, so the tie-breaker is passed explicitly as a second key.
    /// </summary>
    public sealed class ANonUniqueSortKey(ToCursorResultAsyncProjectionTests fixture) : IClassFixture<ToCursorResultAsyncProjectionTests> {
        private readonly ProjectionContext _db = fixture._db;
        private readonly CommandRecorder _recorder = fixture._recorder;

        [Fact]
        public async Task Should_Page_Every_Row_Once_With_A_Strongly_Typed_Id_As_The_Tie_Breaker() {
            List<AssetSummaryResponse> pages = await TraverseForwardAsync(async request => {
                var page = await this._db.Assets
                    .OrderByDescending(a => a.Priority)
                    .Select(a => new {
                        a.Priority,
                        a.Id,
                        Item = new AssetSummaryResponse(a.Id.Encode(), a.FileName, a.FileSize)
                    })
                    .ToCursorResultAsync(request, x => x.Priority, x => x.Id,
                        EncodePriorityAndId, DecodePriorityAndId, TestContext.Current.CancellationToken);

                return page.Select(x => x.Item);
            });

            List<string> seen = [.. pages.Select(x => x.Id)];
            string lastPage = this._recorder.Commands[^1];

            List<string> expected = [.. this._db.Assets.AsEnumerable()
                .OrderByDescending(a => a.Priority).ThenByDescending(a => a.Id.Value)
                .Select(a => a.Id.Encode())];

            Assert.Equal(expected, seen);
            Assert.Contains("ORDER BY \"a\".\"Priority\" DESC, \"a\".\"Id\" DESC", lastPage, StringComparison.Ordinal);
            Assert.DoesNotContain("StoragePath", lastPage, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// The composite seeks built each level with relational operators, which a strongly-typed identifier does not
    /// declare, so the key a composite exists to carry could not be one. Checked on the entity, without a projection.
    /// </summary>
    public sealed class AStronglyTypedIdInACompositeSeek(ToCursorResultAsyncProjectionTests fixture) : IClassFixture<ToCursorResultAsyncProjectionTests> {
        private readonly ProjectionContext _db = fixture._db;

        [Fact]
        public async Task Should_Page_As_A_Primary_Key_Level_Compared_For_Equality() {
            // The id as the primary level exercises the equality leg of the seek, (Id == pivot AND tie > pivot).
            List<Asset> seen = await TraverseForwardAsync(request => this._db.Assets
                .OrderBy(a => a.Id).ThenBy(a => a.Priority)
                .ToCursorResultAsync(request, a => a.Id, a => a.Priority,
                    (id, priority) => EncodePriorityAndId(priority, id),
                    token => { (int priority, AssetId id) = DecodePriorityAndId(token); return (id, priority); },
                    TestContext.Current.CancellationToken));

            Assert.Equal([.. Enumerable.Range(1, 9).Select(i => (long)i)], seen.Select(a => a.Id.Value));
        }

        [Fact]
        public async Task Should_Page_An_Id_With_No_Operators_At_All_As_The_Primary_Level() {
            // Neither == nor < exists, so both legs of the seek go through CompareTo.
            ProjectionContext db = this._db;
            db.OperatorlessAssets.RemoveRange(db.OperatorlessAssets);
            db.OperatorlessAssets.AddRange(new[] { 4L, 1L, 3L, 2L, 5L }.Select(v =>
                new OperatorlessAsset { Id = new OperatorlessAssetId(v), FileName = $"f{v}" }));
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);

            List<OperatorlessAsset> seen = await TraverseForwardAsync(request => db.OperatorlessAssets
                .OrderByDescending(a => a.Id).ThenBy(a => a.FileName)
                .ToCursorResultAsync(request, a => a.Id, a => a.FileName,
                    (id, name) => CursorToken.FromUtf8($"{id.Value}|{name}"),
                    token => {
                        string[] parts = token.ToUtf8String().Split('|');
                        return (new OperatorlessAssetId(long.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture)), parts[1]);
                    },
                    TestContext.Current.CancellationToken));

            Assert.Equal([5L, 4L, 3L, 2L, 1L], seen.Select(a => a.Id.Value));
        }

        [Fact]
        public async Task Should_Page_A_Triple_Composite_Ending_In_The_Id() {
            List<Asset> seen = await TraverseForwardAsync(request => this._db.Assets
                .OrderByDescending(a => a.Priority).ThenBy(a => a.FileName).ThenBy(a => a.Id)
                .ToCursorResultAsync(request, a => a.Priority, a => a.FileName, a => a.Id,
                    (priority, name, id) => CursorToken.FromUtf8($"{priority}|{name}|{id.Value}"),
                    token => {
                        string[] parts = token.ToUtf8String().Split('|');
                        return (int.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture), parts[1],
                            new AssetId(long.Parse(parts[2], System.Globalization.CultureInfo.InvariantCulture)));
                    },
                    TestContext.Current.CancellationToken));

            List<long> expected = [.. this._db.Assets.AsEnumerable()
                .OrderByDescending(a => a.Priority).ThenBy(a => a.FileName, StringComparer.Ordinal).ThenBy(a => a.Id.Value)
                .Select(a => a.Id.Value)];

            Assert.Equal(expected, seen.Select(a => a.Id.Value));
        }
    }
    /// <summary>
    /// Response DTOs are usually positional records, and a positional record cannot carry the key: EF Core cannot see
    /// through a constructor to bind <c>x.Key</c> back to a column. Both ways of writing it must fail loudly, rather
    /// than start paging on something else if either side changes.
    /// </summary>
    public sealed class APositionalRecordCarryingTheKey(ToCursorResultAsyncProjectionTests fixture) : IClassFixture<ToCursorResultAsyncProjectionTests> {
        private readonly ProjectionContext _db = fixture._db;

        private static readonly CursorRequest SecondPage = new(EncodeId(new AssetId(2)), 2, CursorDirection.Forward);

        [Fact]
        public async Task Should_Be_Refused_When_Ordered_Before_The_Projection() {
            InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(() => this._db.Assets
                .OrderBy(a => a.Id)
                .Select(a => new PositionalAssetRow(a.Id, a.FileName))
                .ToCursorResultAsync(SecondPage, x => x.Key, EncodeId, DecodeId, TestContext.Current.CancellationToken));

            Assert.Contains("projection", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public async Task Should_Fail_Translation_When_Ordered_After_The_Projection() {
            // The ordering agrees with the key, so this passes verification and reaches EF Core, which cannot
            // translate a seek over a constructor-bound member.
            await Assert.ThrowsAsync<InvalidOperationException>(() => this._db.Assets
                .Select(a => new PositionalAssetRow(a.Id, a.FileName))
                .OrderBy(x => x.Key)
                .ToCursorResultAsync(SecondPage, x => x.Key, EncodeId, DecodeId, TestContext.Current.CancellationToken));
        }
    }
}
