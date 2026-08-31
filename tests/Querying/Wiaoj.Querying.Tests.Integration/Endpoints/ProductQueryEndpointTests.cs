using System.Net;
using System.Net.Http.Json;
using Wiaoj.Querying.Tests.Integration.Builders;
using Wiaoj.Querying.Tests.Integration.Fixtures;

namespace Wiaoj.Querying.Tests.Integration.Endpoints;
/// <summary>
/// End-to-end integration test suite verifying querying, filtering, and sorting over HTTP endpoints.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Feature", "EndToEndQuerying")]
public class ProductQueryEndpointTests(TestApplicationFixture fixture) : IClassFixture<TestApplicationFixture> {
    protected readonly HttpClient Client = fixture.Client;

    /// <summary>
    /// Tests for single and multiple field filtering over HTTP GET.
    /// </summary>
    public sealed class FilteringScenarios(TestApplicationFixture fixture) : ProductQueryEndpointTests(fixture) {
        [Fact]
        public async Task Should_Filter_Products_By_Single_Equality_Condition() {
            // Arrange: Request only Electronics category
            string url = QueryBuilder.Create()
                .Where("category", QueryOperator.Equal, "Electronics")
                .BuildUrl("/api/v1/products");

            // Act: Send HTTP GET request
            HttpResponseMessage response = await this.Client.GetAsync(url, TestContext.Current.CancellationToken);

            // Assert: Verify HTTP 200 OK and all items match category
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            List<Product>? items = await response.Content.ReadFromJsonAsync<List<Product>>(TestContext.Current.CancellationToken);

            Assert.NotNull(items);
            Assert.Equal(3, items.Count);
            Assert.All(items, item => Assert.Equal("Electronics", item.Category));
        }

        [Fact]
        public async Task Should_Filter_Products_By_Comparison_Operator() {
            // Arrange: Request products with price >= 300
            string url = QueryBuilder.Create()
                .Where("price", QueryOperator.GreaterThanOrEqual, 300)
                .BuildUrl("/api/v1/products");

            // Act: Send HTTP GET request
            HttpResponseMessage response = await this.Client.GetAsync(url, TestContext.Current.CancellationToken);

            // Assert: Verify only matching items are returned
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            List<Product>? items = await response.Content.ReadFromJsonAsync<List<Product>>(TestContext.Current.CancellationToken);

            Assert.NotNull(items);
            Assert.Equal(3, items.Count); // Gaming Laptop (2500), Standing Desk (600), Office Chair (300)
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

            // Act: Send HTTP GET request
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

    /// <summary>
    /// Tests for free-text search across multiple fields over HTTP GET.
    /// </summary>
    public sealed class SearchScenarios(TestApplicationFixture fixture) : ProductQueryEndpointTests(fixture) {
        [Fact]
        public async Task Should_Search_Products_By_Free_Text_Term() {
            // Arrange: Free-text search for "Desk"
            string url = QueryBuilder.Create()
                .Search("Desk")
                .BuildUrl("/api/v1/products");

            // Act
            HttpResponseMessage response = await this.Client.GetAsync(url, TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            List<Product>? items = await response.Content.ReadFromJsonAsync<List<Product>>(TestContext.Current.CancellationToken);

            Assert.NotNull(items);
            Product item = Assert.Single(items);
            Assert.Equal("Standing Desk", item.Name);
        }
    }
}