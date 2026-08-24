namespace Wiaoj.Webhooks;

/// <summary>
/// Defines the contract for signing outbound webhook payloads and verifying inbound webhook signatures.
/// </summary>
/// <remarks>
/// Implementations must incorporate the generation timestamp into the signature input to prevent replay attacks,
/// and perform verification using constant-time comparisons to prevent timing-based side-channel attacks.
/// </remarks>
public interface IWebhookSigner {

    /// <summary>
    /// Gets the unique algorithm identifier (e.g., "hmac-sha256", "hmac-sha512").
    /// </summary>
    string AlgorithmName { get; }

    /// <summary>
    /// Gets the HTTP header name used to transport the signature (e.g., "Wiaoj-Signature").
    /// </summary>
    string HeaderName { get; }

    /// <summary>
    /// Gets the signature version scheme prefix (e.g., "v1" for HMAC-SHA256, "v2" for HMAC-SHA512).
    /// </summary>
    string SchemePrefix { get; }

    /// <summary>
    /// Computes a signature for the specified payload and timestamp using raw secret key bytes.
    /// </summary>
    /// <param name="payload">The raw UTF-8 payload bytes.</param>
    /// <param name="secretKey">The secret key bytes.</param>
    /// <param name="timestamp">The Unix timestamp when the signature is created.</param>
    /// <returns>A <see cref="WebhookSignature"/> instance.</returns>
    WebhookSignature Sign(ReadOnlySpan<byte> payload, ReadOnlySpan<byte> secretKey, UnixTimestamp timestamp);

    /// <summary>
    /// Computes a signature for the specified payload and timestamp using a secure <see cref="Secret{T}"/> key.
    /// </summary>
    /// <param name="payload">The raw UTF-8 payload bytes.</param>
    /// <param name="secretKey">The sensitive secret key stored in protected unmanaged memory.</param>
    /// <param name="timestamp">The Unix timestamp when the signature is created.</param>
    /// <returns>A <see cref="WebhookSignature"/> instance.</returns>
    WebhookSignature Sign(ReadOnlySpan<byte> payload, Secret<byte> secretKey, UnixTimestamp timestamp);

    /// <summary>
    /// Verifies that a webhook signature header is authentic for the given payload and secret key within the specified clock tolerance.
    /// </summary>
    /// <param name="payload">The raw UTF-8 payload bytes received.</param>
    /// <param name="signatureHeader">The value of the signature header (e.g., "t=1724190000,v1=4f53c...").</param>
    /// <param name="secretKey">The secret key bytes.</param>
    /// <param name="tolerance">The maximum allowable clock drift/skew between current time and signature timestamp.</param>
    /// <param name="currentTimestamp">The reference current timestamp to compare against.</param>
    /// <returns><see langword="true"/> if the signature is authentic and within clock tolerance; otherwise, <see langword="false"/>.</returns>
    bool Verify(ReadOnlySpan<byte> payload, string signatureHeader, ReadOnlySpan<byte> secretKey, TimeSpan tolerance, UnixTimestamp currentTimestamp);

    /// <summary>
    /// Verifies that a webhook signature header is authentic for the given payload and secret key using the current system time.
    /// </summary>
    /// <param name="payload">The raw UTF-8 payload bytes received.</param>
    /// <param name="signatureHeader">The value of the signature header.</param>
    /// <param name="secretKey">The secret key bytes.</param>
    /// <param name="tolerance">The maximum allowable clock drift.</param>
    /// <returns><see langword="true"/> if authentic; otherwise, <see langword="false"/>.</returns>
    bool Verify(ReadOnlySpan<byte> payload, string signatureHeader, ReadOnlySpan<byte> secretKey, TimeSpan tolerance) =>
        Verify(payload, signatureHeader, secretKey, tolerance, UnixTimestamp.Now);

    /// <summary>
    /// Verifies that a webhook signature header is authentic using a secure <see cref="Secret{T}"/> key within the specified clock tolerance.
    /// </summary>
    /// <param name="payload">The raw UTF-8 payload bytes received.</param>
    /// <param name="signatureHeader">The value of the signature header.</param>
    /// <param name="secretKey">The sensitive secret key.</param>
    /// <param name="tolerance">The maximum allowable clock drift.</param>
    /// <param name="currentTimestamp">The reference current timestamp to compare against.</param>
    /// <returns><see langword="true"/> if authentic; otherwise, <see langword="false"/>.</returns>
    bool Verify(ReadOnlySpan<byte> payload, string signatureHeader, Secret<byte> secretKey, TimeSpan tolerance, UnixTimestamp currentTimestamp);

    /// <summary>
    /// Verifies that a webhook signature header is authentic using a secure <see cref="Secret{T}"/> key and the current system time.
    /// </summary>
    /// <param name="payload">The raw UTF-8 payload bytes received.</param>
    /// <param name="signatureHeader">The value of the signature header.</param>
    /// <param name="secretKey">The sensitive secret key.</param>
    /// <param name="tolerance">The maximum allowable clock drift.</param>
    /// <returns><see langword="true"/> if authentic; otherwise, <see langword="false"/>.</returns>
    bool Verify(ReadOnlySpan<byte> payload, string signatureHeader, Secret<byte> secretKey, TimeSpan tolerance) =>
        Verify(payload, signatureHeader, secretKey, tolerance, UnixTimestamp.Now);
}