using Wiaoj.Webhooks.Signing.Asymmetric;

namespace Wiaoj.Webhooks.Tests.Unit.Fakes;

/// <summary>
/// Test double for <see cref="AsymmetricWebhookSignerBase"/> allowing controlled verification callbacks.
/// </summary>
internal sealed class FakeAsymmetricWebhookSigner : AsymmetricWebhookSignerBase {
    private readonly int _expectedSignatureLength;
    private readonly Func<ReadOnlySpan<byte>, ReadOnlySpan<byte>, bool>? _verifierCallback;

    /// <inheritdoc/>
    public override string AlgorithmName => "fake-asymmetric";

    /// <inheritdoc/>
    public override string SchemePrefix => "v1_test";

    /// <summary>
    /// Initializes a new instance of the <see cref="FakeAsymmetricWebhookSigner"/> class.
    /// </summary>
    /// <param name="expectedSignatureLength">The expected signature binary length in bytes.</param>
    /// <param name="verifierCallback">The optional verification delegate.</param>
    /// <param name="headerName">The custom HTTP header name.</param>
    public FakeAsymmetricWebhookSigner(
        int expectedSignatureLength = 64,
        Func<ReadOnlySpan<byte>, ReadOnlySpan<byte>, bool>? verifierCallback = null,
        string headerName = DefaultHeaderName) : base(headerName) {
        this._expectedSignatureLength = expectedSignatureLength;
        this._verifierCallback = verifierCallback;
    }

    /// <inheritdoc/>
    public override WebhookSignature Sign(ReadOnlySpan<byte> payload, ReadOnlySpan<byte> secretKey, UnixTimestamp timestamp) =>
        new(timestamp, this.SchemePrefix, Convert.ToBase64String(new byte[this._expectedSignatureLength]));

    /// <inheritdoc/>
    public override WebhookSignature Sign(ReadOnlySpan<byte> payload, Secret<byte> secretKey, UnixTimestamp timestamp) =>
        Sign(payload, ReadOnlySpan<byte>.Empty, timestamp);

    /// <inheritdoc/>
    public override bool Verify(
        ReadOnlySpan<byte> payload,
        string signatureHeader,
        ReadOnlySpan<byte> secretKey,
        TimeSpan tolerance,
        UnixTimestamp currentTimestamp) {

        TestVerifier verifier = new(this._verifierCallback);
        return VerifyAsymmetricCore(
            payload,
            signatureHeader,
            secretKey.ToArray(),
            tolerance,
            currentTimestamp,
            this._expectedSignatureLength,
            verifier);
    }

    /// <inheritdoc/>
    public override bool Verify(
        ReadOnlySpan<byte> payload,
        string signatureHeader,
        Secret<byte> secretKey,
        TimeSpan tolerance,
        UnixTimestamp currentTimestamp) =>
        Verify(payload, signatureHeader, ReadOnlySpan<byte>.Empty, tolerance, currentTimestamp);

    private readonly struct TestVerifier(Func<ReadOnlySpan<byte>, ReadOnlySpan<byte>, bool>? callback) : IVerifier<byte[]> {
        private readonly Func<ReadOnlySpan<byte>, ReadOnlySpan<byte>, bool>? _callback = callback;

        public bool Verify(byte[] key, ReadOnlySpan<byte> data, ReadOnlySpan<byte> signature) =>
            this._callback?.Invoke(data, signature) ?? true;
    }
}