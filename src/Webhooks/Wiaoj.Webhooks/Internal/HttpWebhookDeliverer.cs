using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text;
using Wiaoj.Primitives.Buffers;
using Wiaoj.Webhooks.Diagnostics;
using Wiaoj.Webhooks.Retries;
using Wiaoj.Webhooks.Security;

namespace Wiaoj.Webhooks.Internal;

/// <summary>
/// Default <see cref="IWebhookDeliverer"/> implementation that delivers webhooks over HTTP/HTTPS.
/// </summary>
/// <remarks>
/// Delegates the raw HTTP request dispatching to <see cref="HttpWebhookSender"/> and translates the resulting
/// <see cref="HttpResponseMessage"/> (including status codes, bounded body inspection, and <c>Retry-After</c> headers)
/// into a strongly-typed <see cref="WebhookDeliveryResult"/>.
/// </remarks>
internal sealed class HttpWebhookDeliverer : IWebhookDeliverer {
    private readonly HttpWebhookSender _sender;
    private readonly WebhookSecurityOptions _securityOptions;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<HttpWebhookDeliverer> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="HttpWebhookDeliverer"/> class.
    /// </summary>
    /// <param name="sender">The low-level HTTP sender used to perform the request.</param>
    /// <param name="securityOptions">The security and outbound hardening options.</param>
    /// <param name="timeProvider">The time provider used for timestamp and date calculations.</param>
    /// <param name="logger">The logger instance.</param>
    /// <exception cref="ArgumentNullException">Thrown when any required parameter is <see langword="null"/>.</exception>
    public HttpWebhookDeliverer(
        HttpWebhookSender sender,
        IOptions<WebhookSecurityOptions> securityOptions,
        TimeProvider timeProvider,
        ILogger<HttpWebhookDeliverer> logger) {
        Preca.ThrowIfNull(sender);
        Preca.ThrowIfNull(securityOptions);
        Preca.ThrowIfNull(timeProvider);
        Preca.ThrowIfNull(logger);

        this._sender = sender;
        this._securityOptions = securityOptions.Value;
        this._timeProvider = timeProvider;
        this._logger = logger;
    }

    /// <inheritdoc/>
    public async Task<WebhookDeliveryResult> DeliverAsync(WebhookDeliveryContext context, CancellationToken cancellationToken = default) {
        Preca.ThrowIfNull(context);

        IReadOnlyDictionary<string, string> headers = context.GetHeaders();

        try {
            using HttpResponseMessage response = await this._sender.SendAsync(
                context.TargetUrl,
                context.SerializedPayload,
                headers,
                cancellationToken).ConfigureAwait(false);

            string responseBody = await ReadBoundedResponseBodyAsync(
                response.Content,
                this._securityOptions.MaxResponseBodyBytes,
                cancellationToken).ConfigureAwait(false);

            int statusCode = (int)response.StatusCode;

            if(response.IsSuccessStatusCode) {
                return WebhookDeliveryResult.Success(statusCode, responseBody);
            }

            TimeSpan? retryAfter = response.Headers.ExtractRetryAfter(this._timeProvider);

            return HttpStatusClassifier.IsTransient(statusCode)
                ? WebhookDeliveryResult.Transient($"HTTP request failed with status code {statusCode}.", statusCode, retryAfter)
                : WebhookDeliveryResult.Permanent($"HTTP request permanently rejected with status code {statusCode}.", statusCode, PermanentFailureReason.DestinationRejected);
        }
        catch(OperationCanceledException) when(!cancellationToken.IsCancellationRequested) {
            this._logger.LogHttpRequestTimedOut(context.TargetUrl, context.Endpoint.Id);
            return WebhookDeliveryResult.Timeout($"Request to '{context.TargetUrl}' timed out.");
        }
        catch(Exception ex) when(ex.TryGetSsrfException(out WebhookSsrfBlockedException? ssrfEx)) {
            return WebhookDeliveryResult.Permanent(
                $"SSRF protection blocked destination: {ssrfEx.Message}",
                PermanentFailureReason.InvalidDestination);
        }
        catch(HttpRequestException ex) {
            int? statusCode = (int?)ex.StatusCode;
            return HttpStatusClassifier.IsTransient(statusCode)
                ? WebhookDeliveryResult.NetworkFailure($"HTTP network failure: {ex.Message}", statusCode, ex)
                : WebhookDeliveryResult.Permanent($"HTTP client error: {ex.Message}", statusCode, PermanentFailureReason.DestinationRejected);
        }
    }

    private static async Task<string> ReadBoundedResponseBodyAsync(HttpContent content, int maxBytes, CancellationToken ct) {
        using Stream stream = await content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        await using AsyncValueBuffer<byte> buffer = new(maxBytes);

        int totalBytesRead = 0;
        while(totalBytesRead < maxBytes) {
            int read = await stream.ReadAsync(buffer.Slice(totalBytesRead, maxBytes - totalBytesRead), ct).ConfigureAwait(false);
            if(read == 0) break;
            totalBytesRead += read;
        }

        if(totalBytesRead == 0) return string.Empty;

        string body = Encoding.UTF8.GetString(buffer.Span[..totalBytesRead]);

        Memory<byte> peekBuffer = new byte[1];
        int peekRead = await stream.ReadAsync(peekBuffer, ct).ConfigureAwait(false);
        return peekRead > 0 ? $"{body} [truncated...]" : body;
    }
}