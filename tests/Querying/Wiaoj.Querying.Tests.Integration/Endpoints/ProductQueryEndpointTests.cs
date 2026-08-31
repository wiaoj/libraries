using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Net.Http.Json;
using Wiaoj.Querying.Tests.Integration.Builders;
using Wiaoj.Querying.Tests.Integration.Fixtures;

namespace Wiaoj.Querying.Tests.Integration.Endpoints;

/// <summary>
/// End-to-end integration test suite verifying querying, filtering, sorting, searching,
/// security limits, and problem details generation over HTTP endpoints.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Feature", "EndToEndQuerying")]
public class ProductQueryEndpointTests(TestApplicationFixture fixture) : IClassFixture<TestApplicationFixture> {
    protected readonly HttpClient Client = fixture.Client;

    public sealed class EqualityAndComparisonFiltering(TestApplicationFixture fixture) : ProductQueryEndpointTests(fixture) {
        [Fact]
        public async Task Should_Filter_Products_By_Single_Equality_Condition() {
            // Arrange
            string url = QueryBuilder.Create()
                .Where("category", QueryOperator.Equal, "Electronics")
                .BuildUrl("/api/v1/products");

            // Act
            HttpResponseMessage response = await this.Client.GetAsync(url, TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            List<Product>? items = await response.Content.ReadFromJsonAsync<List<Product>>(TestContext.Current.CancellationToken);

            Assert.NotNull(items);
            Assert.Equal(4, items.Count); // Laptop (2500), Keyboard (150), Mouse (80), Monitor (450)
            Assert.All(items, item => Assert.Equal("Electronics", item.Category));
        }

        [Fact]
        public async Task Should_Filter_Products_By_Comparison_Operator() {
            // Arrange: price >= 300
            string url = QueryBuilder.Create()
                .Where("price", QueryOperator.GreaterThanOrEqual, 300)
                .BuildUrl("/api/v1/products");

            // Act
            HttpResponseMessage response = await this.Client.GetAsync(url, TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            List<Product>? items = await response.Content.ReadFromJsonAsync<List<Product>>(TestContext.Current.CancellationToken);

            Assert.NotNull(items);
            Assert.Equal(4, items.Count); // Laptop (2500), Monitor (450), Desk (600), Chair (300)
            Assert.All(items, item => Assert.True(item.Price >= 300));
        }

        [Fact]
        public async Task Should_Filter_Products_By_Multiple_Combined_Conditions() {
            // Arrange: Electronics + Active + price >= 150
            string url = QueryBuilder.Create()
                .Where("category", QueryOperator.Equal, "Electronics")
                .Where("status", QueryOperator.Equal, "Active")
                .Where("price", QueryOperator.GreaterThanOrEqual, 150)
                .BuildUrl("/api/v1/products");

            // Act
            HttpResponseMessage response = await this.Client.GetAsync(url, TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            List<Product>? items = await response.Content.ReadFromJsonAsync<List<Product>>(TestContext.Current.CancellationToken);

            Assert.NotNull(items);
            Assert.Equal(2, items.Count); // Gaming Laptop X (2500) and Mechanical Keyboard (150)
            Assert.All(items, item => {
                Assert.Equal("Electronics", item.Category);
                Assert.Equal("Active", item.Status);
                Assert.True(item.Price >= 150);
            });
        }
    }

    public sealed class CollectionAndRangeFiltering(TestApplicationFixture fixture) : ProductQueryEndpointTests(fixture) {
        [Fact]
        public async Task Should_Filter_Products_By_In_Collection_Operator() {
            // Arrange: status IN (Inactive, Pending)
            string url = "/api/v1/products?status[in]=Inactive,Pending";

            // Act
            HttpResponseMessage response = await this.Client.GetAsync(url, TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            List<Product>? items = await response.Content.ReadFromJsonAsync<List<Product>>(TestContext.Current.CancellationToken);

            Assert.NotNull(items);
            Assert.Equal(3, items.Count); // Mouse (Inactive), Stool (Pending), Monitor (Pending)
            Assert.All(items, item => Assert.Contains(item.Status, new[] { "Inactive", "Pending" }));
        }

        [Fact]
        public async Task Should_Filter_Products_By_Between_Range_Operator() {
            // Arrange: price BETWEEN 120 and 450
            string url = "/api/v1/products?price[between]=120..450";

            // Act
            HttpResponseMessage response = await this.Client.GetAsync(url, TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            List<Product>? items = await response.Content.ReadFromJsonAsync<List<Product>>(TestContext.Current.CancellationToken);

            Assert.NotNull(items);
            Assert.Equal(4, items.Count); // Keyboard (150), Chair (300), Stool (120), Monitor (450)
            Assert.All(items, item => Assert.True(item.Price is >= 120m and <= 450m));
        }

        [Fact]
        public async Task Should_Filter_Products_By_DateTime_Comparison() {
            // Arrange: createdAt >= 2026-02-01
            string url = "/api/v1/products?createdAt[gte]=2026-02-01T00:00:00Z";

            // Act
            HttpResponseMessage response = await this.Client.GetAsync(url, TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            List<Product>? items = await response.Content.ReadFromJsonAsync<List<Product>>(TestContext.Current.CancellationToken);

            Assert.NotNull(items);
            Assert.Equal(4, items.Count); // Chair (Feb 1), Desk (Feb 15), Stool (Mar 1), Monitor (Mar 10)
            Assert.All(items, item => Assert.True(item.CreatedAt >= new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc)));
        }
    }

    public sealed class NullAndPresenceFiltering(TestApplicationFixture fixture) : ProductQueryEndpointTests(fixture) {
        [Fact]
        public async Task Should_Filter_Non_Deleted_Products_With_IsNull() {
            // Arrange: deletedAt[isNull]
            string url = "/api/v1/products?deletedAt[isNull]";

            // Act
            HttpResponseMessage response = await this.Client.GetAsync(url, TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            List<Product>? items = await response.Content.ReadFromJsonAsync<List<Product>>(TestContext.Current.CancellationToken);

            Assert.NotNull(items);
            Assert.Equal(5, items.Count); // 2 products are deleted, 5 are active
            Assert.All(items, item => Assert.Null(item.DeletedAt));
        }

        [Fact]
        public async Task Should_Filter_Deleted_Products_With_IsNotNull() {
            // Arrange: deletedAt[isNotNull]
            string url = "/api/v1/products?deletedAt[isNotNull]";

            // Act
            HttpResponseMessage response = await this.Client.GetAsync(url, TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            List<Product>? items = await response.Content.ReadFromJsonAsync<List<Product>>(TestContext.Current.CancellationToken);

            Assert.NotNull(items);
            Assert.Equal(2, items.Count); // Mouse and Desk
            Assert.All(items, item => Assert.NotNull(item.DeletedAt));
        }
    }

    public sealed class FreeTextSearch(TestApplicationFixture fixture) : ProductQueryEndpointTests(fixture) {
        [Theory]
        [InlineData("Desk", 1, "Standing Desk")]
        [InlineData("Gaming", 2, null)] // Laptop & Monitor
        [InlineData("furniture", 3, null)] // Search across category
        public async Task Should_Search_Products_Across_Multiple_Columns(
            string searchTerm,
            int expectedCount,
            string? expectedSingleName) {
            // Arrange
            string url = QueryBuilder.Create()
                .Search(searchTerm)
                .BuildUrl("/api/v1/products");

            // Act
            HttpResponseMessage response = await this.Client.GetAsync(url, TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            List<Product>? items = await response.Content.ReadFromJsonAsync<List<Product>>(TestContext.Current.CancellationToken);

            Assert.NotNull(items);
            Assert.Equal(expectedCount, items.Count);

            if(expectedSingleName != null) {
                Assert.Equal(expectedSingleName, items[0].Name);
            }
        }
    }

    public sealed class SortingAndOrdering(TestApplicationFixture fixture) : ProductQueryEndpointTests(fixture) {
        [Fact]
        public async Task Should_Sort_Products_Ascending_And_Descending() {
            // Arrange: Sort by price descending
            string url = "/api/v1/products?sort=-price";

            // Act
            HttpResponseMessage response = await this.Client.GetAsync(url, TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            List<Product>? items = await response.Content.ReadFromJsonAsync<List<Product>>(TestContext.Current.CancellationToken);

            Assert.NotNull(items);
            Assert.Equal(2500m, items.First().Price);
            Assert.Equal(80m, items.Last().Price);
        }

        [Fact]
        public async Task Should_Support_Multi_Field_Sorting() {
            // Arrange: Sort by createdAt ascending, then price descending
            string url = "/api/v1/products?sort=createdAt,-price";

            // Act
            HttpResponseMessage response = await this.Client.GetAsync(url, TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            List<Product>? items = await response.Content.ReadFromJsonAsync<List<Product>>(TestContext.Current.CancellationToken);

            Assert.NotNull(items);
            Assert.Equal(1, items[0].Id); // Oldest created item
        }
    }

    public sealed class CombinedScenarios(TestApplicationFixture fixture) : ProductQueryEndpointTests(fixture) {
        [Fact]
        public async Task Should_Execute_Search_Filter_And_Sort_Simultaneously() {
            // Arrange: Search "Gaming", Category=Electronics, Price >= 200, Sort=-price, deletedAt[isNull]
            string url = "/api/v1/products?q=Gaming&category=Electronics&price[gte]=200&deletedAt[isNull]&sort=-price";

            // Act
            HttpResponseMessage response = await this.Client.GetAsync(url, TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            List<Product>? items = await response.Content.ReadFromJsonAsync<List<Product>>(TestContext.Current.CancellationToken);

            Assert.NotNull(items);
            Assert.Equal(2, items.Count); // Gaming Laptop X (2500) and 4K Gaming Monitor (450)
            Assert.Equal(2500m, items[0].Price);
            Assert.Equal(450m, items[1].Price);
        }
    }

    public sealed class ValidationAndProblemDetails(TestApplicationFixture fixture) : ProductQueryEndpointTests(fixture) {
        [Fact]
        public async Task Should_Return_Http_400_ValidationProblem_When_Field_Is_Not_Filterable() {
            // Arrange: "InternalSecret" is not in schema
            string url = "/api/v1/products?InternalSecret[eq]=123";

            // Act
            HttpResponseMessage response = await this.Client.GetAsync(url, TestContext.Current.CancellationToken);

            // Assert: Must return HTTP 400 Bad Request
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            ValidationProblemDetails? problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(TestContext.Current.CancellationToken);

            Assert.NotNull(problem);
            Assert.True(problem.Errors.ContainsKey("InternalSecret"));
        }

        [Fact]
        public async Task Should_Return_Http_400_When_Sort_Field_Is_Not_Allowed() {
            // Arrange: "Category" is not registered for sorting in schema
            string url = "/api/v1/products?sort=Category";

            // Act
            HttpResponseMessage response = await this.Client.GetAsync(url, TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            ValidationProblemDetails? problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(TestContext.Current.CancellationToken);

            Assert.NotNull(problem);
            Assert.True(problem.Errors.ContainsKey("Category"));
        }

        [Fact]
        public async Task Should_Return_Http_400_When_Filter_Limit_Is_Exceeded() {
            // Arrange: MaxFilters limit is 5; sending 6 filters
            string url = "/api/v1/products?id=1&price=100&category=Electronics&status=Active&name=Laptop&createdAt=2026-01-01";

            // Act
            HttpResponseMessage response = await this.Client.GetAsync(url, TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            ValidationProblemDetails? problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(TestContext.Current.CancellationToken);

            Assert.NotNull(problem);
            Assert.True(problem.Errors.ContainsKey("$")); // Global limit error key
        }

        [Fact]
        public async Task Should_Return_Http_400_When_Numeric_Value_Is_Malformed() {
            // Arrange: Price value is not a valid number
            string url = "/api/v1/products?price[gte]=not_a_valid_number";

            // Act
            HttpResponseMessage response = await this.Client.GetAsync(url, TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            ValidationProblemDetails? problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(TestContext.Current.CancellationToken);

            Assert.NotNull(problem);
            Assert.True(problem.Errors.ContainsKey("price"));
        }
    }
}