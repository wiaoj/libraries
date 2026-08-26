using Wiaoj.Primitives.Buffers;

namespace Wiaoj.Webhooks.Signing.Asymmetric;

/// <summary>
/// Abstract base class for asymmetric cryptographic webhook signers (RSA, ECDSA) implementing
/// timestamp-bound signing, Base64 signature extraction, and replay attack mitigation.
/// </summary>
public abstract class AsymmetricWebhookSignerBase : WebhookSignerBase {
    private const int SignedBytesStackBufferSize = 256;
    private const int SignatureRangeInitialCapacity = 4;
    private const int SignatureDecodeStackBufferSize = 512;

    /// <summary>
    /// Initializes a new instance of the <see cref="AsymmetricWebhookSignerBase"/> class using the default header name.
    /// </summary>
    protected AsymmetricWebhookSignerBase() : base(DefaultHeaderName) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="AsymmetricWebhookSignerBase"/> class using a custom header name.
    /// </summary>
    /// <param name="headerName">The custom HTTP header name.</param>
    protected AsymmetricWebhookSignerBase(string headerName) : base(headerName) { }

    /// <summary>
    /// Defines a zero-allocation verifier callback contract for asymmetric cryptography.
    /// </summary>
    /// <typeparam name="TKey">The asymmetric public key type.</typeparam>
    protected interface IVerifier<in TKey> {
        /// <summary>
        /// Verifies the signature bytes against the signed data span.
        /// </summary>
        /// <param name="key">The public key instance.</param>
        /// <param name="data">The signed canonical data span.</param>
        /// <param name="signature">The decoded binary signature span.</param>
        /// <returns><see langword="true"/> if authentic; otherwise, <see langword="false"/>.</returns>
        bool Verify(TKey key, ReadOnlySpan<byte> data, ReadOnlySpan<byte> signature);
    }

    /// <summary>
    /// Executes the zero-allocation asymmetric signature verification template.
    /// </summary>
    /// <typeparam name="TKey">The asymmetric public key type.</typeparam>
    /// <typeparam name="TVerifier">The verifier implementation type.</typeparam>
    /// <param name="payload">The raw payload byte span.</param>
    /// <param name="signatureHeader">The signature HTTP header string.</param>
    /// <param name="publicKey">The public key instance.</param>
    /// <param name="tolerance">The maximum allowable clock drift.</param>
    /// <param name="currentTimestamp">The current reference timestamp.</param>
    /// <param name="expectedSignatureByteLength">The expected binary length of decoded signatures.</param>
    /// <param name="verifier">The verifier instance.</param>
    /// <returns><see langword="true"/> if authentic; otherwise, <see langword="false"/>.</returns>
    protected bool VerifyAsymmetricCore<TKey, TVerifier>(
        ReadOnlySpan<byte> payload,
        string signatureHeader,
        TKey publicKey,
        TimeSpan tolerance,
        UnixTimestamp currentTimestamp,
        int expectedSignatureByteLength,
        TVerifier verifier) where TVerifier : struct, IVerifier<TKey> {

        if(string.IsNullOrWhiteSpace(signatureHeader)) {
            return false;
        }

        ValueList<Range> signatureRanges = new(stackalloc Range[SignatureRangeInitialCapacity]);
        try {
            if(!ValidateVerificationParameters(signatureHeader.AsSpan(), tolerance, currentTimestamp, out UnixTimestamp headerTimestamp, ref signatureRanges)) {
                return false;
            }

            using ValueBuffer<byte> signedBytes = CreateSignedBytes(payload, headerTimestamp, stackalloc byte[SignedBytesStackBufferSize]);
            ReadOnlySpan<byte> dataToVerify = signedBytes.Span;
            ReadOnlySpan<char> headerSpan = signatureHeader.AsSpan();

            using ValueBuffer<byte> decodedSignatureBuffer = new(expectedSignatureByteLength, stackalloc byte[SignatureDecodeStackBufferSize]);
            Span<byte> decodedSignature = decodedSignatureBuffer.Span;

            for(int i = 0; i < signatureRanges.Count; i++) {
                ReadOnlySpan<char> sigCandidate = headerSpan[signatureRanges[i]];
                decodedSignature.Clear();

                if(Convert.TryFromBase64Chars(sigCandidate, decodedSignature, out int bytesWritten) && bytesWritten == expectedSignatureByteLength) {
                    if(verifier.Verify(publicKey, dataToVerify, decodedSignature)) {
                        return true;
                    }
                }
            }

            return false;
        }
        finally {
            signatureRanges.Dispose();
        }
    }
}