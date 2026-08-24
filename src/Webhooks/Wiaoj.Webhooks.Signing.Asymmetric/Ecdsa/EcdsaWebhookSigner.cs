using System.Buffers;
using System.Runtime.CompilerServices;
using Wiaoj.Primitives.Buffers;
using Wiaoj.Primitives.Cryptography;

namespace Wiaoj.Webhooks.Signing.Asymmetric;

/// <summary>
/// Implements asymmetric ECDSA webhook payload signing and verification supporting
/// ES256 (NIST P-256), ES384 (NIST P-384), and ES512 (NIST P-521) with IEEE P1363 signature format.
/// </summary>
public sealed class EcdsaWebhookSigner : AsymmetricWebhookSignerBase {
    private readonly EcdsaAlgorithm _algorithm;

    /// <inheritdoc/>
    public override string AlgorithmName => $"ecdsa-{this._algorithm.Name.ToLowerInvariant()}";

    /// <inheritdoc/>
    public override string SchemePrefix => $"v1_{this._algorithm.Name.ToLowerInvariant()}";

    /// <summary>
    /// Gets the strongly-typed ECDSA algorithm configuration.
    /// </summary>
    public EcdsaAlgorithm Algorithm => this._algorithm;

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
        this._algorithm = algorithm;
    }

    /// <summary>
    /// Signs a payload using an asymmetric <see cref="EcdsaKeyPair"/> (Private Key).
    /// </summary>
    /// <param name="payload">The raw UTF-8 payload bytes.</param>
    /// <param name="keyPair">The ECDSA key pair containing the private key.</param>
    /// <param name="timestamp">The Unix timestamp when the signature is generated.</param>
    /// <returns>A strongly-typed <see cref="WebhookSignature"/> instance.</returns>
    public WebhookSignature Sign(ReadOnlySpan<byte> payload, EcdsaKeyPair keyPair, UnixTimestamp timestamp) {
        Preca.ThrowIfNull(keyPair);

        byte[] signedBytes = CreateSignedBytes(payload, timestamp, out int totalLength);
        try {
            byte[] signatureBytes = keyPair.Sign(signedBytes.AsSpan(0, totalLength), this._algorithm);
            string signatureBase64 = Convert.ToBase64String(signatureBytes);

            return new WebhookSignature(timestamp, this.SchemePrefix, signatureBase64);
        }
        finally {
            ArrayPool<byte>.Shared.Return(signedBytes);
        }
    }

    /// <summary>
    /// Verifies that a webhook signature is authentic using an asymmetric <see cref="EcdsaPublicKey"/>.
    /// </summary>
    /// <param name="payload">The raw UTF-8 payload bytes.</param>
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

                // IEEE P1363 signature size: P-256 = 64B, P-384 = 96B, P-521 = 132B
                int signatureByteLength = GetExpectedSignatureLength(publicKey.CurveName);
                Span<byte> decodedSignature = stackalloc byte[signatureByteLength];

                for(int i = 0; i < signatureRanges.Count; i++) {
                    ReadOnlySpan<char> sigCandidate = headerSpan[signatureRanges[i]];
                    decodedSignature.Clear();

                    if(Convert.TryFromBase64Chars(sigCandidate, decodedSignature, out int bytesWritten) && bytesWritten == signatureByteLength) {
                        if(publicKey.Verify(dataToVerify, decodedSignature, this._algorithm)) {
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
        using EcdsaKeyPair keyPair = EcdsaKeyPair.FromPem(System.Text.Encoding.UTF8.GetString(secretKey));
        return Sign(payload, keyPair, timestamp);
    }

    /// <inheritdoc/>
    public override WebhookSignature Sign(ReadOnlySpan<byte> payload, Secret<byte> secretKey, UnixTimestamp timestamp) {
        Preca.ThrowIfDefault(secretKey);

        byte[] signedBytes = CreateSignedBytes(payload, timestamp, out int totalLength);
        try {
            string signatureBase64 = secretKey.Expose(keySpan => {
                using EcdsaKeyPair keyPair = EcdsaKeyPair.FromPem(System.Text.Encoding.UTF8.GetString(keySpan));
                byte[] sigBytes = keyPair.Sign(signedBytes.AsSpan(0, totalLength), this._algorithm);
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
            using EcdsaPublicKey publicKey = PemString.Parse(System.Text.Encoding.UTF8.GetString(secretKey)).ToEcdsaPublicKey();
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
    private static int GetExpectedSignatureLength(string curveName) => curveName switch {
        "P-256" => 64,
        "P-384" => 96,
        "P-521" => 132,
        _ => 64
    };
}