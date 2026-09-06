using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Net.Http.Headers;
using Wiaoj.Pagination.AspNetCore.Filters;
using System.Text.RegularExpressions;
using Wiaoj.Primitives.Collections;
using Xunit;

namespace Wiaoj.Pagination.AspNetCore.Tests.Unit;

/// <summary>
/// The filter emits a <c>Link</c> header for a client to follow. Nothing tested that the endpoint can
/// actually bind the URL it just handed out — link generation and request binding were each covered on their
/// own, but not the contract between them.
/// </summary>
/// <remarks>
/// That gap hid a real trap. <see cref="CursorRequest"/> implements <see cref="ISpanParsable{T}"/>, so a
/// handler taking a bare <c>CursorRequest</c> binds from a single composite value — and rejects the
/// <c>?cursor=…&amp;direction=…</c> form the Link header actually contains. Following <c>rel="next"</c>
/// returns 400, after the first page worked.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Feature", "Pagination")]
[Trait("Component", "LinkHeader")]
public sealed partial class LinkHeaderRoundTripTests {

    [GeneratedRegex("<(?<url>[^>]+)>;\\s*rel=\"next\"")]
    private static partial Regex NextLink();

    /// <summary>Runs the filter over a cursor page and returns the URL its <c>rel="next"</c> points at.</summary>
    private static async Task<string> EmitNextLinkAsync() {
        DefaultHttpContext httpContext = new();
        httpContext.Request.Path = "/api/orders";

        CursorMetadata metadata = new(
            CursorToken.FromUtf8("start_01"),
            CursorToken.FromUtf8("end_01"),
            hasPrevious: false,
            hasNext: true);

        CursorResult<string> page = new(new EquatableArray<string>("Order1"), metadata);

        await PaginationEndpointFilter.Default.InvokeAsync(
            new DefaultEndpointFilterInvocationContext(httpContext),
            _ => ValueTask.FromResult<object?>(TypedResults.Ok(page)));

        string linkHeader = httpContext.Response.Headers[HeaderNames.Link].ToString();
        Match match = NextLink().Match(linkHeader);

        Assert.True(match.Success, $"No rel=\"next\" in Link header: {linkHeader}");
        return match.Groups["url"].Value;
    }

    /// <summary>Sends a URL at a handler bound the given way and returns the status code it produced.</summary>
    private static async Task<int> SendAsync(Delegate handler, string url) {
        RequestDelegate requestDelegate = RequestDelegateFactory.Create(handler).RequestDelegate;

        DefaultHttpContext context = new() {
            RequestServices = new ServiceCollection().AddLogging().BuildServiceProvider()
        };

        int queryStart = url.IndexOf('?', StringComparison.Ordinal);
        context.Request.QueryString = queryStart < 0 ? QueryString.Empty : new QueryString(url[queryStart..]);
        context.Features.Set<IHttpResponseBodyFeature>(new StreamResponseBodyFeature(Stream.Null));

        await requestDelegate(context);
        return context.Response.StatusCode;
    }

    [Fact]
    public async Task An_AsParameters_Endpoint_Should_Accept_The_Link_It_Emitted() {
        string next = await EmitNextLinkAsync();

        int status = await SendAsync(([AsParameters] CursorRequest request) => Results.Ok(), next);

        Assert.Equal(StatusCodes.Status200OK, status);
    }

    [Fact]
    public async Task An_AsParameters_Endpoint_Should_Bind_The_Cursor_And_Direction_From_The_Link() {
        string next = await EmitNextLinkAsync();
        CursorRequest bound = default;

        await SendAsync(([AsParameters] CursorRequest request) => {
            bound = request;
            return Results.Ok();
        }, next);

        Assert.False(bound.Cursor.IsEmpty);
        Assert.Equal(CursorDirection.Forward, bound.Direction);
    }

    [Fact]
    public async Task A_Bare_CursorRequest_Endpoint_Should_Reject_The_Link_It_Emitted() {
        // Pinning the trap rather than the desired behaviour: without [AsParameters] the type binds from a
        // single composite value, so the very link the endpoint handed out comes back 400. This is why the
        // attribute is not optional, and why the README says so.
        string next = await EmitNextLinkAsync();

        int status = await SendAsync((CursorRequest request) => Results.Ok(), next);

        Assert.Equal(StatusCodes.Status400BadRequest, status);
    }
}

file sealed class DefaultEndpointFilterInvocationContext(HttpContext httpContext) : EndpointFilterInvocationContext {
    public override HttpContext HttpContext { get; } = httpContext;
    public override IList<object?> Arguments { get; } = [];
    public override T GetArgument<T>(int index) => (T)this.Arguments[index]!;
}
