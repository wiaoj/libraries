using Microsoft.Extensions.Configuration;
using System.Security.Cryptography;
using Wiaoj.Security.MasterKeyProviders;

namespace Wiaoj.Security.Tests.Unit.KeyWrapping;

[Trait("Category", "Unit")]
[Trait("Feature", "KeyWrapping")]
public class ConfigurationMasterKeyProviderTests {

    [Fact]
    public async Task GetMasterKeyAsync_WhenConfigKeyMissing_ShouldThrowInvalidOperationException() {
        // Arrange
        IConfiguration config = new ConfigurationBuilder().Build();
        ConfigurationMasterKeyProvider provider = new(config, "Missing:Key");

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => provider.GetMasterKeyAsync().AsTask());
    }

    [Fact]
    public async Task GetMasterKeyAsync_WithValidBase64Key_ShouldReturnMasterKey() {
        // Arrange
        byte[] validKey = RandomNumberGenerator.GetBytes(32);
        string base64 = Convert.ToBase64String(validKey);

        Dictionary<string, string?> inMemory = new() {
            ["Security:MasterKey"] = base64
        };

        IConfiguration config = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemory)
            .Build();

        ConfigurationMasterKeyProvider provider = new(config, "Security:MasterKey");

        // Act
        using MasterKey masterKey = await provider.GetMasterKeyAsync();

        // Assert
        masterKey.Expose(span => {
            Assert.Equal(32, span.Length);
            Assert.True(validKey.AsSpan().SequenceEqual(span));
        });
    }
}