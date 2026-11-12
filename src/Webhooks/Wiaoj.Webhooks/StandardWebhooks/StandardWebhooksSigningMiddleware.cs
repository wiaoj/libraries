using Wiaoj.Extensions;
using Wiaoj.Security;

namespace Wiaoj.Webhooks.Signing;

/// <summary>
/// Signs a delivery as <see href="https://www.standardwebhooks.com/">Standard Webhooks</see> do: <c>webhook-id</c> (the
/// job's id, the same on every attempt), <c>webhook-timestamp</c> and <c>webhook-signature</c>
/// (<see cref="StandardWebhooksSignature"/>).
/// </summary>
/// <remarks>
/// While a secret is rotated (<see cref="WebhookEndpoint.SecondarySecret"/>) the delivery carries a signature under each,
/// space separated, as the specification allows, so the receiver may switch at any moment.
/// </remarks>
public sealed class StandardWebhooksSigningMiddleware : IWebhookMiddleware {
    private readonly ISecretProtector<WebhookSigningContext> _secretProtector;
    private readonly TimeProvider _timeProvider;

    /// <summary>Signs with the endpoint's secrets, unprotected by <paramref name="secretProtector"/>, at <paramref name="timeProvider"/>'s time.</summary>
    public StandardWebhooksSigningMiddleware(ISecretProtector<WebhookSigningContext> secretProtector, TimeProvider timeProvider) {
        Preca.ThrowIfNull(secretProtector);
        Preca.ThrowIfNull(timeProvider);
        this._secretProtector = secretProtector;
        this._timeProvider = timeProvider;
    }

    /// <inheritdoc/>
    public async Task InvokeAsync(WebhookDeliveryContext context, WebhookDelegate next, CancellationToken cancellationToken = default) {
        Preca.ThrowIfNull(context);
        Preca.ThrowIfNull(next);

        string id = context.JobId.Value;
        long timestamp = this._timeProvider.GetUtcNow().ToUnixTimeSeconds();
        byte[] body = context.SerializedPayload.ToUtf8Bytes();

        string signature = this.SignWith(context.Endpoint.Secret, id, timestamp, body);
        if(context.Endpoint.SecondarySecret is EncryptedSecret<WebhookSigningContext> secondary) {
            signature = $"{signature} {this.SignWith(secondary, id, timestamp, body)}";
        }

        context.SetHeader(StandardWebhooksSignature.IdHeader, id);
        context.SetHeader(StandardWebhooksSignature.TimestampHeader, timestamp.ToString(System.Globalization.CultureInfo.InvariantCulture));
        context.SetHeader(StandardWebhooksSignature.SignatureHeader, signature);

        await next(context, cancellationToken);
    }

    private string SignWith(EncryptedSecret<WebhookSigningContext> encrypted, string id, long timestamp, byte[] body) {
        using Secret<byte> secret = this._secretProtector.Unprotect(encrypted);
        return secret.Expose((id, timestamp, body), static (state, secretBytes) => {
            byte[] key = StandardWebhooksSignature.KeyOf(secretBytes);
            try {
                return StandardWebhooksSignature.Sign(state.id, state.timestamp, state.body, key);
            }
            finally {
                System.Security.Cryptography.CryptographicOperations.ZeroMemory(key);
            }
        });
    }
}
