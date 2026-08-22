using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Net.Mime;
using System.Text;
using Microsoft.Extensions.Logging;
using Wiaoj.Webhooks.Diagnostics;

namespace Wiaoj.Webhooks.Internal;

/// <summary>
/// Low-level sender responsible for issuing the raw HTTP POST request for a webhook delivery.
/// </summary>
/// <remarks>
/// This type is intentionally narrow: it knows nothing about <see cref="WebhookDeliveryContext"/>,
/// signing, retries, or the pipeline. It only knows how to POST a string payload to a URL and
/// return the raw <see cref="HttpResponseMessage"/>. <see cref="HttpWebhookDeliverer"/> builds on
/// top of it to satisfy the <see cref="IWebhookDeliverer"/> contract.
/// </remarks>
internal sealed class HttpWebhookSender { 
    private readonly HttpClient _httpClient;
    private readonly ILogger<HttpWebhookSender> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="HttpWebhookSender"/> class.
    /// </summary>
    /// <param name="httpClient">The <see cref="HttpClient"/> used to issue requests.</param>
    /// <param name="logger">The logger instance.</param>
    public HttpWebhookSender(HttpClient httpClient, ILogger<HttpWebhookSender> logger) {
        Preca.ThrowIfNull(httpClient);
        Preca.ThrowIfNull(logger);
        this._httpClient = httpClient;
        this._logger = logger;
    }

    /// <summary>
    /// Sends <paramref name="payload"/> as a JSON POST request to <paramref name="targetUrl"/> with custom headers.
    /// </summary>
    /// <param name="targetUrl">The destination URL.</param>
    /// <param name="payload">The already-serialized JSON payload to send as the request body.</param>
    /// <param name="headers">Custom HTTP headers to include in the request.</param>
    /// <param name="cancellationToken">A token to observe for cancellation requests.</param>
    /// <returns>The raw <see cref="HttpResponseMessage"/> returned by the target.</returns>
    public async Task<HttpResponseMessage> SendAsync(Uri targetUrl,
                                                     string payload,
                                                     IReadOnlyDictionary<string, string> headers,
                                                     CancellationToken cancellationToken) {
        Preca.ThrowIfNull(targetUrl);
        Preca.ThrowIfNull(payload);
        Preca.ThrowIfNull(headers);

        this._logger.LogHttpRequestIssuing(targetUrl, payload.Length);

        using Activity? activity = WebhookActivitySource.StartHttpActivity(targetUrl);

        HttpRequestMessage request = new(HttpMethod.Post, targetUrl) {
            Content = new StringContent(payload, Encoding.UTF8, MediaTypeNames.Application.Json)
        };

        foreach(KeyValuePair<string, string> header in headers) {
            request.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        long startTimestamp = Stopwatch.GetTimestamp();
        try {
            // 🌟 ResponseHeadersRead: Yanıtın devasa gövdesini beklemeden başlıklar geldiği an döner!
            HttpResponseMessage response = await this._httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken).ConfigureAwait(false);

            double durationMs = Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds;

            WebhookMeter.HttpRequestDuration.Record(durationMs, new TagList {
                { "http.response.status_code", (int)response.StatusCode },
                { "url.full", targetUrl.ToString() }
            });

            activity?.SetTag("http.response.status_code", (int)response.StatusCode);
            this._logger.LogHttpResponseReceived((int)response.StatusCode, targetUrl, durationMs);

            return response;
        }
        catch(Exception ex) {
            double durationMs = Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds;
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);
            throw;
        }
    }
}