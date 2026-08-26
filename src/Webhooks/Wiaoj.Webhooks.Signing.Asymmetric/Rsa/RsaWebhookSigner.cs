using System.Text;
using Wiaoj.Primitives.Buffers;
using Wiaoj.Primitives.Cryptography;

namespace Wiaoj.Webhooks.Signing.Asymmetric;

/// <summary>
/// Implements asymmetric RSA webhook payload signing and verification supporting RSASSA-PSS (PS256/PS384/PS512)
/// and RSASSA-PKCS1-v1_5 (RS256/RS384/RS512) algorithms.
/// </summary>
public sealed class RsaWebhookSigner : AsymmetricWebhookSignerBase {
    private const int SignedBytesStackBufferSize = 256;

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
    /// Initializes a new instance of the <see cref="RsaWebhookSigner"/> class with the specified algorithm.
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
    /// <param name="payload">The raw payload byte span.</param>
    /// <param name="keyPair">The RSA key pair containing the private key.</param>
    /// <param name="timestamp">The Unix timestamp when the signature is generated.</param>
    /// <returns>A strongly-typed <see cref="WebhookSignature"/> instance.</returns>
    public WebhookSignature Sign(ReadOnlySpan<byte> payload, RsaKeyPair keyPair, UnixTimestamp timestamp) {
        Preca.ThrowIfNull(keyPair);

        using ValueBuffer<byte> signedBytes = CreateSignedBytes(payload, timestamp, stackalloc byte[SignedBytesStackBufferSize]);
        byte[] signatureBytes = keyPair.Sign(signedBytes.Span, this.Algorithm);
        string signatureBase64 = Convert.ToBase64String(signatureBytes);

        return new WebhookSignature(timestamp, this.SchemePrefix, signatureBase64);
    }

    /// <summary>
    /// Verifies that a webhook signature is authentic using an asymmetric <see cref="RsaPublicKey"/>.
    /// </summary>
    /// <param name="payload">The raw payload byte span.</param>
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

        if(publicKey is null) {
            throw new ArgumentNullException(nameof(publicKey));
        }

        int signatureByteLength = publicKey.KeySizeInBits / 8;
        RsaVerifier verifier = new(this.Algorithm);

        return VerifyAsymmetricCore(
            payload,
            signatureHeader,
            publicKey,
            tolerance,
            currentTimestamp,
            signatureByteLength,
            verifier);
    }

    /// <inheritdoc/>
    public override WebhookSignature Sign(ReadOnlySpan<byte> payload, ReadOnlySpan<byte> secretKey, UnixTimestamp timestamp) {
        Preca.ThrowIfEmpty(secretKey);

        using RsaKeyPair keyPair = RsaKeyPair.FromPem(Encoding.UTF8.GetString(secretKey));
        return Sign(payload, keyPair, timestamp);
    }

    /// <inheritdoc/>
    public override WebhookSignature Sign(ReadOnlySpan<byte> payload, Secret<byte> secretKey, UnixTimestamp timestamp) {
        Preca.ThrowIfDefault(secretKey);

        using ValueBuffer<byte> signedBytes = CreateSignedBytes(payload, timestamp, stackalloc byte[SignedBytesStackBufferSize]);
        RsaSignState state = new(signedBytes.Span, this.Algorithm);

        string signatureBase64 = secretKey.Expose(state, static (s, keySpan) => {
            using RsaKeyPair keyPair = RsaKeyPair.FromPem(Encoding.UTF8.GetString(keySpan));
            byte[] sigBytes = keyPair.Sign(s.DataToSign, s.Algorithm);
            return Convert.ToBase64String(sigBytes);
        });

        return new WebhookSignature(timestamp, this.SchemePrefix, signatureBase64);
    }

    /// <inheritdoc/> 
    public override bool Verify(
        ReadOnlySpan<byte> payload,
        string signatureHeader,
        ReadOnlySpan<byte> secretKey,
        TimeSpan tolerance,
        UnixTimestamp currentTimestamp) {

        if(secretKey.IsEmpty) {
            return false;
        }

        try { 
            using RsaPublicKey publicKey = PemString.Parse(Encoding.UTF8.GetString(secretKey)).ToRsaPublicKey();
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

    private readonly struct RsaVerifier(RsaAlgorithm algorithm) : IVerifier<RsaPublicKey> {
        private readonly RsaAlgorithm _algorithm = algorithm;

        public bool Verify(RsaPublicKey key, ReadOnlySpan<byte> data, ReadOnlySpan<byte> signature) {
            return key.Verify(data, signature, this._algorithm);
        }
    }

    private readonly ref struct RsaSignState(ReadOnlySpan<byte> dataToSign, RsaAlgorithm algorithm) {
        public readonly ReadOnlySpan<byte> DataToSign = dataToSign;
        public readonly RsaAlgorithm Algorithm = algorithm;
    }
}