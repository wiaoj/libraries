using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Wiaoj.Preconditions.Exceptions;

namespace Wiaoj.Pagination.EntityFrameworkCore.Tests.Integration;

[Trait("Category", "Integration")]
[Trait("Subsystem", "EntityFrameworkCore")]
public sealed class ToPagedResultAsyncTests : IAsyncLifetime {
    private TestDbContext _context = null!;
    private SqliteConnection _connection = null!;

    public async ValueTask InitializeAsync() {
        (this._context, this._connection) = TestDbContext.CreateInMemoryContext();

        // Seed 50 test records
        IEnumerable<TestItem> items = Enumerable.Range(1, 50).Select(i => new TestItem {
            Id = i,
            Name = $"Item_{i:D2}",
            Price = i * 10.5m,
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(i)
        });

        await this._context.Items.AddRangeAsync(items, TestContext.Current.CancellationToken);
        await this._context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync() {
        await this._context.DisposeAsync();
        await this._connection.DisposeAsync();
    }

    public sealed class ToPagedResultAsyncMethod : IClassFixture<ToPagedResultAsyncTests> {
        private readonly ToPagedResultAsyncTests _fixture;

        public ToPagedResultAsyncMethod() {
            this._fixture = new ToPagedResultAsyncTests();
            this._fixture.InitializeAsync().GetAwaiter().GetResult();
        }

        [Fact]
        public async Task Should_Paginate_First_Page_Accurately() {
            // Arrange
            PageRequest request = new(pageNumber: 1, pageSize: 10);

            // Act
            PagedResult<TestItem> result = await this._fixture._context.Items
                .OrderBy(x => x.Id)
                .ToPagedResultAsync(request, TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(10, result.Count);
            Assert.Equal(50, result.Metadata.TotalCount);
            Assert.Equal(1, result.Metadata.PageNumber);
            Assert.Equal(10, result.Metadata.PageSize);
            Assert.Equal(5, result.Metadata.TotalPages);
            Assert.False(result.Metadata.HasPrevious);
            Assert.True(result.Metadata.HasNext);
            Assert.Equal(1, result.Items[0].Id);
            Assert.Equal(10, result.Items[^1].Id);
        }

        [Fact]
        public async Task Should_Paginate_Middle_Page_Accurately() {
            // Arrange
            PageRequest request = new(pageNumber: 3, pageSize: 10);

            // Act
            PagedResult<TestItem> result = await this._fixture._context.Items
                .OrderBy(x => x.Id)
                .ToPagedResultAsync(request, TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(10, result.Count);
            Assert.Equal(21, result.Items[0].Id);
            Assert.Equal(30, result.Items[^1].Id);
            Assert.True(result.Metadata.HasPrevious);
            Assert.True(result.Metadata.HasNext);
        }

        [Fact]
        public async Task Should_Paginate_Last_Page_Accurately() {
            // Arrange
            PageRequest request = new(pageNumber: 5, pageSize: 10);

            // Act
            PagedResult<TestItem> result = await this._fixture._context.Items
                .OrderBy(x => x.Id)
                .ToPagedResultAsync(request, TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(10, result.Count);
            Assert.Equal(41, result.Items[0].Id);
            Assert.Equal(50, result.Items[^1].Id);
            Assert.True(result.Metadata.HasPrevious);
            Assert.False(result.Metadata.HasNext);
        }

        [Fact]
        public async Task Should_Return_Empty_Items_When_PageNumber_Exceeds_TotalPages() {
            // Arrange
            PageRequest request = new(pageNumber: 10, pageSize: 10);

            // Act
            PagedResult<TestItem> result = await this._fixture._context.Items
                .OrderBy(x => x.Id)
                .ToPagedResultAsync(request, TestContext.Current.CancellationToken);

            // Assert
            Assert.True(result.IsEmpty);
            Assert.Equal(0, result.Count);
            Assert.Equal(50, result.Metadata.TotalCount);
            Assert.Equal(5, result.Metadata.TotalPages);
            Assert.True(result.Metadata.HasPrevious);
            Assert.False(result.Metadata.HasNext);
        }

        [Fact]
        public async Task Should_Return_Empty_PagedResult_When_Database_Is_Empty() {
            // Arrange: Empty database context
            (TestDbContext? emptyContext, SqliteConnection? connection) = TestDbContext.CreateInMemoryContext();
            PageRequest request = new(pageNumber: 1, pageSize: 20);

            // Act
            PagedResult<TestItem> result = await emptyContext.Items.ToPagedResultAsync(request, TestContext.Current.CancellationToken);

            // Assert
            Assert.True(result.IsEmpty);
            Assert.Equal(0, result.Metadata.TotalCount);
            Assert.Equal(0, result.Metadata.TotalPages);
            Assert.False(result.Metadata.HasNext);
            Assert.False(result.Metadata.HasPrevious);

            await emptyContext.DisposeAsync();
            await connection.DisposeAsync();
        }

        [Fact]
        public async Task Should_Throw_When_Source_Is_Null() {
            // Arrange
            IQueryable<TestItem> nullQuery = null!;
            PageRequest request = PageRequest.Default;

            // Act & Assert
            await Assert.ThrowsAsync<PrecaArgumentNullException>(() =>
                nullQuery.ToPagedResultAsync(request, TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task Should_Respect_CancellationToken() {
            // Arrange
            using CancellationTokenSource cts = new();
            cts.Cancel(); // Cancel immediately

            // Act & Assert
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                this._fixture._context.Items.ToPagedResultAsync(PageRequest.Default, cts.Token));
        }

        [Fact]
        public async Task Should_Calculate_TotalCount_Accurately_When_Where_Filter_Is_Applied() {
            // Arrange: Filter items where price > 300 (22 items: Id 29..50)
            PageRequest request = new(pageNumber: 1, pageSize: 10);

            // Act
            PagedResult<TestItem> result = await this._fixture._context.Items
                .Where(x => x.Price > 300m)
                .OrderBy(x => x.Id)
                .ToPagedResultAsync(request, TestContext.Current.CancellationToken);

            // Assert: TotalCount must reflect the filtered count (22 items)
            Assert.Equal(10, result.Count);
            Assert.Equal(22, result.Metadata.TotalCount);
            Assert.Equal(3, result.Metadata.TotalPages);
            Assert.True(result.Metadata.HasNext);
        }

        [Fact]
        public async Task Should_Paginate_Successfully_With_Select_Projection() {
            // Arrange: DTO projection
            PageRequest request = new(pageNumber: 1, pageSize: 5);

            // Act
            var result = await this._fixture._context.Items
                .AsNoTracking()
                .OrderBy(x => x.Id)
                .Select(x => new { x.Id, UpperName = x.Name.ToUpper() })
                .ToPagedResultAsync(request, TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(5, result.Count);
            Assert.Equal(50, result.Metadata.TotalCount);
            Assert.Equal("ITEM_01", result.Items[0].UpperName);
        }

        [Fact]
        public async Task Should_Support_Raw_Integer_Overload() {
            // Act: Invoke overload directly with raw integer parameters
            PagedResult<TestItem> result = await this._fixture._context.Items
                .OrderBy(x => x.Id)
                .ToPagedResultAsync(pageNumber: 2, pageSize: 15, TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(15, result.Count);
            Assert.Equal(16, result.Items[0].Id);
            Assert.Equal(30, result.Items[^1].Id);
            Assert.Equal(50, result.Metadata.TotalCount);
            Assert.Equal(4, result.Metadata.TotalPages);
        }

        [Fact]
        public async Task Should_Handle_Exact_PageSize_Match() {
            // Arrange: Request page size exactly matching total items count
            PageRequest request = new(pageNumber: 1, pageSize: 50);

            // Act
            PagedResult<TestItem> result = await this._fixture._context.Items
                .OrderBy(x => x.Id)
                .ToPagedResultAsync(request, TestContext.Current.CancellationToken);

            // Assert: Exactly one page with no navigation flags
            Assert.Equal(50, result.Count);
            Assert.Equal(1, result.Metadata.TotalPages);
            Assert.False(result.Metadata.HasPrevious);
            Assert.False(result.Metadata.HasNext);
        }
    }
}