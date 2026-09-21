using System.Security.Cryptography;
using Wiaoj.Primitives;

namespace Wiaoj.Security.Tests.Unit.KeyWrapping;

/// <summary>
/// What the master key errors say, which is the only part of them anyone acts on.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Feature", "KeyWrapping")]
public class MasterKeyMessageTests {
    [Fact]
    public async Task AMissingKeySaysWhichEncodingItWants() {
        // It said "Base64-encoded" and parsed Base64Url, so a key generated exactly as instructed
        // was rejected — by a message that repeated the instruction.
        string variable = $"WIAOJ_TEST_KEY_{Guid.NewGuid():N}";
        EnvironmentMasterKeyProvider provider = new(variable);

        InvalidOperationException missing = await Assert.ThrowsAsync<InvalidOperationException>(
            () => provider.GetMasterKeyAsync().AsTask());

        Assert.Contains("Base64Url", missing.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("Base64-encoded", missing.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AKeyInPlainBase64SaysWhatIsDifferentAboutBase64Url() {
        string variable = $"WIAOJ_TEST_KEY_{Guid.NewGuid():N}";
        // '+' and '/' are what Base64 produces and Base64Url does not accept.
        Environment.SetEnvironmentVariable(variable, "++//++//++//++//++//++//++//++//++//++//++//");

        try {
            EnvironmentMasterKeyProvider provider = new(variable);

            InvalidOperationException malformed = await Assert.ThrowsAsync<InvalidOperationException>(
                () => provider.GetMasterKeyAsync().AsTask());

            Assert.Contains("Base64Url", malformed.Message, StringComparison.Ordinal);
        }
        finally {
            Environment.SetEnvironmentVariable(variable, null);
        }
    }

    [Fact]
    public void AKeyThatCannotUnwrapTheRingSaysSoRatherThanBlamingCorruption() {
        // "AES-GCM authentication tag mismatch" reads as corruption, and the first thing anyone did
        // was look for corruption. It is almost always a host holding a different key.
        MasterKey original = new(Secret.From(RandomNumberGenerator.GetBytes(32)));
        MasterKey different = new(Secret.From(RandomNumberGenerator.GetBytes(32)));

        string wrapped = original.Wrap(RandomNumberGenerator.GetBytes(32));

        CryptographicException refused = Assert.Throws<CryptographicException>(() => different.Unwrap(wrapped));

        Assert.Contains("different key", refused.Message, StringComparison.Ordinal);
        Assert.Contains("Nothing here is corrupt", refused.Message, StringComparison.Ordinal);
    }
}
