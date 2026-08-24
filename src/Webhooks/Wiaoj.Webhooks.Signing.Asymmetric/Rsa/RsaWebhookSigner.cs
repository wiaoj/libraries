using System.Buffers;
using Wiaoj.Primitives.Buffers;

namespace Wiaoj.Webhooks.Signing.Asymmetric.Rsa;

/// <summary>
/// Implements asymmetric RSA webhook payload signing and verification supporting RSASSA-PSS (PS256/PS384/PS512)
/// and RSASSA-PKCS1-v1_5 (RS256/RS384/RS512) algorithms.
/// </summary>
public sealed class RsaWebhookSigner : AsymmetricWebhookSignerBase {

    /// <inheritdoc/>
    public override string AlgorithmName => $"rsa-{this.Algorithm.Name.ToLowerInvariant()}";

    /// <inheritdoc/>
    public override string SchemePrefix => $"v1_{this.Algorithm.Name.ToLowerInvariant()}";

    /// <summary>
    /// Gets the strongly-typed RSA algorithm configuration.
    /// </summary>
    public RsaAlgorithm Algorithm { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="RsaWebhookSigner"/> class defaulting to PS256 (RSASSA-PSS with SHA-256).
    /// </summary>
    public RsaWebhookSigner() : this(RsaAlgorithm.PS256, DefaultHeaderName) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="RsaWebhookSigner"/> class with the specified algorithm and default header.
    /// </summary>
    /// <param name="algorithm">The RSA algorithm configuration.</param>
    public RsaWebhookSigner(RsaAlgorithm algorithm) : this(algorithm, DefaultHeaderName) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="RsaWebhookSigner"/> class with algorithm and custom header name.
    /// </summary>
    /// <param name="algorithm">The RSA algorithm configuration.</param>
    /// <param name="headerName">The custom HTTP header name.</param>
    public RsaWebhookSigner(RsaAlgorithm algorithm, string headerName) : base(headerName) {
        Preca.ThrowIfNull(algorithm);
        this.Algorithm = algorithm;
    }

    /// <summary>
    /// Signs a payload using an asymmetric <see cref="RsaKeyPair"/> (Private Key).
    /// </summary>
    /// <param name="payload">The raw UTF-8 payload bytes.</param>
    /// <param name="keyPair">The RSA key pair containing the private key.</param>
    /// <param name="timestamp">The Unix timestamp when the signature is generated.</param>
    /// <returns>A strongly-typed <see cref="WebhookSignature"/> instance.</returns>
    public WebhookSignature Sign(ReadOnlySpan<byte> payload, RsaKeyPair keyPair, UnixTimestamp timestamp) {
        Preca.ThrowIfNull(keyPair);

        byte[] signedBytes = CreateSignedBytes(payload, timestamp, out int totalLength);
        try {
            byte[] signatureBytes = keyPair.Sign(signedBytes.AsSpan(0, totalLength), this.Algorithm);
            string signatureBase64 = Convert.ToBase64String(signatureBytes);

            return new WebhookSignature(timestamp, this.SchemePrefix, signatureBase64);
        }
        finally {
            ArrayPool<byte>.Shared.Return(signedBytes);
        }
    }

    /// <summary>
    /// Verifies that a webhook signature is authentic using an asymmetric <see cref="RsaPublicKey"/>.
    /// </summary>
    /// <param name="payload">The raw UTF-8 payload bytes.</param>
    /// <param name="signatureHeader">The signature HTTP header value.</param>
    /// <param name="publicKey">The RSA public key.</param>
    /// <param name="tolerance">The maximum allowable clock skew drift.</param>
    /// <param name="currentTimestamp">The current reference timestamp.</param>
    /// <returns><see langword="true"/> if authentic; otherwise, <see langword="false"/>.</returns>
    public bool Verify(
        ReadOnlySpan<byte> payload,
        string signatureHeader,
        RsaPublicKey publicKey,
        TimeSpan tolerance,
        UnixTimestamp currentTimestamp) {

        Preca.ThrowIfNull(publicKey);

        if(string.IsNullOrWhiteSpace(signatureHeader)) {
            return false;
        }

        Span<Range> initialRangeBuffer = stackalloc Range[4];
        ValueList<Range> signatureRanges = new(initialRangeBuffer);
        try {
            if(!ValidateAndExtractSignatures(signatureHeader.AsSpan(), tolerance, currentTimestamp, out UnixTimestamp headerTimestamp, ref signatureRanges)) {
                return false;
            }

            byte[] signedBytes = CreateSignedBytes(payload, headerTimestamp, out int totalLength);
            try {
                ReadOnlySpan<byte> dataToVerify = signedBytes.AsSpan(0, totalLength);
                ReadOnlySpan<char> headerSpan = signatureHeader.AsSpan();

                int signatureByteLength = publicKey.KeySizeInBits / 8;
                Span<byte> decodedSignature = stackalloc byte[signatureByteLength];

                for(int i = 0; i < signatureRanges.Count; i++) {
                    ReadOnlySpan<char> sigCandidate = headerSpan[signatureRanges[i]];
                    decodedSignature.Clear();

                    if(Convert.TryFromBase64Chars(sigCandidate, decodedSignature, out int bytesWritten) && bytesWritten == signatureByteLength) {
                        if(publicKey.Verify(dataToVerify, decodedSignature, this.Algorithm)) {
                            return true;
                        }
                    }
                }

                return false;
            }
            finally {
                ArrayPool<byte>.Shared.Return(signedBytes);
            }
        }
        finally {
            signatureRanges.Dispose();
        }
    }

    /// <inheritdoc/>
    public override WebhookSignature Sign(ReadOnlySpan<byte> payload, ReadOnlySpan<byte> secretKey, UnixTimestamp timestamp) {
        using RsaKeyPair keyPair = RsaKeyPair.FromPem(System.Text.Encoding.UTF8.GetString(secretKey));
        return Sign(payload, keyPair, timestamp);
    }

    /// <inheritdoc/>
    public override WebhookSignature Sign(ReadOnlySpan<byte> payload, Secret<byte> secretKey, UnixTimestamp timestamp) {
        Preca.ThrowIfDefault(secretKey);

        byte[] signedBytes = CreateSignedBytes(payload, timestamp, out int totalLength);
        try {
            string signatureBase64 = secretKey.Expose(keySpan => {
                using RsaKeyPair keyPair = RsaKeyPair.FromPem(System.Text.Encoding.UTF8.GetString(keySpan));
                byte[] sigBytes = keyPair.Sign(signedBytes.AsSpan(0, totalLength), this.Algorithm);
                return Convert.ToBase64String(sigBytes);
            });

            return new WebhookSignature(timestamp, this.SchemePrefix, signatureBase64);
        }
        finally {
            ArrayPool<byte>.Shared.Return(signedBytes);
        }
    }

    /// <inheritdoc/>
    public override bool Verify(
        ReadOnlySpan<byte> payload,
        string signatureHeader,
        ReadOnlySpan<byte> secretKey,
        TimeSpan tolerance,
        UnixTimestamp currentTimestamp) {

        if(secretKey.IsEmpty) return false;

        try {
            using RsaPublicKey publicKey = RsaPublicKey.Create(secretKey, [0x01, 0x00, 0x01]);
            return Verify(payload, signatureHeader, publicKey, tolerance, currentTimestamp);
        }
        catch {
            return false;
        }
    }

    /// <inheritdoc/>
    public override bool Verify(
        ReadOnlySpan<byte> payload,
        string signatureHeader,
        Secret<byte> secretKey,
        TimeSpan tolerance,
        UnixTimestamp currentTimestamp) {

        if(secretKey.Length == 0 || string.IsNullOrWhiteSpace(signatureHeader)) {
            return false;
        }

        return secretKey.Expose(payload, (state, keySpan) =>
            Verify(state, signatureHeader, keySpan, tolerance, currentTimestamp));
    }
}