using System.Buffers;
using System.Security.Cryptography;
using System.Text;
using Wiaoj.Primitives.Buffers;

namespace Wiaoj.Webhooks.Signing;

/// <summary>
/// Abstract base class for HMAC-based webhook signers implementing timestamp-bound signing and constant-time verification.
/// </summary>
public abstract class HmacWebhookSignerBase : WebhookSignerBase {
    private const int SignedBytesStackBufferSize = 256;
    private const int SignatureRangeInitialCapacity = 4;
    private const int HexCharStackBufferSize = 128;

    /// <summary>
    /// Initializes a new instance of the <see cref="HmacWebhookSignerBase"/> class using the default header name.
    /// </summary>
    protected HmacWebhookSignerBase() : base(DefaultHeaderName) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="HmacWebhookSignerBase"/> class using a custom header name.
    /// </summary>
    /// <param name="headerName">The custom HTTP header name.</param>
    protected HmacWebhookSignerBase(string headerName) : base(headerName) { }

    /// <summary>
    /// Computes the cryptographic HMAC hash for the given data and returns its lowercase hexadecimal representation.
    /// </summary>
    /// <param name="key">The secret key bytes.</param>
    /// <param name="data">The combined timestamp and payload data.</param>
    /// <returns>A lowercase hexadecimal string representing the hash.</returns>
    protected abstract string ComputeHashString(ReadOnlySpan<byte> key, ReadOnlySpan<byte> data);

    /// <inheritdoc/>
    public override WebhookSignature Sign(ReadOnlySpan<byte> payload, ReadOnlySpan<byte> secretKey, UnixTimestamp timestamp) {
        Preca.ThrowIfEmpty(secretKey);

        using ValueBuffer<byte> signedBytes = CreateSignedBytes(payload, timestamp, stackalloc byte[SignedBytesStackBufferSize]);
        string hash = ComputeHashString(secretKey, signedBytes.Span);
        return new WebhookSignature(timestamp, this.SchemePrefix, hash);
    }

    /// <inheritdoc/>
    public override WebhookSignature Sign(ReadOnlySpan<byte> payload, Secret<byte> secretKey, UnixTimestamp timestamp) {
        Preca.ThrowIfNull(secretKey);

        using ValueBuffer<byte> signedBytes = CreateSignedBytes(payload, timestamp, stackalloc byte[SignedBytesStackBufferSize]);
        string hash = secretKey.Expose(signedBytes.Span, (state, keySpan) => ComputeHashString(keySpan, state));
        return new WebhookSignature(timestamp, this.SchemePrefix, hash);
    }

    /// <inheritdoc/>
    public override bool Verify(
        ReadOnlySpan<byte> payload,
        string signatureHeader,
        ReadOnlySpan<byte> secretKey,
        TimeSpan tolerance,
        UnixTimestamp currentTimestamp) {

        if(string.IsNullOrWhiteSpace(signatureHeader) || secretKey.IsEmpty) {
            return false;
        }

        ValueList<Range> signatureRanges = new(stackalloc Range[SignatureRangeInitialCapacity]);
        try {
            if(!ValidateVerificationParameters(signatureHeader.AsSpan(), tolerance, currentTimestamp, out UnixTimestamp headerTimestamp, ref signatureRanges)) {
                return false;
            }

            using ValueBuffer<byte> signedBytes = CreateSignedBytes(payload, headerTimestamp, stackalloc byte[SignedBytesStackBufferSize]);
            string expectedSignature = ComputeHashString(secretKey, signedBytes.Span);
            return VerifyConstantTime(signatureHeader.AsSpan(), expectedSignature.AsSpan(), signatureRanges.AsSpan());
        }
        finally {
            signatureRanges.Dispose();
        }
    }

    /// <inheritdoc/>
    public override bool Verify(
        ReadOnlySpan<byte> payload,
        string signatureHeader,
        Secret<byte> secretKey,
        TimeSpan tolerance,
        UnixTimestamp currentTimestamp) {

        Preca.ThrowIfNull(secretKey);

        if(string.IsNullOrWhiteSpace(signatureHeader)) {
            return false;
        }

        ValueList<Range> signatureRanges = new(stackalloc Range[SignatureRangeInitialCapacity]);
        try {
            if(!ValidateVerificationParameters(signatureHeader.AsSpan(), tolerance, currentTimestamp, out UnixTimestamp headerTimestamp, ref signatureRanges)) {
                return false;
            }

            using ValueBuffer<byte> signedBytes = CreateSignedBytes(payload, headerTimestamp, stackalloc byte[SignedBytesStackBufferSize]);
            string expectedSignature = secretKey.Expose(signedBytes.Span, (state, keySpan) => ComputeHashString(keySpan, state));
            return VerifyConstantTime(signatureHeader.AsSpan(), expectedSignature.AsSpan(), signatureRanges.AsSpan());
        }
        finally {
            signatureRanges.Dispose();
        }
    }

    private static bool VerifyConstantTime(
        ReadOnlySpan<char> header,
        ReadOnlySpan<char> expectedSignature,
        ReadOnlySpan<Range> signatures) {

        if(!Ascii.IsValid(expectedSignature)) {
            return false;
        }

        using ValueBuffer<byte> expectedBuffer = ValueBuffer.Create(expectedSignature.Length, stackalloc byte[HexCharStackBufferSize]);
        Span<byte> expectedBytes = expectedBuffer.Span;
        Encoding.ASCII.GetBytes(expectedSignature, expectedBytes);

        using ValueBuffer<byte> candidateBuffer = ValueBuffer.Create(expectedSignature.Length, stackalloc byte[HexCharStackBufferSize]);
        Span<byte> candidateBytes = candidateBuffer.Span;

        for(int i = 0; i < signatures.Length; i++) {
            ReadOnlySpan<char> candidate = header[signatures[i]];

            if(candidate.Length != expectedSignature.Length || !Ascii.IsValid(candidate)) {
                continue;
            }

            Encoding.ASCII.GetBytes(candidate, candidateBytes);

            if(Ascii.ToLowerInPlace(candidateBytes, out _) != OperationStatus.Done) {
                continue;
            }

            if(CryptographicOperations.FixedTimeEquals(expectedBytes, candidateBytes)) {
                return true;
            }
        }

        return false;
    }
}