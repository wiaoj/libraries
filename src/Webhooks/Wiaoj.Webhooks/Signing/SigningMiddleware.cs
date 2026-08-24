using Microsoft.Extensions.Logging;
using Wiaoj.Extensions;
using Wiaoj.Security;
using Wiaoj.Webhooks.Diagnostics;

namespace Wiaoj.Webhooks.Signing;

/// <summary>
/// Outbound pipeline middleware that calculates cryptographic signatures for webhook payloads
/// and injects the resulting signature header into the delivery context.
/// Supports endpoint-specific signer overrides.
/// </summary>
public sealed class SigningMiddleware : IWebhookMiddleware {
    private readonly IWebhookSigner _signer;
    private readonly ISecretProtector<WebhookSigningContext> _secretProtector;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<SigningMiddleware> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SigningMiddleware"/> class.
    /// </summary>
    /// <param name="signer">The default fallback webhook signer instance.</param>
    /// <param name="secretProtector">The secret protector used to unprotect endpoint secrets.</param>
    /// <param name="timeProvider">The time provider used for timestamp calculations.</param>
    /// <param name="logger">The logger instance.</param>
    /// <exception cref="ArgumentNullException">Thrown when any required parameter is <see langword="null"/>.</exception>
    public SigningMiddleware(
        IWebhookSigner signer,
        ISecretProtector<WebhookSigningContext> secretProtector,
        TimeProvider timeProvider,
        ILogger<SigningMiddleware> logger) {
        Preca.ThrowIfNull(signer);
        Preca.ThrowIfNull(secretProtector);
        Preca.ThrowIfNull(timeProvider);
        Preca.ThrowIfNull(logger);

        this._signer = signer;
        this._secretProtector = secretProtector;
        this._timeProvider = timeProvider;
        this._logger = logger;
    }

    /// <inheritdoc/>
    public async Task InvokeAsync(WebhookDeliveryContext context, WebhookDelegate next, CancellationToken cancellationToken = default) {
        Preca.ThrowIfNull(context);
        Preca.ThrowIfNull(next);

        IWebhookSigner activeSigner = context.Endpoint.CustomSigner ?? this._signer;

        using Secret<byte> secretKey = this._secretProtector.Unprotect(context.Endpoint.Secret);
        byte[] payloadBytes = context.SerializedPayload.ToUtf8Bytes();
        UnixTimestamp now = this._timeProvider.GetUnixTimestamp();

        WebhookSignature signature = activeSigner.Sign(payloadBytes, secretKey, now);

        context.SetHeader(activeSigner.HeaderName, signature.HeaderValue);
        context.SetSignature(signature);

        this._logger.LogSigningCompleted(context.Endpoint.Id, activeSigner.AlgorithmName, now.TotalSeconds);

        await next(context, cancellationToken);
    }
}