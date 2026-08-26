using System.Runtime.CompilerServices;
using System.Text;
using Wiaoj.Primitives.Buffers;
using Wiaoj.Primitives.Cryptography;

namespace Wiaoj.Webhooks.Signing.Asymmetric;

/// <summary>
/// Implements asymmetric ECDSA webhook payload signing and verification supporting
/// ES256 (NIST P-256), ES384 (NIST P-384), and ES512 (NIST P-521) with IEEE P1363 signature format.
/// </summary>
public sealed class EcdsaWebhookSigner : AsymmetricWebhookSignerBase {
    private const int SignedBytesStackBufferSize = 256;
    private const int P256SignatureByteLength = 64;
    private const int P384SignatureByteLength = 96;
    private const int P521SignatureByteLength = 132;

    /// <inheritdoc/>
    public override string AlgorithmName => $"ecdsa-{this.Algorithm.Name.ToLowerInvariant()}";

    /// <inheritdoc/>
    public override string SchemePrefix => $"v1_{this.Algorithm.Name.ToLowerInvariant()}";

    /// <summary>
    /// Gets the strongly-typed ECDSA algorithm configuration.
    /// </summary>
    public EcdsaAlgorithm Algorithm { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="EcdsaWebhookSigner"/> class defaulting to ES256 (P-256 with SHA-256).
    /// </summary>
    public EcdsaWebhookSigner() : this(EcdsaAlgorithm.ES256, DefaultHeaderName) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="EcdsaWebhookSigner"/> class with the specified algorithm.
    /// </summary>
    /// <param name="algorithm">The ECDSA algorithm configuration.</param>
    public EcdsaWebhookSigner(EcdsaAlgorithm algorithm) : this(algorithm, DefaultHeaderName) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="EcdsaWebhookSigner"/> class with algorithm and custom header name.
    /// </summary>
    /// <param name="algorithm">The ECDSA algorithm configuration.</param>
    /// <param name="headerName">The custom HTTP header name.</param>
    public EcdsaWebhookSigner(EcdsaAlgorithm algorithm, string headerName) : base(headerName) {
        Preca.ThrowIfNull(algorithm);
        this.Algorithm = algorithm;
    }

    /// <summary>
    /// Signs a payload using an asymmetric <see cref="EcdsaKeyPair"/> (Private Key).
    /// </summary>
    /// <param name="payload">The raw payload byte span.</param>
    /// <param name="keyPair">The ECDSA key pair containing the private key.</param>
    /// <param name="timestamp">The Unix timestamp when the signature is generated.</param>
    /// <returns>A strongly-typed <see cref="WebhookSignature"/> instance.</returns>
    public WebhookSignature Sign(ReadOnlySpan<byte> payload, EcdsaKeyPair keyPair, UnixTimestamp timestamp) {
        Preca.ThrowIfNull(keyPair);

        using ValueBuffer<byte> signedBytes = CreateSignedBytes(payload, timestamp, stackalloc byte[SignedBytesStackBufferSize]);
        byte[] signatureBytes = keyPair.Sign(signedBytes.Span, this.Algorithm);
        string signatureBase64 = Convert.ToBase64String(signatureBytes);

        return new WebhookSignature(timestamp, this.SchemePrefix, signatureBase64);
    }

    /// <summary>
    /// Verifies that a webhook signature is authentic using an asymmetric <see cref="EcdsaPublicKey"/>.
    /// </summary>
    /// <param name="payload">The raw payload byte span.</param>
    /// <param name="signatureHeader">The signature HTTP header value.</param>
    /// <param name="publicKey">The ECDSA public key.</param>
    /// <param name="tolerance">The maximum allowable clock skew drift.</param>
    /// <param name="currentTimestamp">The current reference timestamp.</param>
    /// <returns><see langword="true"/> if authentic; otherwise, <see langword="false"/>.</returns>
    public bool Verify(
        ReadOnlySpan<byte> payload,
        string signatureHeader,
        EcdsaPublicKey publicKey,
        TimeSpan tolerance,
        UnixTimestamp currentTimestamp) {

        if(publicKey is null) {
            throw new ArgumentNullException(nameof(publicKey));
        }

        int signatureByteLength = GetExpectedSignatureLength(publicKey.CurveName);
        EcdsaVerifier verifier = new(this.Algorithm);

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

        using EcdsaKeyPair keyPair = EcdsaKeyPair.FromPem(Encoding.UTF8.GetString(secretKey));
        return Sign(payload, keyPair, timestamp);
    }

    /// <inheritdoc/>
    public override WebhookSignature Sign(ReadOnlySpan<byte> payload, Secret<byte> secretKey, UnixTimestamp timestamp) {
        Preca.ThrowIfDefault(secretKey);

        using ValueBuffer<byte> signedBytes = CreateSignedBytes(payload, timestamp, stackalloc byte[SignedBytesStackBufferSize]);
        EcdsaSignState state = new(signedBytes.Span, this.Algorithm);

        string signatureBase64 = secretKey.Expose(state, static (s, keySpan) => {
            using EcdsaKeyPair keyPair = EcdsaKeyPair.FromPem(Encoding.UTF8.GetString(keySpan));
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
            using EcdsaPublicKey publicKey = PemString.Parse(Encoding.UTF8.GetString(secretKey)).ToEcdsaPublicKey();
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GetExpectedSignatureLength(string curveName) {
        return curveName switch {
            "P-256" => P256SignatureByteLength,
            "P-384" => P384SignatureByteLength,
            "P-521" => P521SignatureByteLength,
            _ => throw new NotSupportedException($"Unsupported curve: {curveName}")
        };
    }

    private readonly struct EcdsaVerifier(EcdsaAlgorithm algorithm) : IVerifier<EcdsaPublicKey> {
        private readonly EcdsaAlgorithm _algorithm = algorithm;

        public bool Verify(EcdsaPublicKey key, ReadOnlySpan<byte> data, ReadOnlySpan<byte> signature) {
            return key.Verify(data, signature, this._algorithm);
        }
    }

    private readonly ref struct EcdsaSignState(ReadOnlySpan<byte> dataToSign, EcdsaAlgorithm algorithm) {
        public readonly ReadOnlySpan<byte> DataToSign = dataToSign;
        public readonly EcdsaAlgorithm Algorithm = algorithm;
    }
}