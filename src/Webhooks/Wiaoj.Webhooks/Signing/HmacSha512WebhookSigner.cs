namespace Wiaoj.Webhooks.Signing;

/// <summary>
/// Implements HMAC-SHA512 webhook payload signing and verification (Scheme prefix: "v2").
/// </summary>
public sealed class HmacSha512WebhookSigner : HmacWebhookSignerBase {

    /// <inheritdoc/>
    public override string AlgorithmName => "hmac-sha512";

    /// <inheritdoc/>
    public override string SchemePrefix => "v2";

    /// <summary>
    /// Initializes a new instance of the <see cref="HmacSha512WebhookSigner"/> class with the default header name ("Wiaoj-Signature").
    /// </summary>
    public HmacSha512WebhookSigner() : base() { }

    /// <summary>
    /// Initializes a new instance of the <see cref="HmacSha512WebhookSigner"/> class with a custom header name.
    /// </summary>
    /// <param name="headerName">The custom HTTP header name.</param>
    public HmacSha512WebhookSigner(string headerName) : base(headerName) { }

    /// <inheritdoc/>
    protected override string ComputeHashString(ReadOnlySpan<byte> key, ReadOnlySpan<byte> data) {
        HmacSha512Hash hash = HmacSha512Hash.Compute(key, data);
        return hash.ToHexStringLower().ToString();
    }
}
