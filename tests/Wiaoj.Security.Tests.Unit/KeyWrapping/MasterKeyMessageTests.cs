using Microsoft.Extensions.Configuration;
using System.Security.Cryptography;
using Wiaoj.Primitives;
using Wiaoj.Security.MasterKeyProviders;

namespace Wiaoj.Security.Tests.Unit.KeyWrapping;

/// <summary>
/// How a master key may be written, and what the failures say.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Feature", "KeyWrapping")]
public class MasterKeyMessageTests {
    private static readonly byte[] Key = RandomNumberGenerator.GetBytes(32);

    private static string Base64 => Convert.ToBase64String(Key);

    private static string Base64Url => Convert.ToBase64String(Key).Replace('+', '-').Replace('/', '_').TrimEnd('=');

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task TheEnvironmentTakesEitherEncoding(bool url) {
        // openssl rand -base64 32 produces standard Base64, and this library writes Base64Url. A
        // provider that took one and not the other rejected a key generated exactly as instructed.
        string variable = $"WIAOJ_TEST_KEY_{Guid.NewGuid():N}";
        Environment.SetEnvironmentVariable(variable, url ? Base64Url : Base64);

        try {
            MasterKey masterKey = await new EnvironmentMasterKeyProvider(variable).GetMasterKeyAsync();
            using (masterKey) {
                Assert.True(masterKey.Expose(key => key.SequenceEqual(Key)));
            }
        }
        finally {
            Environment.SetEnvironmentVariable(variable, null);
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ConfigurationTakesEitherEncoding(bool url) {
        // And the same two, because they used to disagree: configuration took Base64 and the
        // environment took Base64Url, so one key stopped working when it moved between them.
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Security:MasterKey"] = url ? Base64Url : Base64 })
            .Build();

        MasterKey masterKey = await new ConfigurationMasterKeyProvider(configuration).GetMasterKeyAsync();
        using (masterKey) {
            Assert.True(masterKey.Expose(key => key.SequenceEqual(Key)));
        }
    }

    [Fact]
    public async Task AMissingKeySaysHowToMakeOne() {
        string variable = $"WIAOJ_TEST_KEY_{Guid.NewGuid():N}";

        InvalidOperationException missing = await Assert.ThrowsAsync<InvalidOperationException>(
            () => new EnvironmentMasterKeyProvider(variable).GetMasterKeyAsync().AsTask());

        Assert.Contains("openssl rand -base64 32", missing.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AKeyOfTheWrongLengthSaysWhichLengthsAreAllowed() {
        string variable = $"WIAOJ_TEST_KEY_{Guid.NewGuid():N}";
        Environment.SetEnvironmentVariable(variable, Convert.ToBase64String(RandomNumberGenerator.GetBytes(20)));

        try {
            InvalidOperationException wrongLength = await Assert.ThrowsAsync<InvalidOperationException>(
                () => new EnvironmentMasterKeyProvider(variable).GetMasterKeyAsync().AsTask());

            Assert.Contains("16, 24, or 32 bytes", wrongLength.Message, StringComparison.Ordinal);
            Assert.Contains("20 bytes", wrongLength.Message, StringComparison.Ordinal);
        }
        finally {
            Environment.SetEnvironmentVariable(variable, null);
        }
    }

    [Fact]
    public void AKeyThatCannotUnwrapTheRingSaysSoRatherThanBlamingCorruption() {
        // "AES-GCM authentication tag mismatch" reads as corruption, and the first thing anyone did
        // was go looking for corruption. It is almost always a host holding a different key.
        MasterKey original = new(Secret.From(RandomNumberGenerator.GetBytes(32)));
        MasterKey different = new(Secret.From(RandomNumberGenerator.GetBytes(32)));

        string wrapped = original.Wrap(RandomNumberGenerator.GetBytes(32));

        CryptographicException refused = Assert.Throws<CryptographicException>(() => different.Unwrap(wrapped));

        Assert.Contains("different key", refused.Message, StringComparison.Ordinal);
        Assert.Contains("Nothing here is corrupt", refused.Message, StringComparison.Ordinal);
    }
}
