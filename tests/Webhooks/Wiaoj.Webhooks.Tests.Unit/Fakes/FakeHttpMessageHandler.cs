using System.Net;

namespace Wiaoj.Webhooks.Tests.Unit.Fakes;

internal sealed class FakeHttpMessageHandler : HttpMessageHandler {
    private readonly HttpStatusCode _statusCode;
    private readonly string _responseBody;

    public HttpRequestMessage? LastRequest { get; private set; }
    public string? LastRequestBody { get; private set; }

    public FakeHttpMessageHandler(HttpStatusCode statusCode) : this(statusCode, string.Empty) {
    }

    public FakeHttpMessageHandler(HttpStatusCode statusCode, string responseBody) {
        this._statusCode = statusCode;
        this._responseBody = responseBody;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
        this.LastRequest = request;
        this.LastRequestBody = request.Content is null
            ? null
            : await request.Content.ReadAsStringAsync(cancellationToken);

        return new HttpResponseMessage(this._statusCode) {
            Content = new StringContent(this._responseBody)
        };
    }
}

internal sealed class HangingHttpMessageHandler : HttpMessageHandler {
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
        await Task.Delay(Timeout.Infinite, cancellationToken);
        return new HttpResponseMessage(HttpStatusCode.OK); // unreachable — only exists to satisfy return type
    }
}