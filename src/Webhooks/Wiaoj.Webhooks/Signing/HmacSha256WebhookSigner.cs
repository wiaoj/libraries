using Wiaoj.Primitives.Cryptography.Hashing;

namespace Wiaoj.Webhooks.Signing;

/// <summary>
/// Implements HMAC-SHA256 webhook payload signing and verification (Scheme prefix: "v1").
/// </summary>
public sealed class HmacSha256WebhookSigner : HmacWebhookSignerBase {

    /// <inheritdoc/>
    public override string AlgorithmName => "hmac-sha256";

    /// <inheritdoc/>
    public override string SchemePrefix => "v1";

    /// <summary>
    /// Initializes a new instance of the <see cref="HmacSha256WebhookSigner"/> class with the default header name ("Wiaoj-Signature").
    /// </summary>
    public HmacSha256WebhookSigner() : base() {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="HmacSha256WebhookSigner"/> class with a custom header name.
    /// </summary>
    /// <param name="headerName">The custom HTTP header name.</param>
    public HmacSha256WebhookSigner(string headerName) : base(headerName) {
    }

    /// <inheritdoc/>
    protected override string ComputeHashString(ReadOnlySpan<byte> key, ReadOnlySpan<byte> data) {
        HmacSha256Hash hash = HmacSha256Hash.Compute(key, data);
        return hash.ToHexStringLower().ToString();
    }
}
