using Wiaoj.Webhooks.Signing;

namespace Wiaoj.Webhooks.Tests.Unit.Fakes;

/// <summary>
/// Minimal HmacWebhookSignerBase test double with a fully controllable ComputeHashString,
/// so the shared/base logic (header parsing, timestamp tolerance, constant-time comparison)
/// can be tested in isolation, independent of any real cryptographic algorithm.
/// </summary>
internal sealed class FakeHmacWebhookSigner : HmacWebhookSignerBase {
    private readonly Func<ReadOnlySpan<byte>, ReadOnlySpan<byte>, string> _computeHash;

    public FakeHmacWebhookSigner(
        string schemePrefix = "v1",
        string algorithmName = "fake-hmac",
        string headerName = DefaultHeaderName,
        Func<ReadOnlySpan<byte>, ReadOnlySpan<byte>, string>? computeHash = null)
        : base(headerName) {
        this.SchemePrefix = schemePrefix;
        this.AlgorithmName = algorithmName;
        this._computeHash = computeHash ?? DefaultComputeHash;
    }

    public override string AlgorithmName { get; }
    public override string SchemePrefix { get; }

    protected override string ComputeHashString(ReadOnlySpan<byte> key, ReadOnlySpan<byte> data) =>
        this._computeHash(key, data);

    // Default: real HMAC-SHA256 so Sign/Verify round-trips work out of the box
    // when the test doesn't care about the exact hashing algorithm.
    private static string DefaultComputeHash(ReadOnlySpan<byte> key, ReadOnlySpan<byte> data) {
        byte[] hash = System.Security.Cryptography.HMACSHA256.HashData(key, data);
        return Convert.ToHexStringLower(hash);
    }
}