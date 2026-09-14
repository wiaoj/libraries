using System.Collections.Concurrent;
using System.Net;
using System.Text;

namespace Wiaoj.WellKnown.Discovery.Tests.Unit;

/// <summary>
/// An HttpMessageHandler answering by absolute URL, counting requests per URL. Unknown URLs answer 404.
/// </summary>
internal sealed class FakeMetadataServer : HttpMessageHandler {
    private readonly ConcurrentDictionary<string, Func<HttpRequestMessage, Task<HttpResponseMessage>>> _routes = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, int> _hits = new(StringComparer.Ordinal);

    public int Hits(string url) => this._hits.TryGetValue(url, out int hits) ? hits : 0;

    public int TotalHits => this._hits.Values.Sum();

    public FakeMetadataServer Json(string url, string json, string? cacheControl = "max-age=300", TimeSpan? age = null) {
        return this.Route(url, _ => {
            HttpResponseMessage response = new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
            if(cacheControl is not null) {
                response.Headers.TryAddWithoutValidation("Cache-Control", cacheControl);
            }

            response.Headers.Age = age;
            return response;
        });
    }

    public FakeMetadataServer Route(string url, Func<HttpRequestMessage, HttpResponseMessage> respond) {
        return this.RouteAsync(url, request => Task.FromResult(respond(request)));
    }

    public FakeMetadataServer RouteAsync(string url, Func<HttpRequestMessage, Task<HttpResponseMessage>> respond) {
        this._routes[url] = respond;
        return this;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
        string url = request.RequestUri!.AbsoluteUri;
        this._hits.AddOrUpdate(url, 1, (_, hits) => hits + 1);

        HttpResponseMessage response = this._routes.TryGetValue(url, out Func<HttpRequestMessage, Task<HttpResponseMessage>>? respond)
            ? await respond(request)
            : new HttpResponseMessage(HttpStatusCode.NotFound);

        response.RequestMessage ??= request;
        return response;
    }

    public static string Resource(string resource, params string[] authorizationServers) {
        string servers = string.Join(",", authorizationServers.Select(s => $"\"{s}\""));
        return $$"""{"resource":"{{resource}}","authorization_servers":[{{servers}}]}""";
    }

    public static string Issuer(string issuer) {
        return $$"""{"issuer":"{{issuer}}","token_endpoint":"{{issuer.TrimEnd('/')}}/token","response_types_supported":["code"],"device_authorization_endpoint":"{{issuer.TrimEnd('/')}}/device"}""";
    }

    public static HttpResponseMessage Challenge(string requestUrl, params string[] wwwAuthenticate) {
        HttpResponseMessage response = new(HttpStatusCode.Unauthorized) {
            RequestMessage = new HttpRequestMessage(HttpMethod.Get, requestUrl)
        };

        foreach(string value in wwwAuthenticate) {
            response.Headers.TryAddWithoutValidation("WWW-Authenticate", value);
        }

        return response;
    }
}
