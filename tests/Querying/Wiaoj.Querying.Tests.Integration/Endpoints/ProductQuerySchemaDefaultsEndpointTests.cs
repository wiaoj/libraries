using System.Net;
using System.Net.Http.Json;
using Wiaoj.Querying.Tests.Integration.Builders;
using Wiaoj.Querying.Tests.Integration.Fixtures;

namespace Wiaoj.Querying.Tests.Integration.Endpoints;

/// <summary>
/// End-to-end integration test suite verifying <see cref="QuerySchema{T}.RequireFilter"/>,
/// <see cref="QuerySchema{T}.DefaultFilter{TProperty}"/>, <see cref="QuerySchema{T}.DefaultSort{TProperty}"/>,
/// and <see cref="QuerySchema{T}.UseCaseInsensitiveText"/> against real HTTP requests, using
/// <see cref="SchemaDefaultsApplicationFixture"/>'s independently configured schema.
/// </summary>
/// <remarks>
/// Seed reference (from <see cref="TestDbContext.SeedData"/>) — items 3 and 5 are soft-deleted:
/// <list type="table">
/// <item><description>1 Gaming Laptop X, 2500, Electronics, Active, 2026-01-01, not deleted</description></item>
/// <item><description>2 Mechanical Keyboard, 150, Electronics, Active, 2026-01-05, not deleted</description></item>
/// <item><description>3 Wireless Mouse, 80, Electronics, Inactive, 2026-01-10, DELETED</description></item>
/// <item><description>4 Office Chair, 300, Furniture, Active, 2026-02-01, not deleted</description></item>
/// <item><description>5 Standing Desk, 600, Furniture, Active, 2026-02-15, DELETED</description></item>
/// <item><description>6 Ergonomic Stool, 120, Furniture, Pending, 2026-03-01, not deleted</description></item>
/// <item><description>7 4K Gaming Monitor, 450, Electronics, Pending, 2026-03-10, not deleted</description></item>
/// </list>
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Feature", "SchemaLevelDefaults")]
public class ProductQuerySchemaDefaultsEndpointTests(SchemaDefaultsApplicationFixture fixture)
    : IClassFixture<SchemaDefaultsApplicationFixture> {
    protected readonly HttpClient Client = fixture.Client;

    public sealed class RequireFilterEnforcement(SchemaDefaultsApplicationFixture fixture)
        : ProductQuerySchemaDefaultsEndpointTests(fixture) {
        [Fact]
        public async Task Should_Never_Return_Soft_Deleted_Products_Even_With_Empty_Request() {
            // Act
            HttpResponseMessage response = await this.Client.GetAsync("/api/v1/products", TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            List<Product>? items = await response.Content.ReadFromJsonAsync<List<Product>>(TestContext.Current.CancellationToken);

            Assert.NotNull(items);
            Assert.All(items, x => Assert.Null(x.DeletedAt));
        }

        [Fact]
        public async Task Should_Not_Be_Bypassable_By_Explicitly_Requesting_A_Soft_Deleted_Product() {
            // Arrange: item 3 is soft-deleted; explicitly requesting it by Id must still be excluded
            string url = QueryBuilder.Create()
                .Where("id", QueryOperator.Equal, 3)
                .BuildUrl("/api/v1/products");

            // Act
            HttpResponseMessage response = await this.Client.GetAsync(url, TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            List<Product>? items = await response.Content.ReadFromJsonAsync<List<Product>>(TestContext.Current.CancellationToken);

            Assert.NotNull(items);
            Assert.Empty(items);
        }
    }

    public sealed class DefaultFilterFallback(SchemaDefaultsApplicationFixture fixture)
        : ProductQuerySchemaDefaultsEndpointTests(fixture) {
        [Fact]
        public async Task Should_Apply_Default_Active_Status_Filter_When_Request_Is_Empty() {
            // Act
            HttpResponseMessage response = await this.Client.GetAsync("/api/v1/products", TestContext.Current.CancellationToken);

            // Assert: items 1, 2, 4 — Active, not deleted (3 and 5 excluded by RequireFilter; 6, 7 are Pending)
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            List<Product>? items = await response.Content.ReadFromJsonAsync<List<Product>>(TestContext.Current.CancellationToken);

            Assert.NotNull(items);
            Assert.Equal(3, items.Count);
            Assert.All(items, x => Assert.Equal("Active", x.Status));
        }

        [Fact]
        public async Task Should_Skip_Default_Status_Filter_When_Caller_Explicitly_Filters_Status() {
            // Arrange: explicitly request Pending — the "Active" default must be skipped, not combined
            string url = QueryBuilder.Create()
                .Where("status", QueryOperator.Equal, "Pending")
                .BuildUrl("/api/v1/products");

            // Act
            HttpResponseMessage response = await this.Client.GetAsync(url, TestContext.Current.CancellationToken);

            // Assert: items 6 and 7 — Pending, not deleted
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            List<Product>? items = await response.Content.ReadFromJsonAsync<List<Product>>(TestContext.Current.CancellationToken);

            Assert.NotNull(items);
            Assert.Equal(2, items.Count);
            Assert.All(items, x => Assert.Equal("Pending", x.Status));
        }
    }

    public sealed class DefaultSortFallback(SchemaDefaultsApplicationFixture fixture)
        : ProductQuerySchemaDefaultsEndpointTests(fixture) {
        [Fact]
        public async Task Should_Apply_Default_CreatedAt_Descending_Sort_When_Request_Has_No_Sort() {
            // Act: empty request -> Active + not-deleted = items 1, 2, 4; default sort by CreatedAt descending
            HttpResponseMessage response = await this.Client.GetAsync("/api/v1/products", TestContext.Current.CancellationToken);

            // Assert: 4 (Feb 1) newest, then 2 (Jan 5), then 1 (Jan 1) oldest
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            List<Product>? items = await response.Content.ReadFromJsonAsync<List<Product>>(TestContext.Current.CancellationToken);

            Assert.NotNull(items);
            Assert.Equal(3, items.Count);
            Assert.Equal(4, items[0].Id);
            Assert.Equal(1, items[^1].Id);
        }

        [Fact]
        public async Task Should_Skip_Default_Sort_When_Caller_Supplies_Any_Sort() {
            // Arrange: caller sorts by Id ascending instead
            string url = "/api/v1/products?sort=id";

            // Act
            HttpResponseMessage response = await this.Client.GetAsync(url, TestContext.Current.CancellationToken);

            // Assert: still Active + not-deleted (1, 2, 4), but now ascending by Id rather than CreatedAt descending
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            List<Product>? items = await response.Content.ReadFromJsonAsync<List<Product>>(TestContext.Current.CancellationToken);

            Assert.NotNull(items);
            Assert.Equal(3, items.Count);
            Assert.Equal(1, items[0].Id);
            Assert.Equal(4, items[^1].Id);
        }
    }

    public sealed class CaseInsensitiveTextOptOut(SchemaDefaultsApplicationFixture fixture)
        : ProductQuerySchemaDefaultsEndpointTests(fixture) {
        [Fact]
        public async Task Should_Not_Match_Wrong_Case_Value_When_Case_Insensitive_Text_Is_Disabled() {
            // Arrange: actual Category value is "Electronics" (capital E); this endpoint disables
            // case-insensitive text comparisons, so a lowercase "electronics" must not match
            string url = QueryBuilder.Create()
                .Where("category", QueryOperator.Equal, "electronics")
                .BuildUrl("/api/v1/products");

            // Act
            HttpResponseMessage response = await this.Client.GetAsync(url, TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            List<Product>? items = await response.Content.ReadFromJsonAsync<List<Product>>(TestContext.Current.CancellationToken);

            Assert.NotNull(items);
            Assert.Empty(items);
        }

        [Fact]
        public async Task Should_Match_Exact_Case_Value_When_Case_Insensitive_Text_Is_Disabled() {
            // Arrange: exact-case "Electronics" should still match normally
            string url = QueryBuilder.Create()
                .Where("category", QueryOperator.Equal, "Electronics")
                .BuildUrl("/api/v1/products");

            // Act
            HttpResponseMessage response = await this.Client.GetAsync(url, TestContext.Current.CancellationToken);

            // Assert: items 1, 2 — Electronics, Active, not deleted (7 is Electronics but Pending, filtered by the Status default)
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            List<Product>? items = await response.Content.ReadFromJsonAsync<List<Product>>(TestContext.Current.CancellationToken);

            Assert.NotNull(items);
            Assert.Equal(2, items.Count);
            Assert.All(items, x => Assert.Equal("Electronics", x.Category));
        }
    }
}