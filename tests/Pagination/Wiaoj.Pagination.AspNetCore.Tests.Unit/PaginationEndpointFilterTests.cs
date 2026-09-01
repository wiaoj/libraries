using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Net.Http.Headers;
using Wiaoj.Pagination.AspNetCore.Filters;
using Wiaoj.Primitives.Collections;

namespace Wiaoj.Pagination.AspNetCore.Tests.Unit;

[Trait("Category", "Unit")]
[Trait("Subsystem", "AspNetCore.Filters")]
public sealed class PaginationEndpointFilterTests {

    public sealed class InvokeAsyncMethod {
        [Fact]
        public async Task Should_Append_Link_And_ETag_Headers_For_PagedResult() {
            // Arrange
            PaginationEndpointFilter filter = PaginationEndpointFilter.Default;
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Path = "/api/items";

            var items = new EquatableArray<string>("Item1", "Item2");
            var metadata = new PageMetadata(totalCount: 20, pageNumber: 1, pageSize: 2);
            var pagedResult = new PagedResult<string>(items, metadata);

            var context = new DefaultEndpointFilterInvocationContext(httpContext);

            // Act
            object? result = await filter.InvokeAsync(context, _ => ValueTask.FromResult<object?>(TypedResults.Ok(pagedResult)));

            // Assert: RFC 6648 header "Pagination"
            Assert.True(httpContext.Response.Headers.ContainsKey(HeaderNames.Link));
            Assert.True(httpContext.Response.Headers.ContainsKey(HeaderNames.ETag)); 
            Assert.IsType<Ok<PagedResult<string>>>(result);
        }

        [Fact]
        public async Task Should_Append_Headers_For_CursorResult() {
            // Arrange
            PaginationEndpointFilter filter = PaginationEndpointFilter.Default;
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Path = "/api/orders";

            var items = new EquatableArray<string>("Order1");
            var startCursor = CursorToken.FromUtf8("start_01");
            var endCursor = CursorToken.FromUtf8("end_01");
            var metadata = new CursorMetadata(startCursor, endCursor, hasPrevious: true, hasNext: true);
            var cursorResult = new CursorResult<string>(items, metadata);

            var context = new DefaultEndpointFilterInvocationContext(httpContext);

            // Act
            object? result = await filter.InvokeAsync(context, _ => ValueTask.FromResult<object?>(TypedResults.Ok(cursorResult)));

            // Assert
            Assert.True(httpContext.Response.Headers.ContainsKey(HeaderNames.Link));
            Assert.True(httpContext.Response.Headers.ContainsKey(HeaderNames.ETag));
            Assert.IsType<Ok<CursorResult<string>>>(result);
        }

        [Fact]
        public async Task Should_Return_304_Not_Modified_When_IfNoneMatch_Matches() {
            // Arrange
            PaginationEndpointFilter filter = PaginationEndpointFilter.Default;
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Path = "/api/items";

            var items = new EquatableArray<string>("Data");
            var metadata = new PageMetadata(totalCount: 1, pageNumber: 1, pageSize: 1);
            var pagedResult = new PagedResult<string>(items, metadata);

            // First call to extract the generated ETag
            var initialContext = new DefaultEndpointFilterInvocationContext(httpContext);
            await filter.InvokeAsync(initialContext, _ => ValueTask.FromResult<object?>(TypedResults.Ok(pagedResult)));
            string generatedETag = httpContext.Response.Headers[HeaderNames.ETag].ToString();

            // Second call simulating conditional request with matching If-None-Match
            var secondHttpContext = new DefaultHttpContext();
            secondHttpContext.Request.Path = "/api/items";
            secondHttpContext.Request.Headers[HeaderNames.IfNoneMatch] = generatedETag;
            var secondContext = new DefaultEndpointFilterInvocationContext(secondHttpContext);

            // Act
            object? result = await filter.InvokeAsync(secondContext, _ => ValueTask.FromResult<object?>(TypedResults.Ok(pagedResult)));

            // Assert: Must return 304 StatusCode result without body payload
            StatusCodeHttpResult statusCodeResult = Assert.IsType<StatusCodeHttpResult>(result);
            Assert.Equal(StatusCodes.Status304NotModified, statusCodeResult.StatusCode);
        }

        [Fact]
        public async Task Should_Pass_Through_Non_Paginated_Results_Unmodified() {
            // Arrange
            PaginationEndpointFilter filter = PaginationEndpointFilter.Default;
            var httpContext = new DefaultHttpContext();
            var context = new DefaultEndpointFilterInvocationContext(httpContext);

            // Act: Endpoint returns a plain string instead of PagedResult
            object? result = await filter.InvokeAsync(context, _ => ValueTask.FromResult<object?>(TypedResults.NotFound()));

            // Assert: Must not crash and should not append pagination headers
            Assert.False(httpContext.Response.Headers.ContainsKey(HeaderNames.Link)); 
            Assert.IsType<NotFound>(result);
        }

        [Fact]
        public async Task Should_Not_Duplicate_PageNumber_Query_Param_In_Link_Header_When_Request_Already_Contains_It() {
            // Arrange: Kullanıcı 2. sayfayı talep ediyor: GET /api/items?pageNumber=2&pageSize=2
            PaginationEndpointFilter filter = PaginationEndpointFilter.Default;
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Path = "/api/items";

            // 🎯 İŞTE ZURNA'NIN ZIRT DEDİĞİ YER: İstek zaten query parametreleriyle geliyor!
            httpContext.Request.QueryString = new QueryString("?pageNumber=2&pageSize=2");

            var items = new EquatableArray<string>("Item3", "Item4");
            // Toplam 20 kayıt var, şu an 2. sayfadayız:
            var metadata = new PageMetadata(totalCount: 20, pageNumber: 2, pageSize: 2);
            var pagedResult = new PagedResult<string>(items, metadata);

            var context = new DefaultEndpointFilterInvocationContext(httpContext);

            // Act
            await filter.InvokeAsync(context, _ => ValueTask.FromResult<object?>(TypedResults.Ok(pagedResult)));

            // Assert
            string linkHeader = httpContext.Response.Headers[HeaderNames.Link].ToString();

            // ❌ MEVCUT KODUN ÜRETTİĞİ (BUG): 
            // </api/items?pageNumber=2&pageSize=2&pageNumber=3>; rel="next"  <-- İki tane pageNumber var!

            // ✅ OLMASI GEREKEN: 
            // </api/items?pageNumber=3&pageSize=2>; rel="next"

            // KANIT ASERSIYONU: Tek bir link URL'i içinde birden fazla "pageNumber" ASLA geçmemelidir!
            Assert.DoesNotContain("pageNumber=2&pageSize=2&pageNumber=", linkHeader);
            Assert.Contains("pageNumber=3", linkHeader);
            Assert.DoesNotContain("pageNumber=2&", linkHeader[(linkHeader.IndexOf("rel=\"next\"") - 60)..]);
        }
    }

    private sealed class DefaultEndpointFilterInvocationContext : EndpointFilterInvocationContext {
        public override HttpContext HttpContext { get; }
        public override IList<object?> Arguments { get; } = [];

        public DefaultEndpointFilterInvocationContext(HttpContext httpContext) {
            this.HttpContext = httpContext;
        }

        public override T GetArgument<T>(int index) {
            return (T)this.Arguments[index]!;
        }
    }
}