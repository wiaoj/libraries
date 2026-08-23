using Microsoft.AspNetCore.Http;

namespace Wiaoj.Webhooks.AspNetCore.Authentication;

/// <summary>
/// Strategy for verifying inbound webhook signatures with automatic unmanaged secret lifetime management.
/// </summary>
public interface IWebhookSecretResolver {
    /// <summary>
    /// Verifies the authenticity of an incoming webhook signature against the resolved secret in unmanaged memory.
    /// </summary>
    ValueTask<bool> VerifyAsync(
        HttpContext httpContext,
        ReadOnlyMemory<byte> payload,
        string signatureHeader,
        IWebhookSigner signer,
        TimeSpan tolerance,
        UnixTimestamp currentTimestamp,
        CancellationToken cancellationToken = default);
}