using Microsoft.Extensions.Logging;
using System.Text;
using Wiaoj.Security;
using Wiaoj.Webhooks.Diagnostics;

namespace Wiaoj.Webhooks.Internal;

/// <summary>
/// Outbound pipeline middleware that calculates cryptographic signatures for webhook payloads
/// and injects the resulting signature header into the delivery context.
/// </summary>
public sealed class SigningMiddleware : IWebhookMiddleware {
    private readonly IWebhookSigner _signer;
    private readonly ISecretProtector<WebhookSigningContext> _secretProtector;
    private readonly ILogger<SigningMiddleware> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SigningMiddleware"/> class.
    /// </summary>
    /// <param name="signer">The webhook signer instance.</param>
    /// <param name="secretProtector">The secret protector used to unprotect endpoint secrets.</param>
    /// <param name="logger">The logger instance.</param>
    public SigningMiddleware(
        IWebhookSigner signer,
        ISecretProtector<WebhookSigningContext> secretProtector,
        ILogger<SigningMiddleware> logger) {
        Preca.ThrowIfNull(signer);
        Preca.ThrowIfNull(secretProtector);
        Preca.ThrowIfNull(logger);

        this._signer = signer;
        this._secretProtector = secretProtector;
        this._logger = logger;
    }

    /// <inheritdoc/>
    public async Task InvokeAsync(WebhookDeliveryContext context, WebhookDelegate next, CancellationToken cancellationToken) {
        Preca.ThrowIfNull(context);
        Preca.ThrowIfNull(next);

        using Secret<byte> secretKey = this._secretProtector.Unprotect(context.Endpoint.Secret);
        byte[] payloadBytes = Encoding.UTF8.GetBytes(context.SerializedPayload);
        UnixTimestamp now = UnixTimestamp.Now;

        WebhookSignature signature = this._signer.Sign(payloadBytes, secretKey, now);

        context.SetHeader(this._signer.HeaderName, signature.HeaderValue);
        context.SetSignature(signature);

        this._logger.LogSigningCompleted(context.Endpoint.Id, this._signer.AlgorithmName, now.TotalSeconds);

        await next(context, cancellationToken);
    }
}