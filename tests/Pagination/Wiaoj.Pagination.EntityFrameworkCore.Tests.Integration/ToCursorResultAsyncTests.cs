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

        IEnumerable<SnowflakeItem> snowflakeItems = Enumerable.Range(1, 20).Select(i => new SnowflakeItem {
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
        public async Task Should_Detect_HasPrevious_False_When_Reaching_Beginning_On_Backward() {
            // Arrange: 30 items in database, seek backward from ID 4 with limit 5
            // Items before 4 are only 1, 2, 3 (fewer than limit 5)
            CursorToken cursor = EncodeLong(4);
            CursorRequest request = new(cursor, limit: 5, CursorDirection.Backward);

            // Act
            CursorResult<TestItem> result = await this._fixture._context.Items
                .OrderBy(x => x.Id)
                .ToCursorResultAsync(request, x => x.Id, TestContext.Current.CancellationToken);

            // Assert: Must return items [1, 2, 3]
            Assert.Equal(3, result.Count);
            Assert.Equal(1, result.Items[0].Id);
            Assert.Equal(3, result.Items[^1].Id);
            Assert.False(result.Metadata.HasPrevious); // Hit the beginning of the table!
            Assert.True(result.Metadata.HasNext);      // Forward records exist (>= 4)
        }

        [Fact]
        public async Task Should_Set_Both_Navigation_Flags_True_On_Middle_Backward_Window() {
            // Arrange: Seek backward from ID 15 with limit 5 (items 10..14)
            CursorToken cursor = EncodeLong(15);
            CursorRequest request = new(cursor, limit: 5, CursorDirection.Backward);

            // Act
            CursorResult<TestItem> result = await this._fixture._context.Items
                .OrderBy(x => x.Id)
                .ToCursorResultAsync(request, x => x.Id, TestContext.Current.CancellationToken);

            // Assert: Items [10..14]
            Assert.Equal(5, result.Count);
            Assert.Equal(10, result.Items[0].Id);
            Assert.Equal(14, result.Items[^1].Id);
            Assert.True(result.Metadata.HasPrevious); // Items 1..9 exist behind
            Assert.True(result.Metadata.HasNext);     // Items 15..30 exist ahead
        }

        [Fact]
        public async Task Should_Support_DateTimeOffset_Key_Selector() {
            // Arrange: Fetch page 1 to obtain a genuine server-generated cursor
            CursorRequest firstPageRequest = new(CursorToken.Empty, limit: 5, CursorDirection.Forward);
            CursorResult<TestItem> firstPage = await this._fixture._context.Items
                .OrderBy(x => x.CreatedAt)
                .ToCursorResultAsync(firstPageRequest, x => x.CreatedAt, TestContext.Current.CancellationToken);

            DateTimeOffset pivotTime = firstPage.Items[^1].CreatedAt;

            // Act: Seek forward using the genuine EndCursor produced by the engine
            CursorRequest secondPageRequest = new(firstPage.Metadata.EndCursor, limit: 5, CursorDirection.Forward);
            CursorResult<TestItem> secondPage = await this._fixture._context.Items
                .OrderBy(x => x.CreatedAt)
                .ToCursorResultAsync(secondPageRequest, x => x.CreatedAt, TestContext.Current.CancellationToken);

            // Assert: Must fetch exact subsequent items strictly after the pivot timestamp
            Assert.Equal(5, secondPage.Count);
            Assert.True(secondPage.Items[0].CreatedAt > pivotTime);
            Assert.True(secondPage.Metadata.HasNext);
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
            CursorRequest request = new(cursor, limit: 5, CursorDirection.Forward);

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
            CursorRequest request = new(cursor, limit: 5, CursorDirection.Backward);

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
            SnowflakeId pivotId = new(1005L);
            Span<byte> idBuffer = stackalloc byte[sizeof(long)];
            System.Buffers.Binary.BinaryPrimitives.WriteInt64BigEndian(idBuffer, pivotId.Value);
            CursorToken cursor = CursorToken.FromBytes(idBuffer);

            CursorRequest request = new(cursor, limit: 5, CursorDirection.Forward);

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
            CursorRequest request = new(CursorToken.Empty, limit: 5, CursorDirection.Forward);

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
            CursorToken corruptedCursor = CursorToken.FromBytes([1, 2, 3]);
            CursorRequest request = new(corruptedCursor, limit: 5, CursorDirection.Forward);

            // Act & Assert: Must throw FormatException due to byte length mismatch
            await Assert.ThrowsAsync<FormatException>(() =>
                this._fixture._context.SnowflakeItems
                    .OrderBy(x => x.Id)
                    .ToCursorResultAsync(request, x => x.Id, TestContext.Current.CancellationToken));
        }

        // ---------------------------------------------------------------
        // Additional edge-case coverage
        // ---------------------------------------------------------------

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(-50)]
        [InlineData(int.MinValue)]
        public async Task Should_Clamp_Limit_To_Default_When_Zero_Or_Negative(int invalidLimit) {
            // Arrange: CursorRequest's constructor clamps limit < 1 up to CursorRequest.DefaultLimit
            // (no exception), mirroring PageRequest's clamping behavior.
            CursorRequest request = new(CursorToken.Empty, invalidLimit, CursorDirection.Forward);
            Assert.Equal(CursorRequest.DefaultLimit, request.Limit);

            // Act
            CursorResult<TestItem> result = await this._fixture._context.Items
                .OrderBy(x => x.Id)
                .ToCursorResultAsync(request, x => x.Id, TestContext.Current.CancellationToken);

            // Assert: Item count must match the clamped default limit, capped by the 30 seeded records
            int expectedCount = Math.Min(CursorRequest.DefaultLimit, 30);
            Assert.Equal(expectedCount, result.Count);
        }

        [Fact]
        public async Task Should_Clamp_Limit_To_Maximum_When_Exceeding_MaxLimit() {
            // Arrange: limit far exceeding CursorRequest.MaxLimit must clamp down, not throw.
            CursorRequest request = new(CursorToken.Empty, CursorRequest.MaxLimit + 1_000, CursorDirection.Forward);
            Assert.Equal(CursorRequest.MaxLimit, request.Limit);

            // Act
            CursorResult<TestItem> result = await this._fixture._context.Items
                .OrderBy(x => x.Id)
                .ToCursorResultAsync(request, x => x.Id, TestContext.Current.CancellationToken);

            // Assert: Only 30 records exist, all fit within the clamped limit
            Assert.Equal(30, result.Count);
            Assert.False(result.Metadata.HasNext);
        }

        [Fact]
        public async Task Should_Throw_FormatException_When_Long_Key_Cursor_Is_Invalid() {
            // Arrange: 3 bytes instead of 8 bytes, using the plain `long` key selector (not SnowflakeId)
            CursorToken corruptedCursor = CursorToken.FromBytes([9, 9, 9]);
            CursorRequest request = new(corruptedCursor, limit: 5, CursorDirection.Forward);

            // Act & Assert
            await Assert.ThrowsAsync<FormatException>(() =>
                this._fixture._context.Items
                    .OrderBy(x => x.Id)
                    .ToCursorResultAsync(request, x => x.Id, TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task Should_Return_Empty_Window_When_Cursor_Points_Beyond_Existing_Range_Forward() {
            // Arrange: Seek forward from a cursor value (999) that is beyond every existing ID (max is 30)
            CursorToken cursor = EncodeLong(999);
            CursorRequest request = new(cursor, limit: 10, CursorDirection.Forward);

            // Act
            CursorResult<TestItem> result = await this._fixture._context.Items
                .OrderBy(x => x.Id)
                .ToCursorResultAsync(request, x => x.Id, TestContext.Current.CancellationToken);

            // Assert: No records exist beyond the cursor
            Assert.True(result.IsEmpty);
            Assert.False(result.Metadata.HasNext);
        }

        [Fact]
        public async Task Should_Return_Empty_Window_When_Cursor_Points_Before_Existing_Range_Backward() {
            // Arrange: Seek backward from a cursor value (-999) that is before every existing ID (min is 1)
            CursorToken cursor = EncodeLong(-999);
            CursorRequest request = new(cursor, limit: 10, CursorDirection.Backward);

            // Act
            CursorResult<TestItem> result = await this._fixture._context.Items
                .OrderBy(x => x.Id)
                .ToCursorResultAsync(request, x => x.Id, TestContext.Current.CancellationToken);

            // Assert: No records exist before the cursor
            Assert.True(result.IsEmpty);
            Assert.False(result.Metadata.HasPrevious);
        }

        [Fact]
        public async Task Should_Return_Empty_Window_When_Forward_Cursor_Equals_Last_Item() {
            // Arrange: Seeking forward from the very last ID (30) must yield nothing further
            CursorToken cursor = EncodeLong(30);
            CursorRequest request = new(cursor, limit: 10, CursorDirection.Forward);

            // Act
            CursorResult<TestItem> result = await this._fixture._context.Items
                .OrderBy(x => x.Id)
                .ToCursorResultAsync(request, x => x.Id, TestContext.Current.CancellationToken);

            // Assert
            Assert.True(result.IsEmpty);
            Assert.False(result.Metadata.HasNext);
        }

        [Fact]
        public async Task Should_Return_Empty_Window_When_Backward_Cursor_Equals_First_Item() {
            // Arrange: Seeking backward from the very first ID (1) must yield nothing prior
            CursorToken cursor = EncodeLong(1);
            CursorRequest request = new(cursor, limit: 10, CursorDirection.Backward);

            // Act
            CursorResult<TestItem> result = await this._fixture._context.Items
                .OrderBy(x => x.Id)
                .ToCursorResultAsync(request, x => x.Id, TestContext.Current.CancellationToken);

            // Assert
            Assert.True(result.IsEmpty);
            Assert.False(result.Metadata.HasPrevious);
        }

        [Fact]
        public async Task Should_Combine_Existing_Where_Filter_With_Backward_Direction() {
            // Arrange: Filter Price > 50 (Id > 10) and seek backward from ID 25 with limit 5
            CursorToken cursor = EncodeLong(25);
            CursorRequest request = new(cursor, limit: 5, CursorDirection.Backward);

            // Act
            CursorResult<TestItem> result = await this._fixture._context.Items
                .Where(x => x.Price > 50.0m)
                .OrderBy(x => x.Id)
                .ToCursorResultAsync(request, x => x.Id, TestContext.Current.CancellationToken);

            // Assert: All items must satisfy Price > 50 AND Id < 25
            Assert.Equal(5, result.Count);
            Assert.True(result.Items.AsSpan().ToArray().All(x => x.Price > 50.0m && x.Id < 25));
            Assert.Equal(20, result.Items[0].Id);
            Assert.Equal(24, result.Items[^1].Id);
        }

        [Fact]
        public async Task Should_Support_Backward_Direction_With_SnowflakeId_Key_Selector() {
            // Arrange: Seek backward from Snowflake ID 1015 with limit 5 (expect IDs 1010..1014)
            SnowflakeId pivotId = new(1015L);
            Span<byte> idBuffer = stackalloc byte[sizeof(long)];
            BinaryPrimitives.WriteInt64BigEndian(idBuffer, pivotId.Value);
            CursorToken cursor = CursorToken.FromBytes(idBuffer);

            CursorRequest request = new(cursor, limit: 5, CursorDirection.Backward);

            // Act
            CursorResult<SnowflakeItem> result = await this._fixture._context.SnowflakeItems
                .OrderBy(x => x.Id)
                .ToCursorResultAsync(request, x => x.Id, TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(5, result.Count);
            Assert.Equal(new SnowflakeId(1010L), result.Items[0].Id);
            Assert.Equal(new SnowflakeId(1014L), result.Items[^1].Id);
            Assert.True(result.Metadata.HasPrevious);
        }

        [Fact]
        public async Task Should_Retrieve_All_Sequential_Records_Across_Pages_When_Successive_Items_Share_Identical_Keys() {
            // Arrange
            (TestDbContext context, SqliteConnection connection) = TestDbContext.CreateInMemoryContext();
            DateTimeOffset sharedTimestamp = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

            List<TestItem> items = [
                new() { Id = 1, Name = "Item_01", Price = 100m, CreatedAt = sharedTimestamp },
                new() { Id = 2, Name = "Item_02", Price = 100m, CreatedAt = sharedTimestamp },
                new() { Id = 3, Name = "Item_03", Price = 100m, CreatedAt = sharedTimestamp },
                new() { Id = 4, Name = "Item_04", Price = 100m, CreatedAt = sharedTimestamp },
                new() { Id = 5, Name = "Item_05", Price = 100m, CreatedAt = sharedTimestamp },
                new() { Id = 6, Name = "Item_06", Price = 200m, CreatedAt = sharedTimestamp.AddMinutes(1) },
                new() { Id = 7, Name = "Item_07", Price = 200m, CreatedAt = sharedTimestamp.AddMinutes(2) },
            ];

            await context.Items.AddRangeAsync(items, TestContext.Current.CancellationToken);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);

            try {
                // Act - Page 1
                CursorRequest page1Request = new(CursorToken.Empty, limit: 2, CursorDirection.Forward);
                CursorResult<TestItem> page1 = await context.Items
                    .OrderBy(x => x.CreatedAt)
                    .ToCursorResultAsync(page1Request, x => x.CreatedAt, TestContext.Current.CancellationToken);

                // Act - Page 2 using EndCursor from Page 1
                CursorRequest page2Request = new(page1.Metadata.EndCursor, limit: 2, CursorDirection.Forward);
                CursorResult<TestItem> page2 = await context.Items
                    .OrderBy(x => x.CreatedAt)
                    .ToCursorResultAsync(page2Request, x => x.CreatedAt, TestContext.Current.CancellationToken);

                // Assert
                Assert.Equal(2, page2.Count);
                Assert.Equal(3, page2.Items[0].Id);
                Assert.Equal(4, page2.Items[1].Id);
            }
            finally {
                await context.DisposeAsync();
                await connection.DisposeAsync();
            }
        }

        [Fact]
        public async Task Should_Maintain_Deterministic_Sequence_When_Items_Share_Primary_Sort_Key() {
            // Arrange
            (TestDbContext context, SqliteConnection connection) = TestDbContext.CreateInMemoryContext();
            DateTimeOffset sharedTimestamp = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

            List<TestItem> items = [
                new() { Id = 10, Name = "Product_A", Price = 50m, CreatedAt = sharedTimestamp },
                new() { Id = 20, Name = "Product_B", Price = 50m, CreatedAt = sharedTimestamp },
                new() { Id = 30, Name = "Product_C", Price = 50m, CreatedAt = sharedTimestamp },
                new() { Id = 40, Name = "Product_D", Price = 50m, CreatedAt = sharedTimestamp },
            ];

            await context.Items.AddRangeAsync(items, TestContext.Current.CancellationToken);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);

            try {
                // Act - Page 1
                CursorRequest page1Request = new(CursorToken.Empty, limit: 2, CursorDirection.Forward);
                CursorResult<TestItem> page1 = await context.Items
                    .OrderBy(x => x.CreatedAt)
                    .ThenBy(x => x.Id)
                    .ToCursorResultAsync(page1Request, x => x.CreatedAt, TestContext.Current.CancellationToken);

                // Act - Page 2 using EndCursor from Page 1
                CursorRequest page2Request = new(page1.Metadata.EndCursor, limit: 2, CursorDirection.Forward);
                CursorResult<TestItem> page2 = await context.Items
                    .OrderBy(x => x.CreatedAt)
                    .ThenBy(x => x.Id)
                    .ToCursorResultAsync(page2Request, x => x.CreatedAt, TestContext.Current.CancellationToken);

                // Assert
                Assert.Equal(2, page2.Count);
                Assert.Equal(30, page2.Items[0].Id);
                Assert.Equal(40, page2.Items[1].Id);
            }
            finally {
                await context.DisposeAsync();
                await connection.DisposeAsync();
            }
        }

        [Fact]
        public async Task Should_Paginate_Backward_Correctly_When_Items_Share_Primary_Sort_Key() {
            // Arrange
            (TestDbContext context, SqliteConnection connection) = TestDbContext.CreateInMemoryContext();
            DateTimeOffset sharedTimestamp = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

            List<TestItem> items = [
                new() { Id = 1, Name = "Item_01", Price = 100m, CreatedAt = sharedTimestamp },
                new() { Id = 2, Name = "Item_02", Price = 100m, CreatedAt = sharedTimestamp },
                new() { Id = 3, Name = "Item_03", Price = 100m, CreatedAt = sharedTimestamp },
                new() { Id = 4, Name = "Item_04", Price = 100m, CreatedAt = sharedTimestamp },
                new() { Id = 5, Name = "Item_05", Price = 100m, CreatedAt = sharedTimestamp },
            ];

            await context.Items.AddRangeAsync(items, TestContext.Current.CancellationToken);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);

            try {
                // Arrange - Move forward to obtain a cursor positioned at Item 4
                CursorRequest forwardRequest = new(CursorToken.Empty, limit: 4, CursorDirection.Forward);
                CursorResult<TestItem> forwardResult = await context.Items
                    .OrderBy(x => x.CreatedAt)
                    .ToCursorResultAsync(forwardRequest, x => x.CreatedAt, TestContext.Current.CancellationToken);

                // Act - Seek backward from Item 4 with limit 2 (Expect items 2 and 3 in ascending order)
                CursorRequest backwardRequest = new(forwardResult.Metadata.EndCursor, limit: 2, CursorDirection.Backward);
                CursorResult<TestItem> backwardResult = await context.Items
                    .OrderBy(x => x.CreatedAt)
                    .ToCursorResultAsync(backwardRequest, x => x.CreatedAt, TestContext.Current.CancellationToken);

                // Assert
                Assert.Equal(2, backwardResult.Count);
                Assert.Equal(2, backwardResult.Items[0].Id);
                Assert.Equal(3, backwardResult.Items[1].Id);
                Assert.True(backwardResult.Metadata.HasPrevious);
                Assert.True(backwardResult.Metadata.HasNext);
            }
            finally {
                await context.DisposeAsync();
                await connection.DisposeAsync();
            }
        }

        [Fact]
        public async Task Should_Paginate_Forward_Correctly_On_Descending_Query_With_Duplicate_Keys() {
            // Arrange
            (TestDbContext context, SqliteConnection connection) = TestDbContext.CreateInMemoryContext();
            DateTimeOffset sharedTimestamp = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

            List<TestItem> items = [
                new() { Id = 1, Name = "Item_01", Price = 100m, CreatedAt = sharedTimestamp },
                new() { Id = 2, Name = "Item_02", Price = 100m, CreatedAt = sharedTimestamp },
                new() { Id = 3, Name = "Item_03", Price = 100m, CreatedAt = sharedTimestamp },
                new() { Id = 4, Name = "Item_04", Price = 100m, CreatedAt = sharedTimestamp },
            ];

            await context.Items.AddRangeAsync(items, TestContext.Current.CancellationToken);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);

            try {
                // Act - Page 1 (Ordered DESC: Expect items 4, 3)
                CursorRequest page1Request = new(CursorToken.Empty, limit: 2, CursorDirection.Forward);
                CursorResult<TestItem> page1 = await context.Items
                    .OrderByDescending(x => x.CreatedAt)
                    .ThenByDescending(x => x.Id)
                    .ToCursorResultAsync(page1Request, x => x.CreatedAt, x => x.Id, TestContext.Current.CancellationToken);

                // Act - Page 2 using EndCursor from Page 1 (Expect items 2, 1)
                CursorRequest page2Request = new(page1.Metadata.EndCursor, limit: 2, CursorDirection.Forward);
                CursorResult<TestItem> page2 = await context.Items
                    .OrderByDescending(x => x.CreatedAt)
                    .ThenByDescending(x => x.Id)
                    .ToCursorResultAsync(page2Request, x => x.CreatedAt, x => x.Id, TestContext.Current.CancellationToken);

                // Assert
                Assert.Equal(2, page1.Count);
                Assert.Equal(4, page1.Items[0].Id);
                Assert.Equal(3, page1.Items[1].Id);

                Assert.Equal(2, page2.Count);
                Assert.Equal(2, page2.Items[0].Id);
                Assert.Equal(1, page2.Items[1].Id);
            }
            finally {
                await context.DisposeAsync();
                await connection.DisposeAsync();
            }
        }

        [Fact]
        public async Task Should_Rebind_Parameters_Correctly_When_Selectors_Use_Different_Parameter_Names() {
            // Arrange
            (TestDbContext context, SqliteConnection connection) = TestDbContext.CreateInMemoryContext();
            DateTimeOffset now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

            List<TestItem> items = [
                new() { Id = 1, Name = "Item_01", Price = 50m, CreatedAt = now },
                new() { Id = 2, Name = "Item_02", Price = 50m, CreatedAt = now },
                new() { Id = 3, Name = "Item_03", Price = 50m, CreatedAt = now },
            ];

            await context.Items.AddRangeAsync(items, TestContext.Current.CancellationToken);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);

            try {
                // Act - Page 1: primary uses 'firstParam', tie-breaker uses 'secondParam'
                CursorRequest page1Request = new(CursorToken.Empty, limit: 2, CursorDirection.Forward);
                CursorResult<TestItem> page1 = await context.Items
                    .OrderBy(firstParam => firstParam.Price)
                    .ThenBy(secondParam => secondParam.Id)
                    .ToCursorResultAsync(page1Request, firstParam => firstParam.Price, secondParam => secondParam.Id, TestContext.Current.CancellationToken);

                // Act - Page 2 using EndCursor
                CursorRequest page2Request = new(page1.Metadata.EndCursor, limit: 2, CursorDirection.Forward);
                CursorResult<TestItem> page2 = await context.Items
                    .OrderBy(firstParam => firstParam.Price)
                    .ThenBy(secondParam => secondParam.Id)
                    .ToCursorResultAsync(page2Request, firstParam => firstParam.Price, secondParam => secondParam.Id, TestContext.Current.CancellationToken);

                // Assert
                Assert.Single(page2.Items);
                Assert.Equal(3, page2.Items[0].Id);
            }
            finally {
                await context.DisposeAsync();
                await connection.DisposeAsync();
            }
        }

        [Fact]
        public async Task Should_Paginate_Correctly_With_Decimal_And_Long_Composite_Keys() {
            // Arrange
            (TestDbContext context, SqliteConnection connection) = TestDbContext.CreateInMemoryContext();
            DateTimeOffset now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

            List<TestItem> items = [
                new() { Id = 10, Name = "Item_10", Price = 99.99m, CreatedAt = now },
                new() { Id = 20, Name = "Item_20", Price = 99.99m, CreatedAt = now },
                new() { Id = 30, Name = "Item_30", Price = 99.99m, CreatedAt = now },
            ];

            await context.Items.AddRangeAsync(items, TestContext.Current.CancellationToken);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);

            try {
                // Act - Page 1
                CursorRequest page1Request = new(CursorToken.Empty, limit: 2, CursorDirection.Forward);
                CursorResult<TestItem> page1 = await context.Items
                    .OrderBy(x => x.Price)
                    .ThenBy(x => x.Id)
                    .ToCursorResultAsync(page1Request, x => x.Price, x => x.Id, TestContext.Current.CancellationToken);

                // Act - Page 2
                CursorRequest page2Request = new(page1.Metadata.EndCursor, limit: 2, CursorDirection.Forward);
                CursorResult<TestItem> page2 = await context.Items
                    .OrderBy(x => x.Price)
                    .ThenBy(x => x.Id)
                    .ToCursorResultAsync(page2Request, x => x.Price, x => x.Id, TestContext.Current.CancellationToken);

                // Assert
                Assert.Equal(2, page1.Count);
                Assert.Equal(10, page1.Items[0].Id);
                Assert.Equal(20, page1.Items[1].Id);

                Assert.Single(page2.Items);
                Assert.Equal(30, page2.Items[0].Id);
            }
            finally {
                await context.DisposeAsync();
                await connection.DisposeAsync();
            }
        }
    }
}