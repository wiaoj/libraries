using Wiaoj.Primitives.Buffers;
using Wiaoj.Webhooks.Signing;

namespace Wiaoj.Webhooks.Tests.Unit.Fakes;

/// <summary>
/// Minimal test double inheriting directly from <see cref="WebhookSignerBase"/> to test root base logic in isolation.
/// </summary>
internal sealed class FakeWebhookSigner : WebhookSignerBase {
    private const int SignedBytesStackBufferSize = 256;
    private const int SignatureRangeInitialCapacity = 4;

    /// <inheritdoc/>
    public override string AlgorithmName => "fake-root-signer";

    /// <inheritdoc/>
    public override string SchemePrefix => "v1";

    /// <summary>
    /// Initializes a new instance of the <see cref="FakeWebhookSigner"/> class using the default header name.
    /// </summary>
    public FakeWebhookSigner() : base() { }

    /// <summary>
    /// Initializes a new instance of the <see cref="FakeWebhookSigner"/> class using a custom header name.
    /// </summary>
    /// <param name="headerName">The custom HTTP header name.</param>
    public FakeWebhookSigner(string headerName) : base(headerName) { }

    /// <inheritdoc/>
    public override WebhookSignature Sign(ReadOnlySpan<byte> payload, ReadOnlySpan<byte> secretKey, UnixTimestamp timestamp) {
        return new(timestamp, this.SchemePrefix, "dummy-sig");
    }

    /// <inheritdoc/>
    public override WebhookSignature Sign(ReadOnlySpan<byte> payload, Secret<byte> secretKey, UnixTimestamp timestamp) {
        return new(timestamp, this.SchemePrefix, "dummy-sig");
    }

    /// <inheritdoc/>
    public override bool Verify(
        ReadOnlySpan<byte> payload,
        string signatureHeader,
        ReadOnlySpan<byte> secretKey,
        TimeSpan tolerance,
        UnixTimestamp currentTimestamp) {

        ValueList<Range> ranges = new(stackalloc Range[SignatureRangeInitialCapacity]);
        try {
            return ValidateVerificationParameters(signatureHeader.AsSpan(), tolerance, currentTimestamp, out _, ref ranges);
        }
        finally {
            ranges.Dispose();
        }
    }

    /// <inheritdoc/>
    public override bool Verify(
        ReadOnlySpan<byte> payload,
        string signatureHeader,
        Secret<byte> secretKey,
        TimeSpan tolerance,
        UnixTimestamp currentTimestamp) {
        return Verify(payload, signatureHeader, ReadOnlySpan<byte>.Empty, tolerance, currentTimestamp);
    }

    /// <summary>
    /// Exposes the protected <see cref="WebhookSignerBase.CreateSignedBytes"/> method for unit testing buffer edge cases.
    /// </summary>
    /// <param name="payload">The raw payload span.</param>
    /// <param name="timestamp">The generation timestamp.</param>
    /// <returns>The formatted canonical byte array.</returns>
    public byte[] ExposeCreateSignedBytes(ReadOnlySpan<byte> payload, UnixTimestamp timestamp) {
        using ValueBuffer<byte> buffer = CreateSignedBytes(payload, timestamp, stackalloc byte[SignedBytesStackBufferSize]);
        return buffer.Span.ToArray();
    }
}