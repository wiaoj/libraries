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

        // ---------------------------------------------------------------
        // Additional edge-case coverage
        // ---------------------------------------------------------------

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(-100)]
        public async Task Should_Clamp_PageNumber_To_One_When_Zero_Or_Negative(int invalidPageNumber) {
            // Arrange: PageRequest's constructor clamps pageNumber < 1 up to 1 (no exception).
            PageRequest request = new(pageNumber: invalidPageNumber, pageSize: 10);
            Assert.Equal(1, request.PageNumber);

            // Act
            PagedResult<TestItem> result = await this._fixture._context.Items
                .OrderBy(x => x.Id)
                .ToPagedResultAsync(request, TestContext.Current.CancellationToken);

            // Assert: Behaves identically to an explicit first-page request
            Assert.Equal(1, result.Metadata.PageNumber);
            Assert.False(result.Metadata.HasPrevious);
            Assert.Equal(1, result.Items[0].Id);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public async Task Should_Clamp_PageSize_To_Default_When_Zero_Or_Negative(int invalidPageSize) {
            // Arrange: PageRequest's constructor clamps pageSize < 1 up to DefaultPageSize (20).
            PageRequest request = new(pageNumber: 1, pageSize: invalidPageSize);
            Assert.Equal(PageRequest.DefaultPageSize, request.PageSize);

            // Act
            PagedResult<TestItem> result = await this._fixture._context.Items
                .OrderBy(x => x.Id)
                .ToPagedResultAsync(request, TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(PageRequest.DefaultPageSize, result.Count);
            Assert.Equal(50, result.Metadata.TotalCount);
        }

        [Fact]
        public async Task Should_Clamp_PageSize_To_MaxPageSize_When_Exceeding_Limit() {
            // Arrange: PageRequest's constructor clamps pageSize > MaxPageSize (100) down to 100.
            PageRequest request = new(pageNumber: 1, pageSize: 1_000_000);
            Assert.Equal(PageRequest.MaxPageSize, request.PageSize);

            // Act
            PagedResult<TestItem> result = await this._fixture._context.Items
                .OrderBy(x => x.Id)
                .ToPagedResultAsync(request, TestContext.Current.CancellationToken);

            // Assert: Only 50 records exist, all fit within the clamped page size of 100
            Assert.Equal(50, result.Count);
            Assert.Equal(1, result.Metadata.TotalPages);
            Assert.False(result.Metadata.HasNext);
        }

        [Fact]
        public async Task Should_Return_Empty_Page_When_Large_PageNumber_Exceeds_TotalPages() {
            // Arrange: A large pageNumber combined with the clamped max pageSize (100) is still
            // far beyond the 50 seeded records, so the offset (skip) calculation must not throw
            // or misbehave, and the result must simply be an empty page.
            PageRequest request = new(pageNumber: 100_000, pageSize: 100_000);
            Assert.Equal(PageRequest.MaxPageSize, request.PageSize);

            // Act
            PagedResult<TestItem> result = await this._fixture._context.Items
                .OrderBy(x => x.Id)
                .ToPagedResultAsync(request, TestContext.Current.CancellationToken);

            // Assert
            Assert.True(result.IsEmpty);
            Assert.Equal(50, result.Metadata.TotalCount);
        }

        [Fact]
        public async Task Should_Calculate_TotalCount_Correctly_When_Where_And_Select_Are_Combined() {
            // Arrange: Filter + projection combined (Price > 300 => 22 items)
            PageRequest request = new(pageNumber: 1, pageSize: 5);

            // Act
            var result = await this._fixture._context.Items
                .Where(x => x.Price > 300m)
                .OrderBy(x => x.Id)
                .Select(x => new { x.Id, x.Name })
                .ToPagedResultAsync(request, TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(5, result.Count);
            Assert.Equal(22, result.Metadata.TotalCount);
            Assert.Equal(5, result.Metadata.TotalPages);
        }

        [Fact]
        public async Task Should_Paginate_Consistently_Using_Default_PageRequest() {
            // Act
            PagedResult<TestItem> result = await this._fixture._context.Items
                .OrderBy(x => x.Id)
                .ToPagedResultAsync(PageRequest.Default, TestContext.Current.CancellationToken);

            // Assert: Default request must resolve to the first page
            Assert.Equal(1, result.Metadata.PageNumber);
            Assert.False(result.Metadata.HasPrevious);
            Assert.Equal(1, result.Items[0].Id);
        }

        [Fact]
        public async Task Should_Not_Throw_When_Query_Has_No_Explicit_OrderBy() {
            // Arrange: Deliberately omit OrderBy to verify the method does not hard-require it.
            // Item ordering is not guaranteed by the provider in this case, but the call itself
            // must not throw and Count/TotalCount must still be correct.
            PageRequest request = new(pageNumber: 1, pageSize: 10);

            // Act
            PagedResult<TestItem> result = await this._fixture._context.Items
                .ToPagedResultAsync(request, TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(10, result.Count);
            Assert.Equal(50, result.Metadata.TotalCount);
        }
    }
}