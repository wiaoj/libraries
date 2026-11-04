using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;
using System.Text.RegularExpressions;
using Wiaoj.Pagination.AspNetCore.Filters;
using Wiaoj.Primitives.Collections;
using Xunit;

namespace Wiaoj.Pagination.AspNetCore.Tests.Unit;

/// <summary>
/// The <c>Link</c> header is copied verbatim into an HTTP header value, so it must be ASCII: Kestrel rejects
/// anything else while the header is being written. The filter used to paste query values in decoded form,
/// which turned a search term with a non-ASCII character into a 500 on a paginated list.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Feature", "Pagination")]
[Trait("Component", "LinkHeader")]
public sealed partial class LinkHeaderEscapingTests {

    [GeneratedRegex("<(?<url>[^>]+)>;\\s*rel=\"next\"")]
    private static partial Regex NextLink();

    /// <summary>Runs the offset filter over a request with the given query and returns the raw Link header.</summary>
    private static async Task<string> EmitOffsetLinkAsync(string queryString) {
        DefaultHttpContext httpContext = new();
        httpContext.Request.Path = "/api/orders";
        httpContext.Request.QueryString = new QueryString(queryString);

        PagedResult<string> page = new(
            new EquatableArray<string>("Order1"),
            new PageMetadata(totalCount: 100, page: 2, size: 10));

        await PaginationEndpointFilter.Default.InvokeAsync(
            new DefaultEndpointFilterInvocationContext(httpContext),
            _ => ValueTask.FromResult<object?>(TypedResults.Ok(page)));

        return httpContext.Response.Headers[HeaderNames.Link].ToString();
    }

    [Fact]
    public async Task Non_Ascii_Filter_Value_Should_Be_Percent_Encoded_In_The_Link_Header() {
        string linkHeader = await EmitOffsetLinkAsync("?q=T%C3%BCrk%C3%A7e&size=10");

        Assert.Contains("q=T%C3%BCrk%C3%A7e", linkHeader);
        Assert.True(
            linkHeader.All(character => character is >= '\x20' and < '\x7F'),
            $"Link header carries a character Kestrel would reject: {linkHeader}");
    }

    [Fact]
    public async Task Emitted_Next_Link_Should_Bind_Back_To_The_Original_Value() {
        string linkHeader = await EmitOffsetLinkAsync("?q=T%C3%BCrk%C3%A7e&size=10");
        string url = NextLink().Match(linkHeader).Groups["url"].Value;

        IDictionary<string, StringValues> bound = QueryHelpers.ParseQuery(new Uri("http://localhost" + url).Query);

        Assert.Equal("Türkçe", bound["q"].ToString());
        Assert.Equal("10", bound["size"].ToString());
        Assert.Equal("3", bound["page"].ToString());   // the request was page 2; rel="next" points at page 3
    }

    [Fact]
    public async Task Ascii_Only_Request_Should_Emit_The_Link_It_Always_Did() {
        // The escaping exists to stop a crash, not to restyle working URLs: reserved ASCII bytes are still
        // emitted raw, so an existing client sees byte-identical links.
        string linkHeader = await EmitOffsetLinkAsync("?status=active&sort=-code&size=10");

        Assert.Equal(
            "</api/orders?status=active&sort=-code&size=10&page=1>; rel=\"first\", "
            + "</api/orders?status=active&sort=-code&size=10&page=1>; rel=\"prev\", "
            + "</api/orders?status=active&sort=-code&size=10&page=3>; rel=\"next\", "
            + "</api/orders?status=active&sort=-code&size=10&page=10>; rel=\"last\"",
            linkHeader);
    }

    [Theory]
    [InlineData("?q=a%20b", "q=a%20b")]      // space: legal in the URI only escaped
    [InlineData("?q=a%22b", "q=a%22b")]      // double quote: forbidden in a header field value
    [InlineData("?q=a%23b", "q=a%23b")]      // hash: would end the URI at a fragment
    [InlineData("?q=a&b=1", "q=a&b=1")]      // existing shape kept, see the ASCII pin above
    public async Task Only_Characters_The_Transport_Forbids_Should_Be_Escaped(string queryString, string expected) {
        string linkHeader = await EmitOffsetLinkAsync(queryString);

        Assert.Contains(expected, linkHeader);
    }

    [Fact]
    public async Task Non_Ascii_Filter_Value_Should_Survive_The_Cursor_Link_Too() {
        DefaultHttpContext httpContext = new();
        httpContext.Request.Path = "/api/orders";
        httpContext.Request.QueryString = new QueryString("?q=T%C3%BCrk%C3%A7e&limit=10");

        CursorResult<string> page = new(
            new EquatableArray<string>("Order1"),
            new CursorMetadata(
                CursorToken.FromUtf8("start_01"),
                CursorToken.FromUtf8("end_01"),
                hasPrevious: false,
                hasNext: true));

        await PaginationEndpointFilter.Default.InvokeAsync(
            new DefaultEndpointFilterInvocationContext(httpContext),
            _ => ValueTask.FromResult<object?>(TypedResults.Ok(page)));

        string linkHeader = httpContext.Response.Headers[HeaderNames.Link].ToString();

        Assert.Contains("q=T%C3%BCrk%C3%A7e", linkHeader);
        Assert.True(linkHeader.All(character => character is >= '\x20' and < '\x7F'), linkHeader);
    }
}
