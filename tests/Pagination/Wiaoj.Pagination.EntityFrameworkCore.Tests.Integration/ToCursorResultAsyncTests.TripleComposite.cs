using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Wiaoj.Preconditions.Exceptions;

namespace Wiaoj.Pagination.EntityFrameworkCore.Tests.Integration;

public sealed partial class ToCursorResultAsyncTests {
    public sealed partial class ToCursorResultAsyncMethod {

        [Fact]
        public async Task Should_Order_By_Primary_Then_Secondary_Then_TieBreaker_When_All_Levels_Tie() {
            // Arrange
            (TestDbContext context, SqliteConnection connection) = TestDbContext.CreateInMemoryContext();
            DateTimeOffset sharedTimestamp = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

            List<TestItem> items = [
                new() { Id = 4, Name = "Item_4", Price = 100m, CreatedAt = sharedTimestamp },
                new() { Id = 1, Name = "Item_1", Price = 100m, CreatedAt = sharedTimestamp },
                new() { Id = 3, Name = "Item_3", Price = 100m, CreatedAt = sharedTimestamp },
                new() { Id = 2, Name = "Item_2", Price = 100m, CreatedAt = sharedTimestamp },
            ];

            await context.Items.AddRangeAsync(items, TestContext.Current.CancellationToken);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);

            try {
                // Act
                CursorRequest page1Request = new(CursorToken.Empty, limit: 2, CursorDirection.Forward);
                CursorResult<TestItem> page1 = await context.Items
                    .OrderBy(x => x.CreatedAt)
                    .ThenBy(x => x.Price)
                    .ThenBy(x => x.Id)
                    .ToCursorResultAsync(page1Request, x => x.CreatedAt, x => x.Price, x => x.Id, TestContext.Current.CancellationToken);

                CursorRequest page2Request = new(page1.Metadata.EndCursor, limit: 2, CursorDirection.Forward);
                CursorResult<TestItem> page2 = await context.Items
                    .OrderBy(x => x.CreatedAt)
                    .ThenBy(x => x.Price)
                    .ThenBy(x => x.Id)
                    .ToCursorResultAsync(page2Request, x => x.CreatedAt, x => x.Price, x => x.Id, TestContext.Current.CancellationToken);

                // Assert
                Assert.Equal(2, page1.Count);
                Assert.Equal(1, page1.Items[0].Id);
                Assert.Equal(2, page1.Items[1].Id);

                Assert.Equal(2, page2.Count);
                Assert.Equal(3, page2.Items[0].Id);
                Assert.Equal(4, page2.Items[1].Id);
                Assert.False(page2.Metadata.HasNext);
            }
            finally {
                await context.DisposeAsync();
                await connection.DisposeAsync();
            }
        }

        [Fact]
        public async Task Should_Break_Ties_On_Secondary_Key_When_Primary_Key_Matches() {
            // Arrange: all four items share CreatedAt, so ordering must fall through to Price (secondary)
            (TestDbContext context, SqliteConnection connection) = TestDbContext.CreateInMemoryContext();
            DateTimeOffset sharedTimestamp = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

            List<TestItem> items = [
                new() { Id = 10, Name = "Item_10", Price = 200m, CreatedAt = sharedTimestamp },
                new() { Id = 20, Name = "Item_20", Price = 100m, CreatedAt = sharedTimestamp },
                new() { Id = 30, Name = "Item_30", Price = 100m, CreatedAt = sharedTimestamp },
                new() { Id = 40, Name = "Item_40", Price = 300m, CreatedAt = sharedTimestamp },
            ];

            await context.Items.AddRangeAsync(items, TestContext.Current.CancellationToken);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);

            try {
                // Act: expected order is Price 100(Id 20,30) -> Price 200(Id 10) -> Price 300(Id 40)
                CursorRequest page1Request = new(CursorToken.Empty, limit: 2, CursorDirection.Forward);
                CursorResult<TestItem> page1 = await context.Items
                    .OrderBy(x => x.CreatedAt)
                    .ThenBy(x => x.Price)
                    .ThenBy(x => x.Id)
                    .ToCursorResultAsync(page1Request, x => x.CreatedAt, x => x.Price, x => x.Id, TestContext.Current.CancellationToken);

                CursorRequest page2Request = new(page1.Metadata.EndCursor, limit: 2, CursorDirection.Forward);
                CursorResult<TestItem> page2 = await context.Items
                    .OrderBy(x => x.CreatedAt)
                    .ThenBy(x => x.Price)
                    .ThenBy(x => x.Id)
                    .ToCursorResultAsync(page2Request, x => x.CreatedAt, x => x.Price, x => x.Id, TestContext.Current.CancellationToken);

                // Assert
                Assert.Equal(2, page1.Count);
                Assert.Equal(20, page1.Items[0].Id);
                Assert.Equal(30, page1.Items[1].Id);

                Assert.Equal(2, page2.Count);
                Assert.Equal(10, page2.Items[0].Id);
                Assert.Equal(40, page2.Items[1].Id);
            }
            finally {
                await context.DisposeAsync();
                await connection.DisposeAsync();
            }
        }

        [Fact]
        public async Task Should_Break_Ties_On_TieBreaker_Key_When_Primary_And_Secondary_Both_Match() {
            // Arrange: CreatedAt and Price identical across all items - only Id can disambiguate order
            (TestDbContext context, SqliteConnection connection) = TestDbContext.CreateInMemoryContext();
            DateTimeOffset sharedTimestamp = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

            List<TestItem> items = [
                new() { Id = 8, Name = "Item_8", Price = 100m, CreatedAt = sharedTimestamp },
                new() { Id = 3, Name = "Item_3", Price = 100m, CreatedAt = sharedTimestamp },
                new() { Id = 5, Name = "Item_5", Price = 100m, CreatedAt = sharedTimestamp },
                new() { Id = 1, Name = "Item_1", Price = 100m, CreatedAt = sharedTimestamp },
            ];

            await context.Items.AddRangeAsync(items, TestContext.Current.CancellationToken);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);

            try {
                // Act
                CursorRequest page1Request = new(CursorToken.Empty, limit: 2, CursorDirection.Forward);
                CursorResult<TestItem> page1 = await context.Items
                    .OrderBy(x => x.CreatedAt)
                    .ThenBy(x => x.Price)
                    .ThenBy(x => x.Id)
                    .ToCursorResultAsync(page1Request, x => x.CreatedAt, x => x.Price, x => x.Id, TestContext.Current.CancellationToken);

                CursorRequest page2Request = new(page1.Metadata.EndCursor, limit: 2, CursorDirection.Forward);
                CursorResult<TestItem> page2 = await context.Items
                    .OrderBy(x => x.CreatedAt)
                    .ThenBy(x => x.Price)
                    .ThenBy(x => x.Id)
                    .ToCursorResultAsync(page2Request, x => x.CreatedAt, x => x.Price, x => x.Id, TestContext.Current.CancellationToken);

                // Assert: Id ascending order, 1, 3, 5, 8
                Assert.Equal(2, page1.Count);
                Assert.Equal(1, page1.Items[0].Id);
                Assert.Equal(3, page1.Items[1].Id);

                Assert.Equal(2, page2.Count);
                Assert.Equal(5, page2.Items[0].Id);
                Assert.Equal(8, page2.Items[1].Id);
            }
            finally {
                await context.DisposeAsync();
                await connection.DisposeAsync();
            }
        }

        [Fact]
        public async Task Should_Paginate_Backward_Correctly_Across_Three_Levels() {
            // Arrange
            (TestDbContext context, SqliteConnection connection) = TestDbContext.CreateInMemoryContext();
            DateTimeOffset t0 = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
            DateTimeOffset t1 = t0.AddMinutes(1);

            List<TestItem> items = [
                new() { Id = 1, Name = "Item_1", Price = 100m, CreatedAt = t0 },
                new() { Id = 2, Name = "Item_2", Price = 100m, CreatedAt = t0 },
                new() { Id = 3, Name = "Item_3", Price = 200m, CreatedAt = t0 },
                new() { Id = 4, Name = "Item_4", Price = 50m, CreatedAt = t1 },
                new() { Id = 5, Name = "Item_5", Price = 50m, CreatedAt = t1 },
                new() { Id = 6, Name = "Item_6", Price = 75m, CreatedAt = t1 },
            ];

            await context.Items.AddRangeAsync(items, TestContext.Current.CancellationToken);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);

            try {
                // Forward navigation to obtain a genuine server-generated boundary cursor at item 4
                CursorResult<TestItem> page1 = await context.Items
                    .OrderBy(x => x.CreatedAt)
                    .ThenBy(x => x.Price)
                    .ThenBy(x => x.Id)
                    .ToCursorResultAsync(new CursorRequest(CursorToken.Empty, limit: 4, CursorDirection.Forward), x => x.CreatedAt, x => x.Price, x => x.Id, TestContext.Current.CancellationToken);

                // Act: seek backward from item 4's boundary with limit 2 (expect items 2, 3)
                CursorRequest backwardRequest = new(page1.Metadata.EndCursor, limit: 2, CursorDirection.Backward);
                CursorResult<TestItem> backwardResult = await context.Items
                    .OrderBy(x => x.CreatedAt)
                    .ThenBy(x => x.Price)
                    .ThenBy(x => x.Id)
                    .ToCursorResultAsync(backwardRequest, x => x.CreatedAt, x => x.Price, x => x.Id, TestContext.Current.CancellationToken);

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
        public async Task Should_Support_Mixed_Sort_Directions_Across_Three_Levels() {
            // Arrange: Primary (CreatedAt) DESC, Secondary (Price) ASC, TieBreaker (Id) DESC
            (TestDbContext context, SqliteConnection connection) = TestDbContext.CreateInMemoryContext();
            DateTimeOffset earlier = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
            DateTimeOffset later = earlier.AddMinutes(1);

            List<TestItem> items = [
                new() { Id = 1, Name = "Item_1", Price = 50m, CreatedAt = later },
                new() { Id = 2, Name = "Item_2", Price = 50m, CreatedAt = later },
                new() { Id = 3, Name = "Item_3", Price = 80m, CreatedAt = later },
                new() { Id = 4, Name = "Item_4", Price = 999m, CreatedAt = earlier },
            ];

            await context.Items.AddRangeAsync(items, TestContext.Current.CancellationToken);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);

            try {
                // Act: expected order is 2, 1, 3, 4
                CursorRequest page1Request = new(CursorToken.Empty, limit: 2, CursorDirection.Forward);
                CursorResult<TestItem> page1 = await context.Items
                    .OrderByDescending(x => x.CreatedAt)
                    .ThenBy(x => x.Price)
                    .ThenByDescending(x => x.Id)
                    .ToCursorResultAsync(page1Request, x => x.CreatedAt, x => x.Price, x => x.Id, TestContext.Current.CancellationToken);

                CursorRequest page2Request = new(page1.Metadata.EndCursor, limit: 2, CursorDirection.Forward);
                CursorResult<TestItem> page2 = await context.Items
                    .OrderByDescending(x => x.CreatedAt)
                    .ThenBy(x => x.Price)
                    .ThenByDescending(x => x.Id)
                    .ToCursorResultAsync(page2Request, x => x.CreatedAt, x => x.Price, x => x.Id, TestContext.Current.CancellationToken);

                // Assert
                Assert.Equal(2, page1.Count);
                Assert.Equal(2, page1.Items[0].Id);
                Assert.Equal(1, page1.Items[1].Id);

                Assert.Equal(2, page2.Count);
                Assert.Equal(3, page2.Items[0].Id);
                Assert.Equal(4, page2.Items[1].Id);
                Assert.False(page2.Metadata.HasNext);
            }
            finally {
                await context.DisposeAsync();
                await connection.DisposeAsync();
            }
        }

        [Fact]
        public async Task Should_Return_Empty_Result_When_Database_Is_Empty_For_Triple_Composite() {
            // Arrange
            (TestDbContext emptyContext, SqliteConnection connection) = TestDbContext.CreateInMemoryContext();
            CursorRequest request = CursorRequest.Default;

            try {
                // Act
                CursorResult<TestItem> result = await emptyContext.Items
                    .OrderBy(x => x.CreatedAt)
                    .ThenBy(x => x.Price)
                    .ThenBy(x => x.Id)
                    .ToCursorResultAsync(request, x => x.CreatedAt, x => x.Price, x => x.Id, TestContext.Current.CancellationToken);

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
        public async Task Should_Throw_When_Source_Is_Null_For_Triple_Composite() {
            // Arrange
            IQueryable<TestItem> nullQuery = null!;
            CursorRequest request = CursorRequest.Default;

            // Act & Assert
            await Assert.ThrowsAsync<PrecaArgumentNullException>(() =>
                nullQuery.ToCursorResultAsync(request, x => x.CreatedAt, x => x.Price, x => x.Id, TestContext.Current.CancellationToken));
        }

        // --- Additional OrderBy-chain / limit-clamping edge cases for the 3-key overload -------

        [Fact]
        public async Task Should_Throw_InvalidOperationException_When_Query_Has_No_Explicit_OrderBy_For_TripleComposite() {
            // Arrange
            (TestDbContext context, SqliteConnection connection) = TestDbContext.CreateInMemoryContext();

            try {
                CursorRequest request = new(CursorToken.Empty, limit: 10, CursorDirection.Forward);

                // Act & Assert
                await Assert.ThrowsAsync<InvalidOperationException>(() =>
                    context.Items
                        .ToCursorResultAsync(request, x => x.CreatedAt, x => x.Price, x => x.Id, TestContext.Current.CancellationToken));
            }
            finally {
                await context.DisposeAsync();
                await connection.DisposeAsync();
            }
        }

        [Fact]
        public async Task Should_Throw_InvalidOperationException_When_OrderBy_Chain_Exceeds_Three_Levels_For_TripleComposite() {
            // Arrange: 4 ordering levels supplied against a 3-key selector call
            (TestDbContext context, SqliteConnection connection) = TestDbContext.CreateInMemoryContext();

            try {
                CursorRequest request = new(CursorToken.Empty, limit: 10, CursorDirection.Forward);

                // Act & Assert
                await Assert.ThrowsAsync<InvalidOperationException>(() =>
                    context.Items
                        .OrderBy(x => x.CreatedAt)
                        .ThenBy(x => x.Price)
                        .ThenBy(x => x.Name)
                        .ThenBy(x => x.Id)
                        .ToCursorResultAsync(request, x => x.CreatedAt, x => x.Price, x => x.Id, TestContext.Current.CancellationToken));
            }
            finally {
                await context.DisposeAsync();
                await connection.DisposeAsync();
            }
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(-999)]
        public async Task Should_Clamp_Limit_To_Default_When_Zero_Or_Negative_For_TripleComposite(int invalidLimit) {
            // Arrange
            (TestDbContext context, SqliteConnection connection) = TestDbContext.CreateInMemoryContext();
            List<TestItem> items = [.. Enumerable.Range(1, 40).Select(i => new TestItem {
                Id = i,
                Name = $"Item_{i}",
                Price = i,
                CreatedAt = DateTimeOffset.UtcNow.AddMinutes(i)
            })];
            await context.Items.AddRangeAsync(items, TestContext.Current.CancellationToken);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);

            try {
                CursorRequest request = new(CursorToken.Empty, invalidLimit, CursorDirection.Forward);
                Assert.Equal(CursorRequest.DefaultLimit, request.Limit);

                // Act
                CursorResult<TestItem> result = await context.Items
                    .OrderBy(x => x.CreatedAt)
                    .ThenBy(x => x.Price)
                    .ThenBy(x => x.Id)
                    .ToCursorResultAsync(request, x => x.CreatedAt, x => x.Price, x => x.Id, TestContext.Current.CancellationToken);

                // Assert
                Assert.Equal(CursorRequest.DefaultLimit, result.Count);
            }
            finally {
                await context.DisposeAsync();
                await connection.DisposeAsync();
            }
        }

    }
}