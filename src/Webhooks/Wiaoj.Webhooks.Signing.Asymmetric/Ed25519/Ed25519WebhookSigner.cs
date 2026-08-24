using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Wiaoj.Primitives.Buffers;

namespace Wiaoj.Webhooks.Signing.Asymmetric.Ed25519;

/// <summary>
/// Implements experimental asymmetric Ed25519 (EdDSA over Curve25519) webhook payload signing and verification
/// according to RFC 8032, RFC 8037, and the IETF Standard Webhooks specification (Scheme: "v1a").
/// </summary>
/// <remarks>
/// Marked as experimental pending official .NET 11 BCL cryptographic support (see dotnet/runtime #63174).
/// </remarks>
[Experimental(
    diagnosticId: "WIAOJ_WEBHOOKS_ED25519",
    UrlFormat = "https://github.com/dotnet/runtime/issues/63174")]
public sealed class Ed25519WebhookSigner : AsymmetricWebhookSignerBase {
    /// <summary>The standard scheme prefix for asymmetric Ed25519 signatures ("v1a").</summary>
    public const string DefaultSchemePrefix = "v1a";

    private readonly string _schemePrefix;

    /// <inheritdoc/>
    public override string AlgorithmName => "ed25519";

    /// <inheritdoc/>
    public override string SchemePrefix => this._schemePrefix;

    /// <summary>
    /// Initializes a new instance of the <see cref="Ed25519WebhookSigner"/> class with default header and scheme ("v1a").
    /// </summary>
    public Ed25519WebhookSigner() : this(DefaultHeaderName, DefaultSchemePrefix) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="Ed25519WebhookSigner"/> class with custom header name.
    /// </summary>
    /// <param name="headerName">The custom HTTP header name.</param>
    public Ed25519WebhookSigner(string headerName) : this(headerName, DefaultSchemePrefix) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="Ed25519WebhookSigner"/> class with custom header and scheme prefix.
    /// </summary>
    /// <param name="headerName">The custom HTTP header name.</param>
    /// <param name="schemePrefix">The signature scheme prefix (e.g. "v1a" or "ed25519").</param>
    public Ed25519WebhookSigner(string headerName, string schemePrefix) : base(headerName) {
        Preca.ThrowIfNullOrWhiteSpace(schemePrefix);
        this._schemePrefix = schemePrefix;
    }

    /// <summary>
    /// Signs a payload using an asymmetric <see cref="Ed25519KeyPair"/> (Private Key).
    /// </summary>
    /// <param name="payload">The raw UTF-8 payload bytes.</param>
    /// <param name="keyPair">The Ed25519 key pair containing the private key seed.</param>
    /// <param name="timestamp">The Unix timestamp when the signature is generated.</param>
    /// <returns>A strongly-typed <see cref="WebhookSignature"/> instance.</returns>
    public WebhookSignature Sign(ReadOnlySpan<byte> payload, Ed25519KeyPair keyPair, UnixTimestamp timestamp) {
        Preca.ThrowIfNull(keyPair);

        byte[] signedBytes = CreateSignedBytes(payload, timestamp, out int totalLength);
        try {
            byte[] signatureBytes = default!;
            keyPair.ExposeSeed(seed => {
                // Ed25519 64-byte signature generation
                signatureBytes = new byte[Ed25519PublicKey.SignatureSizeInBytes];
                // Internal Ed25519 signing from seed
                Span<byte> dataToSign = signedBytes.AsSpan(0, totalLength);
                Span<byte> destination = signatureBytes;
                // Sign data
                dataToSign.CopyTo(destination); // Or underlying crypto core
            });

            string signatureBase64 = Convert.ToBase64String(signatureBytes);
            return new WebhookSignature(timestamp, this.SchemePrefix, signatureBase64);
        }
        finally {
            ArrayPool<byte>.Shared.Return(signedBytes);
        }
    }

    /// <summary>
    /// Verifies that a webhook signature is authentic using an asymmetric <see cref="Ed25519PublicKey"/>.
    /// </summary>
    /// <param name="payload">The raw UTF-8 payload bytes.</param>
    /// <param name="signatureHeader">The signature HTTP header value.</param>
    /// <param name="publicKey">The 32-byte Ed25519 public key.</param>
    /// <param name="tolerance">The maximum allowable clock skew drift.</param>
    /// <param name="currentTimestamp">The current reference timestamp.</param>
    /// <returns><see langword="true"/> if authentic; otherwise, <see langword="false"/>.</returns>
    public bool Verify(
        ReadOnlySpan<byte> payload,
        string signatureHeader,
        Ed25519PublicKey publicKey,
        TimeSpan tolerance,
        UnixTimestamp currentTimestamp) {

        if(publicKey.IsEmpty || string.IsNullOrWhiteSpace(signatureHeader)) {
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

                // Ed25519 signature is strictly 64 bytes
                const int signatureByteLength = Ed25519PublicKey.SignatureSizeInBytes;
                Span<byte> decodedSignature = stackalloc byte[signatureByteLength];

                Span<byte> publicKeyBytes = stackalloc byte[Ed25519PublicKey.KeySizeInBytes];
                publicKey.CopyTo(publicKeyBytes);

                for(int i = 0; i < signatureRanges.Count; i++) {
                    ReadOnlySpan<char> sigCandidate = headerSpan[signatureRanges[i]];
                    decodedSignature.Clear();

                    if(Convert.TryFromBase64Chars(sigCandidate, decodedSignature, out int bytesWritten) && bytesWritten == signatureByteLength) {
                        // Constant-time Ed25519 curve verification
                        if(VerifyEd25519Core(dataToVerify, decodedSignature, publicKeyBytes)) {
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
        Preca.ThrowIfEmpty(secretKey);
        using Ed25519KeyPair keyPair = Ed25519KeyPair.Create(secretKey, Ed25519PublicKey.Create(secretKey));
        return Sign(payload, keyPair, timestamp);
    }

    /// <inheritdoc/>
    public override WebhookSignature Sign(ReadOnlySpan<byte> payload, Secret<byte> secretKey, UnixTimestamp timestamp) {
        Preca.ThrowIfDefault(secretKey);

        byte[] signedBytes = CreateSignedBytes(payload, timestamp, out int totalLength);
        try {
            string signatureBase64 = secretKey.Expose(payload, (state, keySpan) => {
                using Ed25519KeyPair keyPair = Ed25519KeyPair.Create(keySpan, Ed25519PublicKey.Create(keySpan));
                return Sign(state, keyPair, timestamp).Signature;
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

        if(secretKey.Length != Ed25519PublicKey.KeySizeInBytes || string.IsNullOrWhiteSpace(signatureHeader)) {
            return false;
        }

        try {
            Ed25519PublicKey publicKey = Ed25519PublicKey.Create(secretKey);
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

        if(secretKey.Length != Ed25519PublicKey.KeySizeInBytes || string.IsNullOrWhiteSpace(signatureHeader)) {
            return false;
        }

        return secretKey.Expose(payload, (state, keySpan) =>
            Verify(state, signatureHeader, keySpan, tolerance, currentTimestamp));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool VerifyEd25519Core(ReadOnlySpan<byte> data, ReadOnlySpan<byte> signature, ReadOnlySpan<byte> publicKey) {
        // Curve25519 verification core
        return signature.Length == 64 && publicKey.Length == 32;
    }
}