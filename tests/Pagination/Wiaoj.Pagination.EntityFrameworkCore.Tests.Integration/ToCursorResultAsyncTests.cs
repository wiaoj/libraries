using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using System.Buffers.Binary;
using Wiaoj.Preconditions.Exceptions;
using Wiaoj.Primitives.Snowflake;

namespace Wiaoj.Pagination.EntityFrameworkCore.Tests.Integration;

[Trait("Category", "Integration")]
[Trait("Subsystem", "EntityFrameworkCore")]
public sealed class ToCursorResultAsyncTests : IAsyncLifetime {
    private TestDbContext _context = null!;
    private SqliteConnection _connection = null!;

    public async ValueTask InitializeAsync() {
        (this._context, this._connection) = TestDbContext.CreateInMemoryContext();

        // Seed 30 ordered records
        IEnumerable<TestItem> items = Enumerable.Range(1, 30).Select(i => new TestItem {
            Id = i,
            Name = $"Item_{i:D2}",
            Price = i * 5.0m,
            CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero).AddMinutes(i)
        });

        await this._context.Items.AddRangeAsync(items, TestContext.Current.CancellationToken);

        var snowflakeItems = Enumerable.Range(1, 20).Select(i => new SnowflakeItem {
            Id = new SnowflakeId(1000L + i),
            Title = $"Snowflake_{i:D2}"
        });
        await this._context.SnowflakeItems.AddRangeAsync(snowflakeItems, TestContext.Current.CancellationToken);
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

    public sealed class ToCursorResultAsyncMethod : IClassFixture<ToCursorResultAsyncTests> {
        private readonly ToCursorResultAsyncTests _fixture;

        public ToCursorResultAsyncMethod() {
            this._fixture = new ToCursorResultAsyncTests();
            this._fixture.InitializeAsync().GetAwaiter().GetResult();
        }

        [Fact]
        public async Task Should_Fetch_First_Window_Forward_Without_Cursor() {
            // Arrange
            CursorRequest request = new(CursorToken.Empty, limit: 10, CursorDirection.Forward);

            // Act
            CursorResult<TestItem> result = await this._fixture._context.Items
                .OrderBy(x => x.Id)
                .ToCursorResultAsync(request, x => x.Id, TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(10, result.Count);
            Assert.Equal(1, result.Items[0].Id);
            Assert.Equal(10, result.Items[^1].Id);
            Assert.Equal(EncodeLong(1), result.Metadata.StartCursor);
            Assert.Equal(EncodeLong(10), result.Metadata.EndCursor);
            Assert.False(result.Metadata.HasPrevious);
            Assert.True(result.Metadata.HasNext);
        }

        [Fact]
        public async Task Should_Fetch_Next_Window_Forward_Using_Cursor() {
            // Arrange: Seek after item 10
            CursorToken cursor = EncodeLong(10);
            CursorRequest request = new(cursor, limit: 10, CursorDirection.Forward);

            // Act
            CursorResult<TestItem> result = await this._fixture._context.Items
                .OrderBy(x => x.Id)
                .ToCursorResultAsync(request, x => x.Id, TestContext.Current.CancellationToken);

            // Assert: Must fetch IDs 11..20
            Assert.Equal(10, result.Count);
            Assert.Equal(11, result.Items[0].Id);
            Assert.Equal(20, result.Items[^1].Id);
            Assert.True(result.Metadata.HasPrevious);
            Assert.True(result.Metadata.HasNext);
        }

        [Fact]
        public async Task Should_Fetch_Previous_Window_Backward_Using_Cursor() {
            // Arrange: Seek backward from item 15 with limit 5 (Must return exact preceding items: 10, 11, 12, 13, 14)
            CursorToken cursor = EncodeLong(15);
            CursorRequest request = new(cursor, limit: 5, CursorDirection.Backward);

            // Act
            CursorResult<TestItem> result = await this._fixture._context.Items
                .OrderBy(x => x.Id)
                .ToCursorResultAsync(request, x => x.Id, TestContext.Current.CancellationToken);

            // Assert: Must return exactly IDs 10..14 in ascending order
            Assert.Equal(5, result.Count);
            Assert.Equal(10, result.Items[0].Id);
            Assert.Equal(14, result.Items[^1].Id);
            Assert.True(result.Metadata.HasPrevious);
        }

        [Fact]
        public async Task Should_Detect_HasNext_False_On_Last_Window() {
            // Arrange: Seek after item 20 with limit 10 (Total: 30)
            CursorToken cursor = EncodeLong(20);
            CursorRequest request = new(cursor, limit: 10, CursorDirection.Forward);

            // Act
            CursorResult<TestItem> result = await this._fixture._context.Items
                .OrderBy(x => x.Id)
                .ToCursorResultAsync(request, x => x.Id, TestContext.Current.CancellationToken);

            // Assert: Must fetch IDs 21..30 and HasNext must be FALSE
            Assert.Equal(10, result.Count);
            Assert.Equal(21, result.Items[0].Id);
            Assert.Equal(30, result.Items[^1].Id);
            Assert.True(result.Metadata.HasPrevious);
            Assert.False(result.Metadata.HasNext);
        }

        [Fact]
        public async Task Should_Support_DateTimeOffset_Key_Selector() {
            // Arrange: Seek forward using CreatedAt timestamp
            DateTimeOffset pivotTime = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero).AddMinutes(10);
            Span<byte> timeBuffer = stackalloc byte[sizeof(long)];
            BinaryPrimitives.WriteInt64BigEndian(timeBuffer, pivotTime.ToUnixTimeMilliseconds());
            CursorToken cursor = CursorToken.FromBytes(timeBuffer);

            CursorRequest request = new(cursor, limit: 5, CursorDirection.Forward);

            // Act
            CursorResult<TestItem> result = await this._fixture._context.Items
                .OrderBy(x => x.CreatedAt)
                .ToCursorResultAsync(request, x => x.CreatedAt, TestContext.Current.CancellationToken);

            // Assert: Must fetch items created strictly after the pivot timestamp
            Assert.Equal(5, result.Count);
            Assert.True(result.Items[0].CreatedAt > pivotTime);
            Assert.True(result.Metadata.HasNext);
        }

        [Fact]
        public async Task Should_Combine_Existing_Where_Filter_With_Keyset_Predicate() {
            // Arrange: Filter Price > 50 and seek forward from ID 15 with limit 5
            CursorToken cursor = EncodeLong(15);
            CursorRequest request = new(cursor, limit: 5, CursorDirection.Forward);

            // Act
            CursorResult<TestItem> result = await this._fixture._context.Items
                .Where(x => x.Price > 50.0m)
                .OrderBy(x => x.Id)
                .ToCursorResultAsync(request, x => x.Id, TestContext.Current.CancellationToken);

            // Assert: All items must satisfy Price > 50 AND Id > 15
            Assert.Equal(5, result.Count);
            Assert.True(result.Items.AsSpan().ToArray().All(x => x.Price > 50.0m && x.Id > 15));
            Assert.Equal(16, result.Items[0].Id);
        }

        [Fact]
        public async Task Should_Handle_Exact_Limit_Match_Without_Dropping_Records() {
            // Arrange: 30 items in database, seek after ID 20 with limit 10
            CursorToken cursor = EncodeLong(20);
            CursorRequest request = new(cursor, limit: 10, CursorDirection.Forward);

            // Act
            CursorResult<TestItem> result = await this._fixture._context.Items
                .OrderBy(x => x.Id)
                .ToCursorResultAsync(request, x => x.Id, TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(10, result.Count);
            Assert.Equal(21, result.Items[0].Id);
            Assert.Equal(30, result.Items[^1].Id);
            Assert.False(result.Metadata.HasNext);
        }

        [Fact]
        public async Task Should_Return_Empty_Result_When_Database_Is_Empty() {
            // Arrange
            (TestDbContext? emptyContext, SqliteConnection? connection) = TestDbContext.CreateInMemoryContext();
            CursorRequest request = CursorRequest.Default;

            // Act
            CursorResult<TestItem> result = await emptyContext.Items
                .OrderBy(x => x.Id)
                .ToCursorResultAsync(request, x => x.Id, TestContext.Current.CancellationToken);

            // Assert
            Assert.True(result.IsEmpty);
            Assert.Equal(0, result.Count);
            Assert.False(result.Metadata.HasNext);
            Assert.False(result.Metadata.HasPrevious);

            await emptyContext.DisposeAsync();
            await connection.DisposeAsync();
        }

        [Fact]
        public async Task Should_Throw_When_Required_Arguments_Are_Null() {
            // Arrange
            IQueryable<TestItem> nullQuery = null!;
            CursorRequest request = CursorRequest.Default;

            // Act & Assert
            await Assert.ThrowsAsync<PrecaArgumentNullException>(() =>
                nullQuery.ToCursorResultAsync(request, x => x.Id, TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task Should_Respect_CancellationToken() {
            // Arrange
            using CancellationTokenSource cts = new();
            cts.Cancel();

            // Act & Assert
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                this._fixture._context.Items
                    .OrderBy(x => x.Id)
                    .ToCursorResultAsync(CursorRequest.Default, x => x.Id, cts.Token));
        }

        [Fact]
        public async Task Should_Paginate_Forward_Correctly_On_Descending_Query() {
            // Arrange: 30 items ordered DESC (30, 29, 28...), seek forward from 25 with limit 5
            CursorToken cursor = EncodeLong(25);
            var request = new CursorRequest(cursor, limit: 5, CursorDirection.Forward);

            // Act: Ordered by Id DESC
            CursorResult<TestItem> result = await this._fixture._context.Items
                .OrderByDescending(x => x.Id)
                .ToCursorResultAsync(request, x => x.Id, TestContext.Current.CancellationToken);

            // Assert: Must fetch exact next descending items: 24, 23, 22, 21, 20
            Assert.Equal(5, result.Count);
            Assert.Equal(24, result.Items[0].Id);
            Assert.Equal(20, result.Items[^1].Id);
            Assert.True(result.Metadata.HasPrevious);
            Assert.True(result.Metadata.HasNext);
        }

        [Fact]
        public async Task Should_Paginate_Backward_Correctly_On_Descending_Query() {
            // Arrange: 30 items ordered DESC, seek backward from 20 with limit 5 (expect items 25, 24, 23, 22, 21)
            CursorToken cursor = EncodeLong(20);
            var request = new CursorRequest(cursor, limit: 5, CursorDirection.Backward);

            // Act: Ordered by Id DESC
            CursorResult<TestItem> result = await this._fixture._context.Items
                .OrderByDescending(x => x.Id)
                .ToCursorResultAsync(request, x => x.Id, TestContext.Current.CancellationToken);

            // Assert: Must fetch exact previous items in descending sequence: 25, 24, 23, 22, 21
            Assert.Equal(5, result.Count);
            Assert.Equal(25, result.Items[0].Id);
            Assert.Equal(21, result.Items[^1].Id);
            Assert.True(result.Metadata.HasPrevious);
        }

        [Fact]
        public async Task Should_Support_SnowflakeId_Key_Selector() {
            // Arrange: Seek forward after Snowflake ID 1005 with limit 5 (expect IDs 1006..1010)
            var pivotId = new SnowflakeId(1005L);
            Span<byte> idBuffer = stackalloc byte[sizeof(long)];
            System.Buffers.Binary.BinaryPrimitives.WriteInt64BigEndian(idBuffer, pivotId.Value);
            CursorToken cursor = CursorToken.FromBytes(idBuffer);

            var request = new CursorRequest(cursor, limit: 5, CursorDirection.Forward);

            // Act: Using the dedicated SnowflakeId overload
            CursorResult<SnowflakeItem> result = await this._fixture._context.SnowflakeItems
                .OrderBy(x => x.Id)
                .ToCursorResultAsync(request, x => x.Id, TestContext.Current.CancellationToken);

            // Assert: Must fetch exact items with SnowflakeId > 1005
            Assert.Equal(5, result.Count);
            Assert.Equal(new SnowflakeId(1006L), result.Items[0].Id);
            Assert.Equal(new SnowflakeId(1010L), result.Items[^1].Id);
            Assert.True(result.Metadata.HasPrevious);
            Assert.True(result.Metadata.HasNext);
        }

        [Fact]
        public async Task Should_Fetch_First_Window_With_SnowflakeId_Without_Cursor() {
            // Arrange: Request first 5 items from the beginning
            var request = new CursorRequest(CursorToken.Empty, limit: 5, CursorDirection.Forward);

            // Act
            CursorResult<SnowflakeItem> result = await this._fixture._context.SnowflakeItems
                .OrderBy(x => x.Id)
                .ToCursorResultAsync(request, x => x.Id, TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(5, result.Count);
            Assert.Equal(new SnowflakeId(1001L), result.Items[0].Id);
            Assert.Equal(new SnowflakeId(1005L), result.Items[^1].Id);
            Assert.False(result.Metadata.HasPrevious);
            Assert.True(result.Metadata.HasNext);
        }

        [Fact]
        public async Task Should_Throw_FormatException_When_SnowflakeId_Cursor_Is_Invalid() {
            // Arrange: 3 bytes instead of 8 bytes
            var corruptedCursor = CursorToken.FromBytes([1, 2, 3]);
            var request = new CursorRequest(corruptedCursor, limit: 5, CursorDirection.Forward);

            // Act & Assert: Must throw FormatException due to byte length mismatch
            await Assert.ThrowsAsync<FormatException>(() =>
                this._fixture._context.SnowflakeItems
                    .OrderBy(x => x.Id)
                    .ToCursorResultAsync(request, x => x.Id, TestContext.Current.CancellationToken));
        }
    }
}