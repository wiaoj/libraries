using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Net.Http.Headers;
using Wiaoj.Pagination.AspNetCore.Filters;
using Wiaoj.Primitives.Collections;
using Xunit;

namespace Wiaoj.Pagination.AspNetCore.Tests.Unit;

/// <summary>
/// What the filter does when the handler declares a union return type.
/// </summary>
/// <remarks>
/// <para>
/// <c>Results&lt;Ok&lt;CursorResult&lt;T&gt;&gt;, ProblemHttpResult&gt;</c> is an <c>INestedHttpResult</c>,
/// not an <c>IValueHttpResult</c>; the page sits one level further in. The filter used to unwrap only the
/// latter, so it held the union wrapper — neither a page nor null. It wrote no <c>Link</c> header, and it
/// hashed the wrapper, whose serialisation carries none of the data.
/// </para>
/// <para>
/// Every response on every endpoint written that way then shared one ETag, and a constant ETag matches every
/// <c>If-None-Match</c>: the request after the data changed was answered <c>304</c>, and the client kept its
/// stale copy. It was reported from production as "the new row does not appear".
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Feature", "Pagination")]
[Trait("Component", "EndpointFilter")]
public sealed class NestedResultUnwrappingTests {

    private static CursorResult<string> Page(int rows) {
        return new CursorResult<string>(
            new EquatableArray<string>([.. Enumerable.Range(1, rows).Select(i => $"row{i}")]),
            new CursorMetadata(
                CursorToken.FromUtf8($"start{rows}"),
                CursorToken.FromUtf8($"end{rows}"),
                hasPrevious: false,
                hasNext: true));
    }

    /// <summary>Runs the filter over a result and hands back the response it wrote.</summary>
    private static async Task<(HttpResponse Response, object? Result)> InvokeAsync(
        object result,
        string? ifNoneMatch = null) {

        DefaultHttpContext httpContext = new();
        httpContext.Request.Path = "/api/assets";

        if(ifNoneMatch is not null) {
            httpContext.Request.Headers.IfNoneMatch = ifNoneMatch;
        }

        object? produced = await PaginationEndpointFilter.Default.InvokeAsync(
            new FilterContext(httpContext),
            _ => ValueTask.FromResult<object?>(result));

        return (httpContext.Response, produced);
    }

    private static Results<Ok<CursorResult<string>>, ProblemHttpResult> Nested(int rows) {
        return TypedResults.Ok(Page(rows));
    }

    public sealed class TheUnionReturnType {
        [Fact]
        public async Task Should_Still_Write_The_Link_Header() {
            (HttpResponse response, _) = await InvokeAsync(Nested(3));

            Assert.False(StringValues.IsNullOrEmpty(response.Headers[HeaderNames.Link]));
        }

        [Fact]
        public async Task Should_Produce_The_Same_ETag_As_The_Unwrapped_Result_For_The_Same_Page() {
            (HttpResponse nested, _) = await InvokeAsync(Nested(3));
            (HttpResponse plain, _) = await InvokeAsync(TypedResults.Ok(Page(3)));

            Assert.Equal(plain.Headers.ETag.ToString(), nested.Headers.ETag.ToString());
        }

        [Fact]
        public async Task Should_Produce_A_Different_ETag_For_A_Different_Page() {
            (HttpResponse three, _) = await InvokeAsync(Nested(3));
            (HttpResponse seven, _) = await InvokeAsync(Nested(7));

            Assert.NotEqual(three.Headers.ETag.ToString(), seven.Headers.ETag.ToString());
        }
    }

    public sealed class TheConditionalRequest {
        [Fact]
        public async Task Should_Answer_304_When_The_Page_Really_Is_Unchanged() {
            (HttpResponse first, _) = await InvokeAsync(Nested(3));

            (_, object? result) = await InvokeAsync(Nested(3), ifNoneMatch: first.Headers.ETag.ToString());

            Assert.True(IsNotModified(result));
        }

        [Fact]
        public async Task Should_Not_Answer_304_After_The_Page_Has_Changed() {
            // The reported failure: a row was added, the client sent back the ETag it had, and the server
            // said 304 — so the new row never reached it.
            (HttpResponse before, _) = await InvokeAsync(Nested(3));

            (_, object? result) = await InvokeAsync(Nested(7), ifNoneMatch: before.Headers.ETag.ToString());

            Assert.False(IsNotModified(result));
        }

        private static bool IsNotModified(object? result) {
            return result is IStatusCodeHttpResult { StatusCode: StatusCodes.Status304NotModified };
        }
    }

    /// <summary>
    /// An endpoint carrying the marker but not returning a page gets no ETag at all.
    /// </summary>
    /// <remarks>
    /// Hashing whatever else came back is what made the defect silent: an unreadable result still produced an
    /// ETag, so the header looked present and correct while carrying no information about the body. No header
    /// is the honest answer, and it fails visibly.
    /// </remarks>
    public sealed class TheUnpagedResult {
        [Fact]
        public async Task Should_Not_Be_Given_An_ETag() {
            (HttpResponse response, _) = await InvokeAsync(TypedResults.Ok(new { total = 5 }));

            Assert.True(StringValues.IsNullOrEmpty(response.Headers.ETag));
        }
    }

    private sealed class FilterContext(HttpContext httpContext) : EndpointFilterInvocationContext {
        public override HttpContext HttpContext { get; } = httpContext;
        public override IList<object?> Arguments { get; } = [];

        public override T GetArgument<T>(int index) => throw new NotSupportedException();
    }
}
