using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Buffers;
using System.Text;
using Wiaoj.Webhooks.Diagnostics;
using Wiaoj.Webhooks.Exceptions;
using Wiaoj.Webhooks.Retries;
using Wiaoj.Webhooks.Security;

namespace Wiaoj.Webhooks.Internal;

/// <summary>
/// Default <see cref="IWebhookDeliverer"/> implementation that delivers webhooks over HTTP.
/// </summary>
/// <remarks>
/// Delegates the raw request/response mechanics to <see cref="HttpWebhookSender"/> and is
/// responsible only for translating <see cref="WebhookDeliveryContext"/> into a request and
/// the resulting <see cref="HttpResponseMessage"/> into a <see cref="WebhookDeliveryResult"/>.
/// </remarks>
internal sealed class HttpWebhookDeliverer : IWebhookDeliverer {
    private readonly HttpWebhookSender _sender;
    private readonly WebhookSecurityOptions _securityOptions;
    private readonly ILogger<HttpWebhookDeliverer> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="HttpWebhookDeliverer"/> class.
    /// </summary>
    /// <param name="sender">The low-level HTTP sender used to perform the request.</param>
    /// <param name="securityOptions">The security and outbound hardening options.</param>
    /// <param name="logger">The logger instance.</param>
    public HttpWebhookDeliverer(
        HttpWebhookSender sender,
        IOptions<WebhookSecurityOptions> securityOptions,
        ILogger<HttpWebhookDeliverer> logger) {
        Preca.ThrowIfNull(sender);
        Preca.ThrowIfNull(securityOptions);
        Preca.ThrowIfNull(logger);

        this._sender = sender;
        this._securityOptions = securityOptions.Value;
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

            // 🌟 Güvenli Yanıt Okuma: Asla 50MB belleğe çekilmez, en fazla MaxResponseBodyBytes okunur!
            string responseBody = await ReadBoundedResponseBodyAsync(response.Content, this._securityOptions.MaxResponseBodyBytes, cancellationToken).ConfigureAwait(false);
            int statusCode = (int)response.StatusCode;

            if(response.IsSuccessStatusCode) {
                return WebhookDeliveryResult.Success(statusCode, responseBody);
            }

            return HttpStatusClassifier.IsTransient(statusCode)
                ? WebhookDeliveryResult.Transient($"HTTP request failed with status code {statusCode}.", statusCode)
                : WebhookDeliveryResult.Permanent($"HTTP request permanently rejected with status code {statusCode}.", statusCode, PermanentFailureReason.DestinationRejected);
        }
        catch(OperationCanceledException) when(!cancellationToken.IsCancellationRequested) {
            this._logger.LogHttpRequestTimedOut(context.TargetUrl, context.Endpoint.Id);
            return WebhookDeliveryResult.Transient($"Request to '{context.TargetUrl}' timed out.");
        }
        catch(WebhookSsrfBlockedException ex) { 
            return WebhookDeliveryResult.Permanent($"SSRF protection blocked destination: {ex.Message}", PermanentFailureReason.InvalidDestination);
        }
        catch(HttpRequestException ex) {
            int? statusCode = (int?)ex.StatusCode;
            return HttpStatusClassifier.IsTransient(statusCode)
                ? WebhookDeliveryResult.Transient($"HTTP network failure: {ex.Message}", statusCode ?? 0)
                : WebhookDeliveryResult.Permanent($"HTTP client error: {ex.Message}", statusCode ?? 0, PermanentFailureReason.DestinationRejected);
        }
    }

    private static async Task<string> ReadBoundedResponseBodyAsync(HttpContent content, int maxBytes, CancellationToken ct) {
        using Stream stream = await content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        byte[] buffer = ArrayPool<byte>.Shared.Rent(maxBytes);

        try {
            int totalBytesRead = 0;
            while(totalBytesRead < maxBytes) {
                int read = await stream.ReadAsync(buffer.AsMemory(totalBytesRead, maxBytes - totalBytesRead), ct).ConfigureAwait(false);
                if(read == 0) break;
                totalBytesRead += read;
            }

            if(totalBytesRead == 0) return string.Empty;

            string body = Encoding.UTF8.GetString(buffer, 0, totalBytesRead);

            int peek = stream.ReadByte();
            return peek != -1 ? $"{body} [truncated...]" : body;
        }
        finally {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
}