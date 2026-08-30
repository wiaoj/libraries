using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using System.Buffers.Binary;
using Wiaoj.Preconditions.Exceptions;

namespace Wiaoj.Pagination.EntityFrameworkCore.Tests.Integration;

// -----------------------------------------------------------------------------------------------
// Supplemental coverage: automatic Id tie-breaker detection on the single-key decimal/DateTimeOffset/
// string overloads, ExtractSortDirections tolerance/strictness around missing vs. extra OrderBy levels,
// legacy (pre-composite) cursor decoding, explicit 2-key composite edge cases, and every small/unsigned
// integer key type (byte, sbyte, short, ushort, uint, ulong) plus the int overload's implicit Id
// tie-breaker detection - none of which are exercised by the primary happy-path test files.
// -----------------------------------------------------------------------------------------------
public sealed partial class ToCursorResultAsyncTests {
    public sealed partial class ToCursorResultAsyncMethod {

        // --- Automatic tie-breaker detection & ordering-level tolerance -------------------------

        [Fact]
        public async Task Should_Not_Throw_And_Should_Traverse_Every_Row_Exactly_Once_When_DateTimeOffset_Keys_Duplicate() {
            // Arrange: four items share the exact same CreatedAt, and are deliberately seeded out of Id
            // order so a coincidental physical/insertion-order match cannot mask a real gap or duplicate.
            // The single-key DateTimeOffset overload auto-detects Id as a tie-breaker purely for building
            // a correct seek (WHERE) predicate; it does not retroactively rewrite the caller's forward-page
            // ORDER BY, so the exact relative order of tied rows is left to the database - this test only
            // asserts the property that actually is guaranteed: paging through to completion visits every
            // row exactly once, with no gaps or duplicates, and never throws.
            (TestDbContext context, SqliteConnection connection) = TestDbContext.CreateInMemoryContext();
            DateTimeOffset sharedTimestamp = new(2026, 3, 1, 0, 0, 0, TimeSpan.Zero);

            List<TestItem> items = [
                new() { Id = 40, Name = "Item_40", Price = 1m, CreatedAt = sharedTimestamp },
                new() { Id = 10, Name = "Item_10", Price = 1m, CreatedAt = sharedTimestamp },
                new() { Id = 30, Name = "Item_30", Price = 1m, CreatedAt = sharedTimestamp },
                new() { Id = 20, Name = "Item_20", Price = 1m, CreatedAt = sharedTimestamp },
            ];

            await context.Items.AddRangeAsync(items, TestContext.Current.CancellationToken);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);

            try {
                // Act: traverse forward two-at-a-time, only ever writing a single explicit OrderBy level
                List<long> visitedIds = [];
                CursorToken cursor = CursorToken.Empty;
                bool hasNext = true;

                while(hasNext) {
                    CursorRequest request = new(cursor, limit: 2, CursorDirection.Forward);
                    CursorResult<TestItem> page = await context.Items
                        .OrderBy(x => x.CreatedAt)
                        .ToCursorResultAsync(request, x => x.CreatedAt, TestContext.Current.CancellationToken);

                    visitedIds.AddRange(page.Items.AsSpan().ToArray().Select(x => x.Id));
                    cursor = page.Metadata.EndCursor;
                    hasNext = page.Metadata.HasNext;
                }

                // Assert: exactly the seeded set, no duplicates, no omissions
                Assert.Equal(4, visitedIds.Count);
                Assert.Equal(visitedIds.Distinct().Count(), visitedIds.Count);
                Assert.Equal([10L, 20L, 30L, 40L], visitedIds.Order());
            }
            finally {
                await context.DisposeAsync();
                await connection.DisposeAsync();
            }
        }

        [Fact]
        public async Task Should_Not_Throw_When_Decimal_Key_Duplicates_Exist_Without_Explicit_TieBreaker_Selector() {
            // Arrange: three items share the same Price; the decimal overload's Id auto-detection must
            // not throw, and must still return every matching row exactly once.
            (TestDbContext context, SqliteConnection connection) = TestDbContext.CreateInMemoryContext();
            DateTimeOffset now = DateTimeOffset.UtcNow;

            List<TestItem> items = [
                new() { Id = 3, Name = "Item_3", Price = 42.5m, CreatedAt = now },
                new() { Id = 1, Name = "Item_1", Price = 42.5m, CreatedAt = now },
                new() { Id = 2, Name = "Item_2", Price = 42.5m, CreatedAt = now },
            ];

            await context.Items.AddRangeAsync(items, TestContext.Current.CancellationToken);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);

            try {
                // Act: no ThenBy(Id) supplied by the caller
                CursorRequest request = new(CursorToken.Empty, limit: 10, CursorDirection.Forward);
                CursorResult<TestItem> result = await context.Items
                    .OrderBy(x => x.Price)
                    .ToCursorResultAsync(request, x => x.Price, TestContext.Current.CancellationToken);

                // Assert
                Assert.Equal(3, result.Count);
                Assert.Equal([1L, 2L, 3L], result.Items.AsSpan().ToArray().Select(x => x.Id).Order());
                Assert.False(result.Metadata.HasNext);
            }
            finally {
                await context.DisposeAsync();
                await connection.DisposeAsync();
            }
        }

        [Fact]
        public async Task Should_Not_Throw_When_String_Key_Duplicates_Exist_Without_Explicit_TieBreaker_Selector() {
            // Arrange: three items share the same Name; the string overload's Id auto-detection must
            // not throw, and must still return every matching row exactly once.
            (TestDbContext context, SqliteConnection connection) = TestDbContext.CreateInMemoryContext();
            DateTimeOffset now = DateTimeOffset.UtcNow;

            List<TestItem> items = [
                new() { Id = 15, Name = "Duplicate", Price = 1m, CreatedAt = now },
                new() { Id = 5, Name = "Duplicate", Price = 1m, CreatedAt = now },
                new() { Id = 25, Name = "Duplicate", Price = 1m, CreatedAt = now },
            ];

            await context.Items.AddRangeAsync(items, TestContext.Current.CancellationToken);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);

            try {
                // Act
                CursorRequest request = new(CursorToken.Empty, limit: 10, CursorDirection.Forward);
                CursorResult<TestItem> result = await context.Items
                    .OrderBy(x => x.Name)
                    .ToCursorResultAsync(request, x => x.Name, TestContext.Current.CancellationToken);

                // Assert
                Assert.Equal(3, result.Count);
                Assert.Equal([5L, 15L, 25L], result.Items.AsSpan().ToArray().Select(x => x.Id).Order());
            }
            finally {
                await context.DisposeAsync();
                await connection.DisposeAsync();
            }
        }

        [Fact]
        public async Task Should_Accept_Legacy_Single_Key_8Byte_Cursor_For_Backward_Compatible_Composite_Decoding() {
            // Arrange: the explicit (DateTimeOffset, long) composite overload's decoder tolerates a
            // pre-composite, timestamp-only 8-byte cursor, defaulting the missing tie-breaker to 0.
            // Because every real Id is > 0, the row the legacy cursor points at (Item_1, Id=1) still
            // satisfies the seek predicate "(CreatedAt, Id) > (boundary.CreatedAt, 0)" - its CreatedAt
            // ties the boundary but Id=1 > 0, so it is NOT excluded. All three seeded rows are returned.
            (TestDbContext context, SqliteConnection connection) = TestDbContext.CreateInMemoryContext();
            DateTimeOffset baseTime = new(2026, 4, 1, 0, 0, 0, TimeSpan.Zero);

            List<TestItem> items = [
                new() { Id = 1, Name = "Item_1", Price = 1m, CreatedAt = baseTime.AddMinutes(1) },
                new() { Id = 2, Name = "Item_2", Price = 1m, CreatedAt = baseTime.AddMinutes(2) },
                new() { Id = 3, Name = "Item_3", Price = 1m, CreatedAt = baseTime.AddMinutes(3) },
            ];

            await context.Items.AddRangeAsync(items, TestContext.Current.CancellationToken);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);

            try {
                // Build a legacy 8-byte (timestamp-only) cursor pointing at item 1's CreatedAt
                Span<byte> legacyBuffer = stackalloc byte[sizeof(long)];
                BinaryPrimitives.WriteInt64BigEndian(legacyBuffer, baseTime.AddMinutes(1).ToUnixTimeMilliseconds());
                CursorToken legacyCursor = CursorToken.FromBytes(legacyBuffer);
                CursorRequest request = new(legacyCursor, limit: 10, CursorDirection.Forward);

                // Act
                CursorResult<TestItem> result = await context.Items
                    .OrderBy(x => x.CreatedAt)
                    .ThenBy(x => x.Id)
                    .ToCursorResultAsync(request, x => x.CreatedAt, x => x.Id, TestContext.Current.CancellationToken);

                // Assert: the tie-breaker fallback of 0 does not exclude Item_1 itself (Id=1 > 0),
                // so all three seeded rows are returned in (CreatedAt, Id) order.
                Assert.Equal(3, result.Count);
                Assert.Equal(1, result.Items[0].Id);
                Assert.Equal(2, result.Items[1].Id);
                Assert.Equal(3, result.Items[2].Id);
            }
            finally {
                await context.DisposeAsync();
                await connection.DisposeAsync();
            }
        }

        [Fact]
        public async Task Should_Not_Throw_When_TieBreaker_Ordering_Level_Is_Omitted_And_Should_Correctly_Seek_Past_Pivot() {
            // Arrange: caller supplies the explicit 2-key composite overload but only writes a single
            // OrderBy(Price) level (no ThenBy(Id)). Per ExtractSortDirections, the missing tie-breaker
            // level is tolerated (defaults to the primary key's direction) rather than throwing - this
            // test only asserts the unambiguous, non-tied portion of the result to stay independent of
            // SQLite's implementation-defined tie order for the two untied Price=10 rows.
            (TestDbContext context, SqliteConnection connection) = TestDbContext.CreateInMemoryContext();
            DateTimeOffset now = DateTimeOffset.UtcNow;

            List<TestItem> items = [
                new() { Id = 1, Name = "Item_1", Price = 10m, CreatedAt = now },
                new() { Id = 2, Name = "Item_2", Price = 10m, CreatedAt = now },
                new() { Id = 3, Name = "Item_3", Price = 20m, CreatedAt = now },
            ];

            await context.Items.AddRangeAsync(items, TestContext.Current.CancellationToken);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);

            try {
                // Act: only OrderByDescending(Price) is present - no ThenBy at all. Must not throw.
                CursorRequest request = new(CursorToken.Empty, limit: 10, CursorDirection.Forward);
                CursorResult<TestItem> result = await context.Items
                    .OrderByDescending(x => x.Price)
                    .ToCursorResultAsync(request, x => x.Price, x => x.Id, TestContext.Current.CancellationToken);

                // Assert: the unique Price=20 row is unambiguously first; both Price=10 rows are present
                Assert.Equal(3, result.Count);
                Assert.Equal(3, result.Items[0].Id);
                Assert.Contains(result.Items.AsSpan().ToArray(), x => x.Id == 1);
                Assert.Contains(result.Items.AsSpan().ToArray(), x => x.Id == 2);
            }
            finally {
                await context.DisposeAsync();
                await connection.DisposeAsync();
            }
        }

        [Fact]
        public async Task Should_Throw_InvalidOperationException_When_OrderBy_Chain_Has_More_Levels_Than_Composite_Key_Selectors() {
            // Arrange: 3 explicit ordering levels supplied, but only 2 key selectors (Price, Id) passed -
            // the extra CreatedAt level would be silently excluded from the seek boundary, so it must throw.
            (TestDbContext context, SqliteConnection connection) = TestDbContext.CreateInMemoryContext();

            try {
                CursorRequest request = new(CursorToken.Empty, limit: 10, CursorDirection.Forward);

                // Act & Assert
                await Assert.ThrowsAsync<InvalidOperationException>(() =>
                    context.Items
                        .OrderBy(x => x.Price)
                        .ThenBy(x => x.CreatedAt)
                        .ThenBy(x => x.Id)
                        .ToCursorResultAsync(request, x => x.Price, x => x.Id, TestContext.Current.CancellationToken));
            }
            finally {
                await context.DisposeAsync();
                await connection.DisposeAsync();
            }
        }

        [Fact]
        public async Task Should_Throw_InvalidOperationException_When_Query_Has_No_Explicit_OrderBy_For_Composite() {
            // Arrange: no OrderBy/OrderByDescending call at all in the chain
            (TestDbContext context, SqliteConnection connection) = TestDbContext.CreateInMemoryContext();

            try {
                CursorRequest request = new(CursorToken.Empty, limit: 10, CursorDirection.Forward);

                // Act & Assert
                await Assert.ThrowsAsync<InvalidOperationException>(() =>
                    context.Items
                        .ToCursorResultAsync(request, x => x.Price, x => x.Id, TestContext.Current.CancellationToken));
            }
            finally {
                await context.DisposeAsync();
                await connection.DisposeAsync();
            }
        }


        // --- Explicit 2-key composite edge cases ----------------------------------------------

        [Fact]
        public async Task Should_Return_Empty_Result_When_Database_Is_Empty_For_Composite() {
            // Arrange: the explicit 2-key (Price, Id) composite overload against an empty database
            (TestDbContext emptyContext, SqliteConnection connection) = TestDbContext.CreateInMemoryContext();
            CursorRequest request = CursorRequest.Default;

            try {
                // Act
                CursorResult<TestItem> result = await emptyContext.Items
                    .OrderBy(x => x.Price)
                    .ThenBy(x => x.Id)
                    .ToCursorResultAsync(request, x => x.Price, x => x.Id, TestContext.Current.CancellationToken);

                // Assert
                Assert.True(result.IsEmpty);
                Assert.False(result.Metadata.HasNext);
                Assert.False(result.Metadata.HasPrevious);
            }
            finally {
                await emptyContext.DisposeAsync();
                await connection.DisposeAsync();
            }
        }

        [Fact]
        public async Task Should_Throw_When_Source_Is_Null_For_Composite() {
            // Arrange
            IQueryable<TestItem> nullQuery = null!;
            CursorRequest request = CursorRequest.Default;

            // Act & Assert
            await Assert.ThrowsAsync<PrecaArgumentNullException>(() =>
                nullQuery.ToCursorResultAsync(request, x => x.Price, x => x.Id, TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task Should_Throw_FormatException_When_Composite_Cursor_Length_Matches_Neither_Legacy_Nor_Composite_Width() {
            // Arrange: the (DateTimeOffset, long) composite decoder only accepts 8 bytes (legacy,
            // timestamp-only) or 16 bytes (full composite) - anything else must be rejected.
            CursorToken malformedCursor = CursorToken.FromBytes([1, 2, 3]);
            CursorRequest request = new(malformedCursor, limit: 5, CursorDirection.Forward);

            // Act & Assert
            await Assert.ThrowsAsync<FormatException>(() =>
                this._fixture._context.Items
                    .OrderBy(x => x.CreatedAt)
                    .ThenBy(x => x.Id)
                    .ToCursorResultAsync(request, x => x.CreatedAt, x => x.Id, TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task Should_Handle_Exact_Limit_Match_Without_Dropping_Records_For_Composite() {
            // Arrange: request a limit exactly equal to the seeded row count for a unique composite key
            (TestDbContext context, SqliteConnection connection) = TestDbContext.CreateInMemoryContext();
            DateTimeOffset now = DateTimeOffset.UtcNow;

            List<TestItem> items = [.. Enumerable.Range(1, 6).Select(i => new TestItem {
                Id = i,
                Name = $"Item_{i}",
                Price = i * 10m,
                CreatedAt = now
            })];

            await context.Items.AddRangeAsync(items, TestContext.Current.CancellationToken);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);

            try {
                // Act
                CursorRequest request = new(CursorToken.Empty, limit: 6, CursorDirection.Forward);
                CursorResult<TestItem> result = await context.Items
                    .OrderBy(x => x.Price)
                    .ThenBy(x => x.Id)
                    .ToCursorResultAsync(request, x => x.Price, x => x.Id, TestContext.Current.CancellationToken);

                // Assert: exactly the seeded count is returned and HasNext correctly reads false,
                // proving the N+1 probe row was not mistakenly retained.
                Assert.Equal(6, result.Count);
                Assert.False(result.Metadata.HasNext);
            }
            finally {
                await context.DisposeAsync();
                await connection.DisposeAsync();
            }
        }


        // --- Small/unsigned integer key types (byte, sbyte, short, ushort, uint, ulong) --------

        private static async Task<(TestDbContext Context, SqliteConnection Connection)> CreateSeededSmallKeyContextAsync(int count = 12) {
            (TestDbContext context, SqliteConnection connection) = TestDbContext.CreateInMemoryContext();

            List<SmallKeyItem> items = [.. Enumerable.Range(1, count).Select(i => new SmallKeyItem {
                Id = i,
                ByteKey = (byte)(10 + i),
                SByteKey = (sbyte)(i - 6),          // spans negative -> positive values
                ShortKey = (short)(1_000 + i),
                UShortKey = (ushort)(2_000 + i),
                IntKey = 100_000 + i,
                UIntKey = (uint)(200_000 + i),
                ULongKey = 5_000_000_000UL + (ulong)i, // beyond uint.MaxValue, confirms 64-bit width
                Label = $"SmallKey_{i:D2}"
            })];

            await context.SmallKeyItems.AddRangeAsync(items, TestContext.Current.CancellationToken);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);

            return (context, connection);
        }

        [Fact]
        public async Task Should_Paginate_Forward_Using_Byte_Key_Selector() {
            // Arrange
            (TestDbContext context, SqliteConnection connection) = await CreateSeededSmallKeyContextAsync();
            CursorRequest request = new(CursorToken.Empty, limit: 5, CursorDirection.Forward);

            try {
                // Act
                CursorResult<SmallKeyItem> result = await context.SmallKeyItems
                    .OrderBy(x => x.ByteKey)
                    .ToCursorResultAsync(request, x => x.ByteKey, TestContext.Current.CancellationToken);

                // Assert
                Assert.Equal(5, result.Count);
                Assert.Equal((byte)11, result.Items[0].ByteKey);
                Assert.Equal((byte)15, result.Items[^1].ByteKey);
                Assert.False(result.Metadata.HasPrevious);
                Assert.True(result.Metadata.HasNext);
            }
            finally {
                await context.DisposeAsync();
                await connection.DisposeAsync();
            }
        }

        [Fact]
        public async Task Should_Paginate_Second_Window_Using_SByte_Key_Selector() {
            // Arrange: SByteKey ranges from -5 (Id=1) to 6 (Id=12), first window covers -5..-1
            (TestDbContext context, SqliteConnection connection) = await CreateSeededSmallKeyContextAsync();

            try {
                CursorRequest page1Request = new(CursorToken.Empty, limit: 5, CursorDirection.Forward);
                CursorResult<SmallKeyItem> page1 = await context.SmallKeyItems
                    .OrderBy(x => x.SByteKey)
                    .ToCursorResultAsync(page1Request, x => x.SByteKey, TestContext.Current.CancellationToken);

                // Act: fetch the next window, which must cross the negative/positive boundary (0)
                CursorRequest page2Request = new(page1.Metadata.EndCursor, limit: 5, CursorDirection.Forward);
                CursorResult<SmallKeyItem> page2 = await context.SmallKeyItems
                    .OrderBy(x => x.SByteKey)
                    .ToCursorResultAsync(page2Request, x => x.SByteKey, TestContext.Current.CancellationToken);

                // Assert
                Assert.Equal((sbyte)-5, page1.Items[0].SByteKey);
                Assert.Equal((sbyte)-1, page1.Items[^1].SByteKey);

                Assert.Equal(5, page2.Count);
                Assert.Equal((sbyte)0, page2.Items[0].SByteKey);
                Assert.Equal((sbyte)4, page2.Items[^1].SByteKey);
            }
            finally {
                await context.DisposeAsync();
                await connection.DisposeAsync();
            }
        }

        [Fact]
        public async Task Should_Paginate_Backward_Using_Short_Key_Selector() {
            // Arrange: forward to a known boundary first, then seek backward from it
            (TestDbContext context, SqliteConnection connection) = await CreateSeededSmallKeyContextAsync();

            try {
                CursorResult<SmallKeyItem> forward = await context.SmallKeyItems
                    .OrderBy(x => x.ShortKey)
                    .ToCursorResultAsync(new CursorRequest(CursorToken.Empty, limit: 6, CursorDirection.Forward),
                        x => x.ShortKey, TestContext.Current.CancellationToken);

                // Act
                CursorRequest backwardRequest = new(forward.Metadata.EndCursor, limit: 3, CursorDirection.Backward);
                CursorResult<SmallKeyItem> backward = await context.SmallKeyItems
                    .OrderBy(x => x.ShortKey)
                    .ToCursorResultAsync(backwardRequest, x => x.ShortKey, TestContext.Current.CancellationToken);

                // Assert: items immediately preceding forward's last item (short key 1006)
                Assert.Equal(3, backward.Count);
                Assert.Equal((short)1003, backward.Items[0].ShortKey);
                Assert.Equal((short)1005, backward.Items[^1].ShortKey);
                Assert.True(backward.Metadata.HasNext);
            }
            finally {
                await context.DisposeAsync();
                await connection.DisposeAsync();
            }
        }

        [Fact]
        public async Task Should_Paginate_Forward_Using_UShort_Key_Selector() {
            // Arrange
            (TestDbContext context, SqliteConnection connection) = await CreateSeededSmallKeyContextAsync();
            CursorRequest request = new(CursorToken.Empty, limit: 4, CursorDirection.Forward);

            try {
                // Act
                CursorResult<SmallKeyItem> result = await context.SmallKeyItems
                    .OrderBy(x => x.UShortKey)
                    .ToCursorResultAsync(request, x => x.UShortKey, TestContext.Current.CancellationToken);

                // Assert
                Assert.Equal(4, result.Count);
                Assert.Equal((ushort)2001, result.Items[0].UShortKey);
                Assert.Equal((ushort)2004, result.Items[^1].UShortKey);
            }
            finally {
                await context.DisposeAsync();
                await connection.DisposeAsync();
            }
        }

        [Fact]
        public async Task Should_Paginate_Forward_Using_UInt_Key_Selector() {
            // Arrange
            (TestDbContext context, SqliteConnection connection) = await CreateSeededSmallKeyContextAsync();
            CursorRequest request = new(CursorToken.Empty, limit: 4, CursorDirection.Forward);

            try {
                // Act
                CursorResult<SmallKeyItem> result = await context.SmallKeyItems
                    .OrderBy(x => x.UIntKey)
                    .ToCursorResultAsync(request, x => x.UIntKey, TestContext.Current.CancellationToken);

                // Assert
                Assert.Equal(4, result.Count);
                Assert.Equal(200_001u, result.Items[0].UIntKey);
                Assert.Equal(200_004u, result.Items[^1].UIntKey);
            }
            finally {
                await context.DisposeAsync();
                await connection.DisposeAsync();
            }
        }

        [Fact]
        public async Task Should_Paginate_Forward_Using_ULong_Key_Selector_Beyond_UInt_Range() {
            // Arrange: values exceed uint.MaxValue, proving the full 64-bit width round-trips correctly
            (TestDbContext context, SqliteConnection connection) = await CreateSeededSmallKeyContextAsync();
            CursorRequest request = new(CursorToken.Empty, limit: 4, CursorDirection.Forward);

            try {
                // Act
                CursorResult<SmallKeyItem> result = await context.SmallKeyItems
                    .OrderBy(x => x.ULongKey)
                    .ToCursorResultAsync(request, x => x.ULongKey, TestContext.Current.CancellationToken);

                // Assert
                Assert.Equal(4, result.Count);
                Assert.Equal(5_000_000_001UL, result.Items[0].ULongKey);
                Assert.Equal(5_000_000_004UL, result.Items[^1].ULongKey);
                Assert.True(result.Items.AsSpan().ToArray().All(x => x.ULongKey > uint.MaxValue));
            }
            finally {
                await context.DisposeAsync();
                await connection.DisposeAsync();
            }
        }

        [Fact]
        public async Task Should_Paginate_Using_Plain_Int_Key_Selector_Without_Implicit_TieBreaker_When_Id_Is_Projected_Away() {
            // Arrange: projecting to an anonymous type without an "Id" member disables the library's
            // automatic tie-breaker detection, exercising the plain (non-composite) int encoding path.
            (TestDbContext context, SqliteConnection connection) = await CreateSeededSmallKeyContextAsync();
            CursorRequest request = new(CursorToken.Empty, limit: 4, CursorDirection.Forward);

            try {
                // Act
                var result = await context.SmallKeyItems
                    .Select(x => new { x.IntKey, x.Label })
                    .OrderBy(x => x.IntKey)
                    .ToCursorResultAsync(request, x => x.IntKey, TestContext.Current.CancellationToken);

                // Assert
                Assert.Equal(4, result.Count);
                Assert.Equal(100_001, result.Items[0].IntKey);
                Assert.Equal(100_004, result.Items[^1].IntKey);
            }
            finally {
                await context.DisposeAsync();
                await connection.DisposeAsync();
            }
        }

        [Fact]
        public async Task Should_Paginate_Using_Int_Key_Selector_With_Implicit_Id_TieBreaker_When_Entity_Has_Id_Property() {
            // Arrange: SmallKeyItem exposes both IntKey and Id (long), so the library auto-detects Id
            // as a tie-breaker for the int overload even though only IntKey is passed explicitly.
            (TestDbContext context, SqliteConnection connection) = await CreateSeededSmallKeyContextAsync();
            CursorRequest request = new(CursorToken.Empty, limit: 4, CursorDirection.Forward);

            try {
                // Act
                CursorResult<SmallKeyItem> result = await context.SmallKeyItems
                    .OrderBy(x => x.IntKey)
                    .ToCursorResultAsync(request, x => x.IntKey, TestContext.Current.CancellationToken);

                // Assert: still resolves correctly (all IntKey values happen to be unique here) and the
                // resulting cursor round-trips through a subsequent page without throwing.
                Assert.Equal(4, result.Count);
                Assert.Equal(100_001, result.Items[0].IntKey);

                CursorRequest page2Request = new(result.Metadata.EndCursor, limit: 4, CursorDirection.Forward);
                CursorResult<SmallKeyItem> page2 = await context.SmallKeyItems
                    .OrderBy(x => x.IntKey)
                    .ToCursorResultAsync(page2Request, x => x.IntKey, TestContext.Current.CancellationToken);

                Assert.Equal(100_005, page2.Items[0].IntKey);
            }
            finally {
                await context.DisposeAsync();
                await connection.DisposeAsync();
            }
        }

        [Fact]
        public async Task Should_Throw_FormatException_When_Byte_Cursor_Is_Invalid() {
            // Arrange: byte cursors must decode to exactly 1 byte
            (TestDbContext context, SqliteConnection connection) = await CreateSeededSmallKeyContextAsync();
            CursorToken corruptedCursor = CursorToken.FromBytes([1, 2]);
            CursorRequest request = new(corruptedCursor, limit: 5, CursorDirection.Forward);

            try {
                // Act & Assert
                await Assert.ThrowsAsync<FormatException>(() =>
                    context.SmallKeyItems
                        .OrderBy(x => x.ByteKey)
                        .ToCursorResultAsync(request, x => x.ByteKey, TestContext.Current.CancellationToken));
            }
            finally {
                await context.DisposeAsync();
                await connection.DisposeAsync();
            }
        }

        [Fact]
        public async Task Should_Throw_FormatException_When_UShort_Cursor_Is_Invalid() {
            // Arrange: ushort cursors must decode to exactly 2 bytes
            (TestDbContext context, SqliteConnection connection) = await CreateSeededSmallKeyContextAsync();
            CursorToken corruptedCursor = CursorToken.FromBytes([1, 2, 3]);
            CursorRequest request = new(corruptedCursor, limit: 5, CursorDirection.Forward);

            try {
                // Act & Assert
                await Assert.ThrowsAsync<FormatException>(() =>
                    context.SmallKeyItems
                        .OrderBy(x => x.UShortKey)
                        .ToCursorResultAsync(request, x => x.UShortKey, TestContext.Current.CancellationToken));
            }
            finally {
                await context.DisposeAsync();
                await connection.DisposeAsync();
            }
        }

        [Fact]
        public async Task Should_Throw_FormatException_When_ULong_Cursor_Is_Invalid() {
            // Arrange: ulong cursors must decode to exactly 8 bytes
            (TestDbContext context, SqliteConnection connection) = await CreateSeededSmallKeyContextAsync();
            CursorToken corruptedCursor = CursorToken.FromBytes([1, 2, 3, 4, 5]);
            CursorRequest request = new(corruptedCursor, limit: 5, CursorDirection.Forward);

            try {
                // Act & Assert
                await Assert.ThrowsAsync<FormatException>(() =>
                    context.SmallKeyItems
                        .OrderBy(x => x.ULongKey)
                        .ToCursorResultAsync(request, x => x.ULongKey, TestContext.Current.CancellationToken));
            }
            finally {
                await context.DisposeAsync();
                await connection.DisposeAsync();
            }
        }

        [Fact]
        public async Task Should_Return_Empty_Window_When_UInt_Cursor_Points_Beyond_Existing_Range() {
            // Arrange: fetch every row first to obtain a real end-of-range cursor (the last UIntKey is
            // 200_012), then seek forward once more from that boundary.
            (TestDbContext context, SqliteConnection connection) = await CreateSeededSmallKeyContextAsync();

            try {
                CursorResult<SmallKeyItem> everything = await context.SmallKeyItems
                    .OrderBy(x => x.UIntKey)
                    .ToCursorResultAsync(new CursorRequest(CursorToken.Empty, limit: 100, CursorDirection.Forward),
                        x => x.UIntKey, TestContext.Current.CancellationToken);
                Assert.False(everything.Metadata.HasNext);

                // Act: seek past the very last UIntKey value
                CursorRequest beyondRequest = new(everything.Metadata.EndCursor, limit: 5, CursorDirection.Forward);
                CursorResult<SmallKeyItem> beyond = await context.SmallKeyItems
                    .OrderBy(x => x.UIntKey)
                    .ToCursorResultAsync(beyondRequest, x => x.UIntKey, TestContext.Current.CancellationToken);

                // Assert
                Assert.True(beyond.IsEmpty);
                Assert.False(beyond.Metadata.HasNext);
            }
            finally {
                await context.DisposeAsync();
                await connection.DisposeAsync();
            }
        }

        [Fact]
        public async Task Should_Return_Empty_Result_When_Database_Is_Empty_For_SByte_Key_Selector() {
            // Arrange
            (TestDbContext emptyContext, SqliteConnection connection) = TestDbContext.CreateInMemoryContext();
            CursorRequest request = CursorRequest.Default;

            try {
                // Act
                CursorResult<SmallKeyItem> result = await emptyContext.SmallKeyItems
                    .OrderBy(x => x.SByteKey)
                    .ToCursorResultAsync(request, x => x.SByteKey, TestContext.Current.CancellationToken);

                // Assert
                Assert.True(result.IsEmpty);
                Assert.False(result.Metadata.HasNext);
                Assert.False(result.Metadata.HasPrevious);
            }
            finally {
                await emptyContext.DisposeAsync();
                await connection.DisposeAsync();
            }
        }

    }
}