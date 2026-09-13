using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using System.Buffers.Binary;

namespace Wiaoj.Pagination.EntityFrameworkCore.Tests.Integration;

/// <summary>
/// The seek predicate is built from the key selectors; the page window comes from the queryable's ordering. When the
/// two describe different columns, pages skip and repeat rows and every call still succeeds (#73). These pin that
/// every such disagreement is refused, and that every agreeing shape keeps working.
/// </summary>
/// <remarks>
/// Traversal tests seed rows whose sort order and <c>Id</c> order disagree, and tie rows under a descending
/// ordering. SQLite returns tied rows in rowid order, which is <c>Id</c> ascending, so an ascending tie is correct by
/// accident and cannot reveal a missing tie-breaker. A descending one can.
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Subsystem", "EntityFrameworkCore")]
[Trait("Component", "OrderingGuardrail")]
public sealed class ToCursorResultAsyncOrderingGuardrailTests : IAsyncLifetime {
    private TestDbContext _context = null!;
    private SqliteConnection _connection = null!;

    public async ValueTask InitializeAsync() {
        (this._context, this._connection) = TestDbContext.CreateInMemoryContext();

        // Price order and Id order disagree; Name order is Id order reversed.
        long[] prices = [50, 90, 10, 30, 70, 20, 100, 40, 80, 60];
        await this._context.Items.AddRangeAsync(
            prices.Select((price, index) => new TestItem {
                Id = index + 1,
                Name = $"Item_{10 - index:D2}",
                Price = price,
                CreatedAt = DateTimeOffset.UnixEpoch
            }),
            TestContext.Current.CancellationToken);

        // Three groups of tied CategoryId values, each group seeded out of Id order.
        long[] categories = [2, 1, 3, 2, 1, 3, 2, 1, 3];
        await this._context.SmallKeyItems.AddRangeAsync(
            categories.Select((category, index) => new SmallKeyItem {
                Id = (index * 7 % 9) + 1,
                CategoryId = category,
                Label = $"Row_{index}"
            }),
            TestContext.Current.CancellationToken);

        await this._context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync() {
        await this._context.DisposeAsync();
        await this._connection.DisposeAsync();
    }

    private static CursorToken EncodeLong(long value) {
        Span<byte> buffer = stackalloc byte[sizeof(long)];
        BinaryPrimitives.WriteInt64BigEndian(buffer, value);
        return CursorToken.FromBytes(buffer);
    }

    private static long DecodeLong(CursorToken token) {
        Span<byte> buffer = stackalloc byte[sizeof(long)];
        return token.TryDecode(buffer, out int written) && written == sizeof(long)
            ? BinaryPrimitives.ReadInt64BigEndian(buffer)
            : throw new FormatException();
    }

    /// <summary>Pages forward from the start to the end, then backward from the end to the start.</summary>
    private static async Task<(List<long> Forward, List<long> Backward)> TraverseAsync<T>(
        Func<CursorRequest, Task<CursorResult<T>>> fetch,
        Func<T, long> id,
        int limit = 2) {

        List<long> forward = [];
        CursorToken cursor = CursorToken.Empty;
        CursorToken last = CursorToken.Empty;

        for(int guard = 0; guard < 100; guard++) {
            CursorResult<T> page = await fetch(new CursorRequest(cursor, limit, CursorDirection.Forward));
            forward.AddRange(page.Items.AsSpan().ToArray().Select(id));
            if(page.Count > 0) {
                last = page.Metadata.StartCursor;
            }
            if(!page.Metadata.HasNext) {
                break;
            }
            cursor = page.Metadata.EndCursor;
        }

        // Backward from the start of the last forward page: everything before it, in reverse page order.
        List<long> backward = [];
        cursor = last;
        List<List<long>> pages = [];

        for(int guard = 0; guard < 100 && !cursor.IsEmpty; guard++) {
            CursorResult<T> page = await fetch(new CursorRequest(cursor, limit, CursorDirection.Backward));
            pages.Add([.. page.Items.AsSpan().ToArray().Select(id)]);
            if(!page.Metadata.HasPrevious || page.Count == 0) {
                break;
            }
            cursor = page.Metadata.StartCursor;
        }

        for(int i = pages.Count - 1; i >= 0; i--) {
            backward.AddRange(pages[i]);
        }

        return (forward, backward);
    }

    private static void AssertEveryRowOnceInOrder(IReadOnlyList<long> expected, (List<long> Forward, List<long> Backward) traversal) {
        Assert.Equal(expected, traversal.Forward);

        // The backward walk starts from the last forward page, so it yields every row before that page.
        int lastPageStart = expected.Count - (expected.Count % 2 == 0 ? 2 : 1);
        Assert.Equal(expected.Take(lastPageStart), traversal.Backward);
    }

    public sealed class WhenTheOrderingAgreesWithTheKeys(ToCursorResultAsyncOrderingGuardrailTests fixture)
        : IClassFixture<ToCursorResultAsyncOrderingGuardrailTests> {

        private readonly TestDbContext _db = fixture._context;

        [Fact]
        public async Task Should_Page_A_Descending_Key() {
            var traversal = await TraverseAsync(
                request => this._db.Items.OrderByDescending(x => x.Id)
                    .ToCursorResultAsync(request, x => x.Id, TestContext.Current.CancellationToken),
                x => x.Id);

            AssertEveryRowOnceInOrder([10, 9, 8, 7, 6, 5, 4, 3, 2, 1], traversal);
        }

        [Fact]
        public async Task Should_Page_Ties_Under_A_Descending_Order_With_The_Injected_Tie_Breaker() {
            // Only OrderByDescending(CategoryId) is written; Id is injected as the tie-breaker. Unless the injected
            // key is also in the ORDER BY, SQLite returns tied rows Id-ascending while the seek assumes descending.
            var traversal = await TraverseAsync(
                request => this._db.SmallKeyItems.OrderByDescending(x => x.CategoryId)
                    .ToCursorResultAsync(request, x => x.CategoryId, TestContext.Current.CancellationToken),
                x => x.Id);

            List<long> expected = [.. this._db.SmallKeyItems.AsEnumerable()
                .OrderByDescending(x => x.CategoryId).ThenByDescending(x => x.Id).Select(x => x.Id)];

            AssertEveryRowOnceInOrder(expected, traversal);
        }

        [Fact]
        public async Task Should_Page_Ties_When_The_Tie_Breaker_Is_Written_Out() {
            var traversal = await TraverseAsync(
                request => this._db.SmallKeyItems.OrderByDescending(x => x.CategoryId).ThenBy(x => x.Id)
                    .ToCursorResultAsync(request, x => x.CategoryId, TestContext.Current.CancellationToken),
                x => x.Id);

            List<long> expected = [.. this._db.SmallKeyItems.AsEnumerable()
                .OrderByDescending(x => x.CategoryId).ThenBy(x => x.Id).Select(x => x.Id)];

            AssertEveryRowOnceInOrder(expected, traversal);
        }

        [Fact]
        public async Task Should_Page_An_Explicit_Composite_With_A_Missing_Trailing_Level() {
            var traversal = await TraverseAsync(
                request => this._db.Items.OrderByDescending(x => x.Price)
                    .ToCursorResultAsync(request, x => x.Price, x => x.Id,
                        (price, id) => EncodeLong((long)price * 1000 + id),
                        token => { long v = DecodeLong(token); return (v / 1000, v % 1000); },
                        TestContext.Current.CancellationToken),
                x => x.Id);

            AssertEveryRowOnceInOrder([7, 2, 9, 5, 10, 1, 8, 4, 6, 3], traversal);
        }

        [Fact]
        public async Task Should_See_Through_A_Filter_Applied_After_The_Ordering() {
            var traversal = await TraverseAsync(
                request => this._db.Items.OrderBy(x => x.Id).Where(x => x.Id > 2)
                    .ToCursorResultAsync(request, x => x.Id, TestContext.Current.CancellationToken),
                x => x.Id);

            AssertEveryRowOnceInOrder([3, 4, 5, 6, 7, 8, 9, 10], traversal);
        }

        [Fact]
        public async Task Should_Use_Only_The_Ordering_That_Replaced_An_Earlier_One() {
            // A second OrderBy discards the first; Name is not part of the effective ordering.
            var traversal = await TraverseAsync(
                request => this._db.Items.OrderBy(x => x.Name).OrderBy(x => x.Id)
                    .ToCursorResultAsync(request, x => x.Id, TestContext.Current.CancellationToken),
                x => x.Id);

            AssertEveryRowOnceInOrder([1, 2, 3, 4, 5, 6, 7, 8, 9, 10], traversal);
        }

        [Fact]
        public async Task Should_See_Through_A_Projection_Carrying_The_Key() {
            // The wrapper pattern from #76: ordered on the entity, paged on the projection's Key member.
            var traversal = await TraverseAsync(
                request => this._db.Items.OrderByDescending(x => x.Id)
                    .Select(x => new { Key = x.Id, x.Name })
                    .ToCursorResultAsync(request, x => x.Key, EncodeLong, DecodeLong, TestContext.Current.CancellationToken),
                x => x.Key);

            AssertEveryRowOnceInOrder([10, 9, 8, 7, 6, 5, 4, 3, 2, 1], traversal);
        }

        [Fact]
        public async Task Should_See_Through_A_Member_Init_Projection() {
            var traversal = await TraverseAsync(
                request => this._db.Items.OrderBy(x => x.Id)
                    .Select(x => new KeyedRow { Key = x.Id, Name = x.Name })
                    .ToCursorResultAsync(request, x => x.Key, EncodeLong, DecodeLong, TestContext.Current.CancellationToken),
                x => x.Key);

            AssertEveryRowOnceInOrder([1, 2, 3, 4, 5, 6, 7, 8, 9, 10], traversal);
        }

        [Fact]
        public async Task Should_Accept_An_Ordering_Written_Over_The_Projection() {
            var traversal = await TraverseAsync(
                request => this._db.Items.Select(x => new { Key = x.Id, x.Name })
                    .OrderByDescending(x => x.Key)
                    .ToCursorResultAsync(request, x => x.Key, EncodeLong, DecodeLong, TestContext.Current.CancellationToken),
                x => x.Key);

            AssertEveryRowOnceInOrder([10, 9, 8, 7, 6, 5, 4, 3, 2, 1], traversal);
        }
    }

    public sealed class WhenTheOrderingDisagreesWithTheKeys(ToCursorResultAsyncOrderingGuardrailTests fixture)
        : IClassFixture<ToCursorResultAsyncOrderingGuardrailTests> {

        private readonly TestDbContext _db = fixture._context;

        private static readonly CursorRequest FirstPage = new(CursorToken.Empty, 3, CursorDirection.Forward);

        [Fact]
        public async Task Should_Refuse_A_Sort_Column_That_Is_Not_The_Cursor_Key() {
            // The #73 reproduction: ordered by one column, seeking on another. Rows were skipped and repeated.
            InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                this._db.Items.OrderByDescending(x => x.Price)
                    .ToCursorResultAsync(FirstPage, x => x.Id, TestContext.Current.CancellationToken));

            Assert.Contains("x.Price", error.Message, StringComparison.Ordinal);
            Assert.Contains("x.Id", error.Message, StringComparison.Ordinal);
            Assert.Contains("AllowSort", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public async Task Should_Refuse_On_The_First_Page_Not_Only_When_A_Cursor_Is_Sent() {
            // The first page is where it must fail: it is the one every test and every client requests.
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                this._db.Items.OrderBy(x => x.Name)
                    .ToCursorResultAsync(FirstPage, x => x.Id, EncodeLong, DecodeLong, TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task Should_Refuse_A_Query_With_No_Ordering() {
            // Without ORDER BY the database may return rows in any order, and the seek assumes one.
            InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                this._db.Items.Where(x => x.Id > 0)
                    .ToCursorResultAsync(FirstPage, x => x.Id, EncodeLong, DecodeLong, TestContext.Current.CancellationToken));

            Assert.Contains("ordered", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public async Task Should_Refuse_More_Ordering_Levels_Than_Keys() {
            // Previously the single-key path read the direction from the last ThenByDescending — descending — while
            // the query was ordered ascending by Id. Measured over six rows at limit 2: page one [1, 2], page two [1]
            // with HasNext false. Rows 3 to 6 were never returned.
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                this._db.Items.OrderBy(x => x.Id).ThenByDescending(x => x.Name)
                    .ToCursorResultAsync(FirstPage, x => x.Id, EncodeLong, DecodeLong, TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task Should_Refuse_A_Written_Tie_Breaker_That_Is_Not_The_Injected_One() {
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                this._db.SmallKeyItems.OrderBy(x => x.CategoryId).ThenBy(x => x.IntKey)
                    .ToCursorResultAsync(FirstPage, x => x.CategoryId, TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task Should_Refuse_A_Composite_Whose_Levels_Are_Swapped() {
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                this._db.Items.OrderBy(x => x.Id).ThenBy(x => x.Price)
                    .ToCursorResultAsync(FirstPage, x => x.Price, x => x.Id,
                        (price, id) => EncodeLong((long)price * 1000 + id),
                        token => { long v = DecodeLong(token); return (v / 1000, v % 1000); },
                        TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task Should_Refuse_A_Triple_Composite_Whose_Middle_Level_Differs() {
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                this._db.Items.OrderBy(x => x.Price).ThenBy(x => x.CreatedAt).ThenBy(x => x.Id)
                    .ToCursorResultAsync(FirstPage, x => x.Price, x => x.Name, x => x.Id,
                        (price, name, id) => CursorToken.FromUtf8($"{price}|{name}|{id}"),
                        token => throw new NotSupportedException(),
                        TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task Should_Refuse_A_Projection_Whose_Key_Is_Not_The_Ordered_Column() {
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                this._db.Items.OrderBy(x => x.Name)
                    .Select(x => new { Key = x.Id, x.Name })
                    .ToCursorResultAsync(FirstPage, x => x.Key, EncodeLong, DecodeLong, TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task Should_Refuse_A_Projection_It_Cannot_See_Through() {
            // A constructor call gives no member binding to follow, so the key cannot be traced back to the column
            // the query is ordered on. Refusing is the only answer that cannot be wrong.
            InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                this._db.Items.OrderBy(x => x.Id)
                    .Select(x => new PositionalRow(x.Id, x.Name))
                    .ToCursorResultAsync(FirstPage, x => x.Key, EncodeLong, DecodeLong, TestContext.Current.CancellationToken));

            Assert.Contains("projection", error.Message, StringComparison.Ordinal);
        }
    }

    public sealed class KeyedRow {
        public long Key { get; init; }
        public string Name { get; init; } = string.Empty;
    }

    public sealed record PositionalRow(long Key, string Name);
}
