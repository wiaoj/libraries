using Microsoft.AspNetCore.Http;

#pragma warning disable IDE0130
namespace Wiaoj.Webhooks.AspNetCore.Authentication;
#pragma warning restore IDE0130

/// <summary>
/// Resolves unmanaged webhook secrets dynamically via a delegate (e.g. multi-tenant database lookup)
/// and securely disposes the ephemeral secret after signature verification.
/// </summary>
public sealed class DelegateWebhookSecretResolver : IWebhookSecretResolver {
    private readonly Func<HttpContext, CancellationToken, ValueTask<Secret<byte>>> _resolverDelegate;

    /// <summary>
    /// Initializes a new instance of the <see cref="DelegateWebhookSecretResolver"/> class.
    /// </summary>
    /// <param name="resolverDelegate">The delegate used to resolve the unmanaged secret.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="resolverDelegate"/> is null.</exception>
    public DelegateWebhookSecretResolver(Func<HttpContext, CancellationToken, ValueTask<Secret<byte>>> resolverDelegate) {
        Preca.ThrowIfNull(resolverDelegate);
        this._resolverDelegate = resolverDelegate;
    }

    /// <inheritdoc/>
    public async ValueTask<bool> VerifyAsync(
        HttpContext httpContext,
        ReadOnlyMemory<byte> payload,
        string signatureHeader,
        IWebhookSigner signer,
        TimeSpan tolerance,
        UnixTimestamp currentTimestamp,
        CancellationToken cancellationToken = default) {

        Preca.ThrowIfNull(signer);

        using Secret<byte> secret = await this._resolverDelegate(httpContext, cancellationToken).ConfigureAwait(false);

        return signer.Verify(
            payload.Span,
            signatureHeader,
            secret,
            tolerance,
            currentTimestamp);
    }
}