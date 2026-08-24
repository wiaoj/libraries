using System.Security.Cryptography;
using System.Text;
using Wiaoj.Preconditions;
using Wiaoj.Primitives;
using Wiaoj.Primitives.Cryptography.Hashing;

namespace Wiaoj.Webhooks.Tests.Integration;

/// <summary>
/// Implements GitHub's standard HMAC-SHA256 signature verification (Header: X-Hub-Signature-256, Format: sha256={hash}).
/// </summary>
public sealed class GitHubWebhookSigner : IWebhookSigner {
    public const string DefaultHeaderName = "X-Hub-Signature-256";
    public const string SchemePrefix = "sha256";

    public string AlgorithmName => "github-hmac-sha256";
    public string HeaderName => DefaultHeaderName;
    string IWebhookSigner.SchemePrefix => SchemePrefix;

    public WebhookSignature Sign(ReadOnlySpan<byte> payload, ReadOnlySpan<byte> secretKey, UnixTimestamp timestamp) {
        HmacSha256Hash hash = HmacSha256Hash.Compute(secretKey, payload);
        return new WebhookSignature(timestamp, SchemePrefix, hash.ToHexStringLower().ToString());
    }

    public WebhookSignature Sign(ReadOnlySpan<byte> payload, Secret<byte> secretKey, UnixTimestamp timestamp) {
        Preca.ThrowIfDefault(secretKey);
        string hash = secretKey.Expose(payload, (state, keySpan) => HmacSha256Hash.Compute(keySpan, state).ToHexStringLower().ToString());
        return new WebhookSignature(timestamp, SchemePrefix, hash);
    }

    public bool Verify(
        ReadOnlySpan<byte> payload,
        string signatureHeader,
        ReadOnlySpan<byte> secretKey,
        TimeSpan tolerance,
        UnixTimestamp currentTimestamp) {

        if(string.IsNullOrWhiteSpace(signatureHeader) || secretKey.IsEmpty) {
            return false;
        }

        ReadOnlySpan<char> span = signatureHeader.AsSpan().Trim();
        const string prefix = "sha256=";

        if(!span.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) {
            return false;
        }

        ReadOnlySpan<char> candidateHashHex = span[prefix.Length..].Trim();
        if(candidateHashHex.Length != 64) {
            return false;
        }

        // Compute expected HMAC-SHA256
        HmacSha256Hash expectedHash = HmacSha256Hash.Compute(secretKey, payload);
        Span<char> expectedHex = stackalloc char[64];
        expectedHash.TryFormat(expectedHex, out _, "x");

        // Constant-time string comparison
        Span<byte> expectedBytes = stackalloc byte[64];
        Span<byte> candidateBytes = stackalloc byte[64];
        Encoding.UTF8.GetBytes(expectedHex, expectedBytes);
        Encoding.UTF8.GetBytes(candidateHashHex, candidateBytes);

        return CryptographicOperations.FixedTimeEquals(expectedBytes, candidateBytes);
    }

    public bool Verify(
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