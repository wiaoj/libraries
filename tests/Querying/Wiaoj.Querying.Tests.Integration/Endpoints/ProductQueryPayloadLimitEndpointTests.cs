using System.Net;
using System.Text;
using Wiaoj.Querying.Tests.Integration.Fixtures;

namespace Wiaoj.Querying.Tests.Integration.Endpoints;

/// <summary>
/// End-to-end integration tests proving <see cref="QueryOptions.MaxPayloadBytes"/> actually takes
/// effect over real HTTP requests, using <see cref="SmallPayloadLimitApplicationFixture"/>'s
/// deliberately tiny 50-byte override.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Feature", "PayloadSizeLimits")]
public class ProductQueryPayloadLimitEndpointTests(SmallPayloadLimitApplicationFixture fixture)
    : IClassFixture<SmallPayloadLimitApplicationFixture> {
    protected readonly HttpClient Client = fixture.Client;

    [Fact]
    public async Task Should_Accept_A_Payload_Under_The_Configured_MaxPayloadBytes() {
        // Arrange: {"q":"a"} is 9 bytes — comfortably under the 50-byte override, proving the
        // override doesn't just reject everything outright.
        const string tinyPayload = """{"q":"a"}""";
        using HttpRequestMessage request = new(new HttpMethod("QUERY"), "/api/v1/products") {
            Content = new StringContent(tinyPayload, Encoding.UTF8, "application/json")
        };

        // Act
        HttpResponseMessage response = await this.Client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Should_Return_413_When_Payload_Exceeds_The_Configured_MaxPayloadBytes() {
        // Arrange: a perfectly ordinary filter payload — well within the parser's own 64 KB default
        // and far below any host-level limit — but well over this fixture's 50-byte override.
        const string ordinaryPayload = """{"filters":[{"field":"category","op":"eq","value":"Electronics"}]}""";
        using HttpRequestMessage request = new(new HttpMethod("QUERY"), "/api/v1/products") {
            Content = new StringContent(ordinaryPayload, Encoding.UTF8, "application/json")
        };

        // Act
        HttpResponseMessage response = await this.Client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert: proves QueryOptions.MaxPayloadBytes is the binding constraint here — not the
        // parser's own 64 KB default and not any host-level limit, since this payload is nowhere
        // near either of those.
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
    }
}