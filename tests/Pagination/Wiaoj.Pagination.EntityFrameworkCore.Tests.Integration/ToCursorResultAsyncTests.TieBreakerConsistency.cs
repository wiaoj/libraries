using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Wiaoj.Pagination.EntityFrameworkCore.Tests.Integration;

// -----------------------------------------------------------------------------------------------
// Coverage for the tie-breaker consistency fix: previously only int/decimal/DateTime/DateTimeOffset/
// string auto-injected the Id tie-breaker on duplicate keys - byte/sbyte/short/ushort/uint/long/ulong/
// Int128/UInt128/double/float/Half/DateOnly/TimeOnly/TimeSpan/char silently did not. Each test below
// seeds duplicate values on a SmallKeyItem property of that type and confirms full, gap-free, non-
// duplicated traversal without an explicit ThenBy(Id) - proving the tie-breaker now fires uniformly.
//
// Guid and SnowflakeId are intentionally NOT covered here: TryGetTieBreaker is hard-coded to look for
// an "Id" property of type `long`. On GuidItem/SnowflakeItem the real Id is typed Guid/SnowflakeId
// (not long), so the type-match check always fails and no tie-breaker is ever injected for those two -
// this is a separate, deeper limitation that adding the composite branch alone does not fix.
// -----------------------------------------------------------------------------------------------
public sealed partial class ToCursorResultAsyncTests {
    public sealed partial class ToCursorResultAsyncMethod {

        [Fact]
        public async Task Should_Traverse_Every_Row_Exactly_Once_When_byte_Key_Duplicates_Without_Explicit_TieBreaker() {
            // Arrange: four rows share the exact same ByteKey (byte) value with distinct Id, seeded
            // out of Id order. Since byte is now covered by the automatic Id tie-breaker injection,
            // ordering by ByteKey alone (no explicit ThenBy(Id)) must still visit every row exactly once.
            (TestDbContext context, SqliteConnection connection) = TestDbContext.CreateInMemoryContext();
            byte shared = (byte)7;

            List<SmallKeyItem> items = [
                new() { Id = 40, ByteKey = shared, Label = "Item_40" },
                new() { Id = 10, ByteKey = shared, Label = "Item_10" },
                new() { Id = 30, ByteKey = shared, Label = "Item_30" },
                new() { Id = 20, ByteKey = shared, Label = "Item_20" },
            ];

            await context.SmallKeyItems.AddRangeAsync(items, TestContext.Current.CancellationToken);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);

            try {
                // Act: traverse forward two-at-a-time with only a single explicit OrderBy level
                List<long> visitedIds = [];
                CursorToken cursor = CursorToken.Empty;
                bool hasNext = true;

                while(hasNext) {
                    CursorRequest request = new(cursor, limit: 2, CursorDirection.Forward);
                    CursorResult<SmallKeyItem> page = await context.SmallKeyItems
                        .OrderBy(x => x.ByteKey)
                        .ToCursorResultAsync(request, x => x.ByteKey, TestContext.Current.CancellationToken);

                    visitedIds.AddRange(page.Items.AsSpan().ToArray().Select(x => x.Id));
                    cursor = page.Metadata.EndCursor;
                    hasNext = page.Metadata.HasNext;
                }

                // Assert: exactly the seeded set, no duplicates, no omissions - proves the byte
                // overload's automatic Id tie-breaker now provides deterministic ordering on ties.
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
        public async Task Should_Traverse_Every_Row_Exactly_Once_When_sbyte_Key_Duplicates_Without_Explicit_TieBreaker() {
            // Arrange: four rows share the exact same SByteKey (sbyte) value with distinct Id, seeded
            // out of Id order. Since sbyte is now covered by the automatic Id tie-breaker injection,
            // ordering by SByteKey alone (no explicit ThenBy(Id)) must still visit every row exactly once.
            (TestDbContext context, SqliteConnection connection) = TestDbContext.CreateInMemoryContext();
            sbyte shared = (sbyte)-7;

            List<SmallKeyItem> items = [
                new() { Id = 40, SByteKey = shared, Label = "Item_40" },
                new() { Id = 10, SByteKey = shared, Label = "Item_10" },
                new() { Id = 30, SByteKey = shared, Label = "Item_30" },
                new() { Id = 20, SByteKey = shared, Label = "Item_20" },
            ];

            await context.SmallKeyItems.AddRangeAsync(items, TestContext.Current.CancellationToken);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);

            try {
                // Act: traverse forward two-at-a-time with only a single explicit OrderBy level
                List<long> visitedIds = [];
                CursorToken cursor = CursorToken.Empty;
                bool hasNext = true;

                while(hasNext) {
                    CursorRequest request = new(cursor, limit: 2, CursorDirection.Forward);
                    CursorResult<SmallKeyItem> page = await context.SmallKeyItems
                        .OrderBy(x => x.SByteKey)
                        .ToCursorResultAsync(request, x => x.SByteKey, TestContext.Current.CancellationToken);

                    visitedIds.AddRange(page.Items.AsSpan().ToArray().Select(x => x.Id));
                    cursor = page.Metadata.EndCursor;
                    hasNext = page.Metadata.HasNext;
                }

                // Assert: exactly the seeded set, no duplicates, no omissions - proves the sbyte
                // overload's automatic Id tie-breaker now provides deterministic ordering on ties.
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
        public async Task Should_Traverse_Every_Row_Exactly_Once_When_short_Key_Duplicates_Without_Explicit_TieBreaker() {
            // Arrange: four rows share the exact same ShortKey (short) value with distinct Id, seeded
            // out of Id order. Since short is now covered by the automatic Id tie-breaker injection,
            // ordering by ShortKey alone (no explicit ThenBy(Id)) must still visit every row exactly once.
            (TestDbContext context, SqliteConnection connection) = TestDbContext.CreateInMemoryContext();
            short shared = (short)700;

            List<SmallKeyItem> items = [
                new() { Id = 40, ShortKey = shared, Label = "Item_40" },
                new() { Id = 10, ShortKey = shared, Label = "Item_10" },
                new() { Id = 30, ShortKey = shared, Label = "Item_30" },
                new() { Id = 20, ShortKey = shared, Label = "Item_20" },
            ];

            await context.SmallKeyItems.AddRangeAsync(items, TestContext.Current.CancellationToken);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);

            try {
                // Act: traverse forward two-at-a-time with only a single explicit OrderBy level
                List<long> visitedIds = [];
                CursorToken cursor = CursorToken.Empty;
                bool hasNext = true;

                while(hasNext) {
                    CursorRequest request = new(cursor, limit: 2, CursorDirection.Forward);
                    CursorResult<SmallKeyItem> page = await context.SmallKeyItems
                        .OrderBy(x => x.ShortKey)
                        .ToCursorResultAsync(request, x => x.ShortKey, TestContext.Current.CancellationToken);

                    visitedIds.AddRange(page.Items.AsSpan().ToArray().Select(x => x.Id));
                    cursor = page.Metadata.EndCursor;
                    hasNext = page.Metadata.HasNext;
                }

                // Assert: exactly the seeded set, no duplicates, no omissions - proves the short
                // overload's automatic Id tie-breaker now provides deterministic ordering on ties.
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
        public async Task Should_Traverse_Every_Row_Exactly_Once_When_ushort_Key_Duplicates_Without_Explicit_TieBreaker() {
            // Arrange: four rows share the exact same UShortKey (ushort) value with distinct Id, seeded
            // out of Id order. Since ushort is now covered by the automatic Id tie-breaker injection,
            // ordering by UShortKey alone (no explicit ThenBy(Id)) must still visit every row exactly once.
            (TestDbContext context, SqliteConnection connection) = TestDbContext.CreateInMemoryContext();
            ushort shared = (ushort)700;

            List<SmallKeyItem> items = [
                new() { Id = 40, UShortKey = shared, Label = "Item_40" },
                new() { Id = 10, UShortKey = shared, Label = "Item_10" },
                new() { Id = 30, UShortKey = shared, Label = "Item_30" },
                new() { Id = 20, UShortKey = shared, Label = "Item_20" },
            ];

            await context.SmallKeyItems.AddRangeAsync(items, TestContext.Current.CancellationToken);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);

            try {
                // Act: traverse forward two-at-a-time with only a single explicit OrderBy level
                List<long> visitedIds = [];
                CursorToken cursor = CursorToken.Empty;
                bool hasNext = true;

                while(hasNext) {
                    CursorRequest request = new(cursor, limit: 2, CursorDirection.Forward);
                    CursorResult<SmallKeyItem> page = await context.SmallKeyItems
                        .OrderBy(x => x.UShortKey)
                        .ToCursorResultAsync(request, x => x.UShortKey, TestContext.Current.CancellationToken);

                    visitedIds.AddRange(page.Items.AsSpan().ToArray().Select(x => x.Id));
                    cursor = page.Metadata.EndCursor;
                    hasNext = page.Metadata.HasNext;
                }

                // Assert: exactly the seeded set, no duplicates, no omissions - proves the ushort
                // overload's automatic Id tie-breaker now provides deterministic ordering on ties.
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
        public async Task Should_Traverse_Every_Row_Exactly_Once_When_uint_Key_Duplicates_Without_Explicit_TieBreaker() {
            // Arrange: four rows share the exact same UIntKey (uint) value with distinct Id, seeded
            // out of Id order. Since uint is now covered by the automatic Id tie-breaker injection,
            // ordering by UIntKey alone (no explicit ThenBy(Id)) must still visit every row exactly once.
            (TestDbContext context, SqliteConnection connection) = TestDbContext.CreateInMemoryContext();
            uint shared = 70000u;

            List<SmallKeyItem> items = [
                new() { Id = 40, UIntKey = shared, Label = "Item_40" },
                new() { Id = 10, UIntKey = shared, Label = "Item_10" },
                new() { Id = 30, UIntKey = shared, Label = "Item_30" },
                new() { Id = 20, UIntKey = shared, Label = "Item_20" },
            ];

            await context.SmallKeyItems.AddRangeAsync(items, TestContext.Current.CancellationToken);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);

            try {
                // Act: traverse forward two-at-a-time with only a single explicit OrderBy level
                List<long> visitedIds = [];
                CursorToken cursor = CursorToken.Empty;
                bool hasNext = true;

                while(hasNext) {
                    CursorRequest request = new(cursor, limit: 2, CursorDirection.Forward);
                    CursorResult<SmallKeyItem> page = await context.SmallKeyItems
                        .OrderBy(x => x.UIntKey)
                        .ToCursorResultAsync(request, x => x.UIntKey, TestContext.Current.CancellationToken);

                    visitedIds.AddRange(page.Items.AsSpan().ToArray().Select(x => x.Id));
                    cursor = page.Metadata.EndCursor;
                    hasNext = page.Metadata.HasNext;
                }

                // Assert: exactly the seeded set, no duplicates, no omissions - proves the uint
                // overload's automatic Id tie-breaker now provides deterministic ordering on ties.
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
        public async Task Should_Traverse_Every_Row_Exactly_Once_When_long_Key_Duplicates_Without_Explicit_TieBreaker() {
            // Arrange: four rows share the exact same CategoryId (long) value with distinct Id, seeded
            // out of Id order. Since long is now covered by the automatic Id tie-breaker injection,
            // ordering by CategoryId alone (no explicit ThenBy(Id)) must still visit every row exactly once.
            (TestDbContext context, SqliteConnection connection) = TestDbContext.CreateInMemoryContext();
            long shared = 700000L;

            List<SmallKeyItem> items = [
                new() { Id = 40, CategoryId = shared, Label = "Item_40" },
                new() { Id = 10, CategoryId = shared, Label = "Item_10" },
                new() { Id = 30, CategoryId = shared, Label = "Item_30" },
                new() { Id = 20, CategoryId = shared, Label = "Item_20" },
            ];

            await context.SmallKeyItems.AddRangeAsync(items, TestContext.Current.CancellationToken);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);

            try {
                // Act: traverse forward two-at-a-time with only a single explicit OrderBy level
                List<long> visitedIds = [];
                CursorToken cursor = CursorToken.Empty;
                bool hasNext = true;

                while(hasNext) {
                    CursorRequest request = new(cursor, limit: 2, CursorDirection.Forward);
                    CursorResult<SmallKeyItem> page = await context.SmallKeyItems
                        .OrderBy(x => x.CategoryId)
                        .ToCursorResultAsync(request, x => x.CategoryId, TestContext.Current.CancellationToken);

                    visitedIds.AddRange(page.Items.AsSpan().ToArray().Select(x => x.Id));
                    cursor = page.Metadata.EndCursor;
                    hasNext = page.Metadata.HasNext;
                }

                // Assert: exactly the seeded set, no duplicates, no omissions - proves the long
                // overload's automatic Id tie-breaker now provides deterministic ordering on ties.
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
        public async Task Should_Traverse_Every_Row_Exactly_Once_When_ulong_Key_Duplicates_Without_Explicit_TieBreaker() {
            // Arrange: four rows share the exact same ULongKey (ulong) value with distinct Id, seeded
            // out of Id order. Since ulong is now covered by the automatic Id tie-breaker injection,
            // ordering by ULongKey alone (no explicit ThenBy(Id)) must still visit every row exactly once.
            (TestDbContext context, SqliteConnection connection) = TestDbContext.CreateInMemoryContext();
            ulong shared = 70000ul;

            List<SmallKeyItem> items = [
                new() { Id = 40, ULongKey = shared, Label = "Item_40" },
                new() { Id = 10, ULongKey = shared, Label = "Item_10" },
                new() { Id = 30, ULongKey = shared, Label = "Item_30" },
                new() { Id = 20, ULongKey = shared, Label = "Item_20" },
            ];

            await context.SmallKeyItems.AddRangeAsync(items, TestContext.Current.CancellationToken);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);

            try {
                // Act: traverse forward two-at-a-time with only a single explicit OrderBy level
                List<long> visitedIds = [];
                CursorToken cursor = CursorToken.Empty;
                bool hasNext = true;

                while(hasNext) {
                    CursorRequest request = new(cursor, limit: 2, CursorDirection.Forward);
                    CursorResult<SmallKeyItem> page = await context.SmallKeyItems
                        .OrderBy(x => x.ULongKey)
                        .ToCursorResultAsync(request, x => x.ULongKey, TestContext.Current.CancellationToken);

                    visitedIds.AddRange(page.Items.AsSpan().ToArray().Select(x => x.Id));
                    cursor = page.Metadata.EndCursor;
                    hasNext = page.Metadata.HasNext;
                }

                // Assert: exactly the seeded set, no duplicates, no omissions - proves the ulong
                // overload's automatic Id tie-breaker now provides deterministic ordering on ties.
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
        public async Task Should_Traverse_Every_Row_Exactly_Once_When_Int128_Key_Duplicates_Without_Explicit_TieBreaker() {
            // Arrange: four rows share the exact same Int128Key (Int128) value with distinct Id, seeded
            // out of Id order. Since Int128 is now covered by the automatic Id tie-breaker injection,
            // ordering by Int128Key alone (no explicit ThenBy(Id)) must still visit every row exactly once.
            (TestDbContext context, SqliteConnection connection) = TestDbContext.CreateInMemoryContext();
            Int128 shared = (Int128)700000;

            List<SmallKeyItem> items = [
                new() { Id = 40, Int128Key = shared, Label = "Item_40" },
                new() { Id = 10, Int128Key = shared, Label = "Item_10" },
                new() { Id = 30, Int128Key = shared, Label = "Item_30" },
                new() { Id = 20, Int128Key = shared, Label = "Item_20" },
            ];

            await context.SmallKeyItems.AddRangeAsync(items, TestContext.Current.CancellationToken);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);

            try {
                // Act: traverse forward two-at-a-time with only a single explicit OrderBy level
                List<long> visitedIds = [];
                CursorToken cursor = CursorToken.Empty;
                bool hasNext = true;

                while(hasNext) {
                    CursorRequest request = new(cursor, limit: 2, CursorDirection.Forward);
                    CursorResult<SmallKeyItem> page = await context.SmallKeyItems
                        .OrderBy(x => x.Int128Key)
                        .ToCursorResultAsync(request, x => x.Int128Key, TestContext.Current.CancellationToken);

                    visitedIds.AddRange(page.Items.AsSpan().ToArray().Select(x => x.Id));
                    cursor = page.Metadata.EndCursor;
                    hasNext = page.Metadata.HasNext;
                }

                // Assert: exactly the seeded set, no duplicates, no omissions - proves the Int128
                // overload's automatic Id tie-breaker now provides deterministic ordering on ties.
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
        public async Task Should_Traverse_Every_Row_Exactly_Once_When_UInt128_Key_Duplicates_Without_Explicit_TieBreaker() {
            // Arrange: four rows share the exact same UInt128Key (UInt128) value with distinct Id, seeded
            // out of Id order. Since UInt128 is now covered by the automatic Id tie-breaker injection,
            // ordering by UInt128Key alone (no explicit ThenBy(Id)) must still visit every row exactly once.
            (TestDbContext context, SqliteConnection connection) = TestDbContext.CreateInMemoryContext();
            UInt128 shared = (UInt128)700000;

            List<SmallKeyItem> items = [
                new() { Id = 40, UInt128Key = shared, Label = "Item_40" },
                new() { Id = 10, UInt128Key = shared, Label = "Item_10" },
                new() { Id = 30, UInt128Key = shared, Label = "Item_30" },
                new() { Id = 20, UInt128Key = shared, Label = "Item_20" },
            ];

            await context.SmallKeyItems.AddRangeAsync(items, TestContext.Current.CancellationToken);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);

            try {
                // Act: traverse forward two-at-a-time with only a single explicit OrderBy level
                List<long> visitedIds = [];
                CursorToken cursor = CursorToken.Empty;
                bool hasNext = true;

                while(hasNext) {
                    CursorRequest request = new(cursor, limit: 2, CursorDirection.Forward);
                    CursorResult<SmallKeyItem> page = await context.SmallKeyItems
                        .OrderBy(x => x.UInt128Key)
                        .ToCursorResultAsync(request, x => x.UInt128Key, TestContext.Current.CancellationToken);

                    visitedIds.AddRange(page.Items.AsSpan().ToArray().Select(x => x.Id));
                    cursor = page.Metadata.EndCursor;
                    hasNext = page.Metadata.HasNext;
                }

                // Assert: exactly the seeded set, no duplicates, no omissions - proves the UInt128
                // overload's automatic Id tie-breaker now provides deterministic ordering on ties.
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
        public async Task Should_Traverse_Every_Row_Exactly_Once_When_double_Key_Duplicates_Without_Explicit_TieBreaker() {
            // Arrange: four rows share the exact same DoubleKey (double) value with distinct Id, seeded
            // out of Id order. Since double is now covered by the automatic Id tie-breaker injection,
            // ordering by DoubleKey alone (no explicit ThenBy(Id)) must still visit every row exactly once.
            (TestDbContext context, SqliteConnection connection) = TestDbContext.CreateInMemoryContext();
            double shared = 70.5d;

            List<SmallKeyItem> items = [
                new() { Id = 40, DoubleKey = shared, Label = "Item_40" },
                new() { Id = 10, DoubleKey = shared, Label = "Item_10" },
                new() { Id = 30, DoubleKey = shared, Label = "Item_30" },
                new() { Id = 20, DoubleKey = shared, Label = "Item_20" },
            ];

            await context.SmallKeyItems.AddRangeAsync(items, TestContext.Current.CancellationToken);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);

            try {
                // Act: traverse forward two-at-a-time with only a single explicit OrderBy level
                List<long> visitedIds = [];
                CursorToken cursor = CursorToken.Empty;
                bool hasNext = true;

                while(hasNext) {
                    CursorRequest request = new(cursor, limit: 2, CursorDirection.Forward);
                    CursorResult<SmallKeyItem> page = await context.SmallKeyItems
                        .OrderBy(x => x.DoubleKey)
                        .ToCursorResultAsync(request, x => x.DoubleKey, TestContext.Current.CancellationToken);

                    visitedIds.AddRange(page.Items.AsSpan().ToArray().Select(x => x.Id));
                    cursor = page.Metadata.EndCursor;
                    hasNext = page.Metadata.HasNext;
                }

                // Assert: exactly the seeded set, no duplicates, no omissions - proves the double
                // overload's automatic Id tie-breaker now provides deterministic ordering on ties.
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
        public async Task Should_Traverse_Every_Row_Exactly_Once_When_float_Key_Duplicates_Without_Explicit_TieBreaker() {
            // Arrange: four rows share the exact same FloatKey (float) value with distinct Id, seeded
            // out of Id order. Since float is now covered by the automatic Id tie-breaker injection,
            // ordering by FloatKey alone (no explicit ThenBy(Id)) must still visit every row exactly once.
            (TestDbContext context, SqliteConnection connection) = TestDbContext.CreateInMemoryContext();
            float shared = 70.5f;

            List<SmallKeyItem> items = [
                new() { Id = 40, FloatKey = shared, Label = "Item_40" },
                new() { Id = 10, FloatKey = shared, Label = "Item_10" },
                new() { Id = 30, FloatKey = shared, Label = "Item_30" },
                new() { Id = 20, FloatKey = shared, Label = "Item_20" },
            ];

            await context.SmallKeyItems.AddRangeAsync(items, TestContext.Current.CancellationToken);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);

            try {
                // Act: traverse forward two-at-a-time with only a single explicit OrderBy level
                List<long> visitedIds = [];
                CursorToken cursor = CursorToken.Empty;
                bool hasNext = true;

                while(hasNext) {
                    CursorRequest request = new(cursor, limit: 2, CursorDirection.Forward);
                    CursorResult<SmallKeyItem> page = await context.SmallKeyItems
                        .OrderBy(x => x.FloatKey)
                        .ToCursorResultAsync(request, x => x.FloatKey, TestContext.Current.CancellationToken);

                    visitedIds.AddRange(page.Items.AsSpan().ToArray().Select(x => x.Id));
                    cursor = page.Metadata.EndCursor;
                    hasNext = page.Metadata.HasNext;
                }

                // Assert: exactly the seeded set, no duplicates, no omissions - proves the float
                // overload's automatic Id tie-breaker now provides deterministic ordering on ties.
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
        public async Task Should_Traverse_Every_Row_Exactly_Once_When_Half_Key_Duplicates_Without_Explicit_TieBreaker() {
            // Arrange: four rows share the exact same HalfKey (Half) value with distinct Id, seeded
            // out of Id order. Since Half is now covered by the automatic Id tie-breaker injection,
            // ordering by HalfKey alone (no explicit ThenBy(Id)) must still visit every row exactly once.
            (TestDbContext context, SqliteConnection connection) = TestDbContext.CreateInMemoryContext();
            Half shared = (Half)70.5f;

            List<SmallKeyItem> items = [
                new() { Id = 40, HalfKey = shared, Label = "Item_40" },
                new() { Id = 10, HalfKey = shared, Label = "Item_10" },
                new() { Id = 30, HalfKey = shared, Label = "Item_30" },
                new() { Id = 20, HalfKey = shared, Label = "Item_20" },
            ];

            await context.SmallKeyItems.AddRangeAsync(items, TestContext.Current.CancellationToken);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);

            try {
                // Act: traverse forward two-at-a-time with only a single explicit OrderBy level
                List<long> visitedIds = [];
                CursorToken cursor = CursorToken.Empty;
                bool hasNext = true;

                while(hasNext) {
                    CursorRequest request = new(cursor, limit: 2, CursorDirection.Forward);
                    CursorResult<SmallKeyItem> page = await context.SmallKeyItems
                        .OrderBy(x => x.HalfKey)
                        .ToCursorResultAsync(request, x => x.HalfKey, TestContext.Current.CancellationToken);

                    visitedIds.AddRange(page.Items.AsSpan().ToArray().Select(x => x.Id));
                    cursor = page.Metadata.EndCursor;
                    hasNext = page.Metadata.HasNext;
                }

                // Assert: exactly the seeded set, no duplicates, no omissions - proves the Half
                // overload's automatic Id tie-breaker now provides deterministic ordering on ties.
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
        public async Task Should_Traverse_Every_Row_Exactly_Once_When_DateOnly_Key_Duplicates_Without_Explicit_TieBreaker() {
            // Arrange: four rows share the exact same DateOnlyKey (DateOnly) value with distinct Id, seeded
            // out of Id order. Since DateOnly is now covered by the automatic Id tie-breaker injection,
            // ordering by DateOnlyKey alone (no explicit ThenBy(Id)) must still visit every row exactly once.
            (TestDbContext context, SqliteConnection connection) = TestDbContext.CreateInMemoryContext();
            DateOnly shared = new(2026, 5, 1);

            List<SmallKeyItem> items = [
                new() { Id = 40, DateOnlyKey = shared, Label = "Item_40" },
                new() { Id = 10, DateOnlyKey = shared, Label = "Item_10" },
                new() { Id = 30, DateOnlyKey = shared, Label = "Item_30" },
                new() { Id = 20, DateOnlyKey = shared, Label = "Item_20" },
            ];

            await context.SmallKeyItems.AddRangeAsync(items, TestContext.Current.CancellationToken);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);

            try {
                // Act: traverse forward two-at-a-time with only a single explicit OrderBy level
                List<long> visitedIds = [];
                CursorToken cursor = CursorToken.Empty;
                bool hasNext = true;

                while(hasNext) {
                    CursorRequest request = new(cursor, limit: 2, CursorDirection.Forward);
                    CursorResult<SmallKeyItem> page = await context.SmallKeyItems
                        .OrderBy(x => x.DateOnlyKey)
                        .ToCursorResultAsync(request, x => x.DateOnlyKey, TestContext.Current.CancellationToken);

                    visitedIds.AddRange(page.Items.AsSpan().ToArray().Select(x => x.Id));
                    cursor = page.Metadata.EndCursor;
                    hasNext = page.Metadata.HasNext;
                }

                // Assert: exactly the seeded set, no duplicates, no omissions - proves the DateOnly
                // overload's automatic Id tie-breaker now provides deterministic ordering on ties.
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
        public async Task Should_Traverse_Every_Row_Exactly_Once_When_TimeOnly_Key_Duplicates_Without_Explicit_TieBreaker() {
            // Arrange: four rows share the exact same TimeOnlyKey (TimeOnly) value with distinct Id, seeded
            // out of Id order. Since TimeOnly is now covered by the automatic Id tie-breaker injection,
            // ordering by TimeOnlyKey alone (no explicit ThenBy(Id)) must still visit every row exactly once.
            (TestDbContext context, SqliteConnection connection) = TestDbContext.CreateInMemoryContext();
            TimeOnly shared = new TimeOnly(10, 30, 0);

            List<SmallKeyItem> items = [
                new() { Id = 40, TimeOnlyKey = shared, Label = "Item_40" },
                new() { Id = 10, TimeOnlyKey = shared, Label = "Item_10" },
                new() { Id = 30, TimeOnlyKey = shared, Label = "Item_30" },
                new() { Id = 20, TimeOnlyKey = shared, Label = "Item_20" },
            ];

            await context.SmallKeyItems.AddRangeAsync(items, TestContext.Current.CancellationToken);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);

            try {
                // Act: traverse forward two-at-a-time with only a single explicit OrderBy level
                List<long> visitedIds = [];
                CursorToken cursor = CursorToken.Empty;
                bool hasNext = true;

                while(hasNext) {
                    CursorRequest request = new(cursor, limit: 2, CursorDirection.Forward);
                    CursorResult<SmallKeyItem> page = await context.SmallKeyItems
                        .OrderBy(x => x.TimeOnlyKey)
                        .ToCursorResultAsync(request, x => x.TimeOnlyKey, TestContext.Current.CancellationToken);

                    visitedIds.AddRange(page.Items.AsSpan().ToArray().Select(x => x.Id));
                    cursor = page.Metadata.EndCursor;
                    hasNext = page.Metadata.HasNext;
                }

                // Assert: exactly the seeded set, no duplicates, no omissions - proves the TimeOnly
                // overload's automatic Id tie-breaker now provides deterministic ordering on ties.
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
        public async Task Should_Traverse_Every_Row_Exactly_Once_When_TimeSpan_Key_Duplicates_Without_Explicit_TieBreaker() {
            // Arrange: four rows share the exact same TimeSpanKey (TimeSpan) value with distinct Id, seeded
            // out of Id order. Since TimeSpan is now covered by the automatic Id tie-breaker injection,
            // ordering by TimeSpanKey alone (no explicit ThenBy(Id)) must still visit every row exactly once.
            (TestDbContext context, SqliteConnection connection) = TestDbContext.CreateInMemoryContext();
            TimeSpan shared = TimeSpan.FromMinutes(90);

            List<SmallKeyItem> items = [
                new() { Id = 40, TimeSpanKey = shared, Label = "Item_40" },
                new() { Id = 10, TimeSpanKey = shared, Label = "Item_10" },
                new() { Id = 30, TimeSpanKey = shared, Label = "Item_30" },
                new() { Id = 20, TimeSpanKey = shared, Label = "Item_20" },
            ];

            await context.SmallKeyItems.AddRangeAsync(items, TestContext.Current.CancellationToken);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);

            try {
                // Act: traverse forward two-at-a-time with only a single explicit OrderBy level
                List<long> visitedIds = [];
                CursorToken cursor = CursorToken.Empty;
                bool hasNext = true;

                while(hasNext) {
                    CursorRequest request = new(cursor, limit: 2, CursorDirection.Forward);
                    CursorResult<SmallKeyItem> page = await context.SmallKeyItems
                        .OrderBy(x => x.TimeSpanKey)
                        .ToCursorResultAsync(request, x => x.TimeSpanKey, TestContext.Current.CancellationToken);

                    visitedIds.AddRange(page.Items.AsSpan().ToArray().Select(x => x.Id));
                    cursor = page.Metadata.EndCursor;
                    hasNext = page.Metadata.HasNext;
                }

                // Assert: exactly the seeded set, no duplicates, no omissions - proves the TimeSpan
                // overload's automatic Id tie-breaker now provides deterministic ordering on ties.
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
        public async Task Should_Traverse_Every_Row_Exactly_Once_When_char_Key_Duplicates_Without_Explicit_TieBreaker() {
            // Arrange: four rows share the exact same CharKey (char) value with distinct Id, seeded
            // out of Id order. Since char is now covered by the automatic Id tie-breaker injection,
            // ordering by CharKey alone (no explicit ThenBy(Id)) must still visit every row exactly once.
            (TestDbContext context, SqliteConnection connection) = TestDbContext.CreateInMemoryContext();
            char shared = 'Q';

            List<SmallKeyItem> items = [
                new() { Id = 40, CharKey = shared, Label = "Item_40" },
                new() { Id = 10, CharKey = shared, Label = "Item_10" },
                new() { Id = 30, CharKey = shared, Label = "Item_30" },
                new() { Id = 20, CharKey = shared, Label = "Item_20" },
            ];

            await context.SmallKeyItems.AddRangeAsync(items, TestContext.Current.CancellationToken);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);

            try {
                // Act: traverse forward two-at-a-time with only a single explicit OrderBy level
                List<long> visitedIds = [];
                CursorToken cursor = CursorToken.Empty;
                bool hasNext = true;

                while(hasNext) {
                    CursorRequest request = new(cursor, limit: 2, CursorDirection.Forward);
                    CursorResult<SmallKeyItem> page = await context.SmallKeyItems
                        .OrderBy(x => x.CharKey)
                        .ToCursorResultAsync(request, x => x.CharKey, TestContext.Current.CancellationToken);

                    visitedIds.AddRange(page.Items.AsSpan().ToArray().Select(x => x.Id));
                    cursor = page.Metadata.EndCursor;
                    hasNext = page.Metadata.HasNext;
                }

                // Assert: exactly the seeded set, no duplicates, no omissions - proves the char
                // overload's automatic Id tie-breaker now provides deterministic ordering on ties.
                Assert.Equal(4, visitedIds.Count);
                Assert.Equal(visitedIds.Distinct().Count(), visitedIds.Count);
                Assert.Equal([10L, 20L, 30L, 40L], visitedIds.Order());
            }
            finally {
                await context.DisposeAsync();
                await connection.DisposeAsync();
            }
        }

    }
}