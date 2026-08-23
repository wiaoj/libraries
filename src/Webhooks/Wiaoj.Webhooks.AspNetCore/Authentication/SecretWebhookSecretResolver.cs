using Microsoft.AspNetCore.Http;

#pragma warning disable IDE0130
namespace Wiaoj.Webhooks.AspNetCore.Authentication;
#pragma warning restore IDE0130

/// <summary>
/// Verifies inbound webhook signatures using a persistent unmanaged <see cref="Secret{Byte}"/>.
/// Does not dispose the underlying unmanaged memory across requests.
/// </summary>
public sealed class SecretWebhookSecretResolver : IWebhookSecretResolver {
    private readonly Secret<byte> _secret;

    /// <summary>
    /// Initializes a new instance of the <see cref="SecretWebhookSecretResolver"/> class.
    /// </summary>
    /// <param name="secret">The unmanaged secret stored in GC-immune memory.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="secret"/> is null.</exception>
    public SecretWebhookSecretResolver(Secret<byte> secret) {
        Preca.ThrowIfNull(secret);
        this._secret = secret;
    }

    /// <inheritdoc/>
    public ValueTask<bool> VerifyAsync(
        HttpContext httpContext,
        ReadOnlyMemory<byte> payload,
        string signatureHeader,
        IWebhookSigner signer,
        TimeSpan tolerance,
        UnixTimestamp currentTimestamp,
        CancellationToken cancellationToken = default) {

        Preca.ThrowIfNull(signer);

        bool isValid = signer.Verify(
            payload.Span,
            signatureHeader,
            this._secret,
            tolerance,
            currentTimestamp);

        return ValueTask.FromResult(isValid);
    }
}

/// <summary>
/// Verifies inbound webhook signatures by decrypting an <see cref="EncryptedSecret{TContext}"/> on-demand
/// and immediately zeroing and freeing unmanaged memory after verification.
/// </summary>
public sealed class EncryptedWebhookSecretResolver : IWebhookSecretResolver {
    private readonly EncryptedSecret<WebhookSigningContext> _encryptedSecret;
    private readonly ISecretProtector<WebhookSigningContext> _secretProtector;

    /// <summary>
    /// Initializes a new instance of the <see cref="EncryptedWebhookSecretResolver"/> class.
    /// </summary>
    /// <param name="encryptedSecret">The encrypted secret key at rest.</param>
    /// <param name="secretProtector">The secret protector used to decrypt the key on-demand.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="encryptedSecret"/> is default.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="secretProtector"/> is null.</exception>
    public EncryptedWebhookSecretResolver(
        EncryptedSecret<WebhookSigningContext> encryptedSecret,
        ISecretProtector<WebhookSigningContext> secretProtector) {
        Preca.ThrowIfDefault(encryptedSecret);
        Preca.ThrowIfNull(secretProtector);

        this._encryptedSecret = encryptedSecret;
        this._secretProtector = secretProtector;
    }

    /// <inheritdoc/>
    public ValueTask<bool> VerifyAsync(
        HttpContext httpContext,
        ReadOnlyMemory<byte> payload,
        string signatureHeader,
        IWebhookSigner signer,
        TimeSpan tolerance,
        UnixTimestamp currentTimestamp,
        CancellationToken cancellationToken = default) {

        Preca.ThrowIfNull(signer);

        using Secret<byte> secret = this._secretProtector.Unprotect(this._encryptedSecret);

        bool isValid = signer.Verify(
            payload.Span,
            signatureHeader,
            secret,
            tolerance,
            currentTimestamp);

        return ValueTask.FromResult(isValid);
    }
}