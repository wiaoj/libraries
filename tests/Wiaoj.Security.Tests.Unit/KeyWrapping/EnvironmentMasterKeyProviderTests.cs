using System.Security.Cryptography;

namespace Wiaoj.Security.Tests.Unit.KeyWrapping;

[Trait("Category", "Unit")]
[Trait("Feature", "KeyWrapping")]
public class EnvironmentMasterKeyProviderTests {

    private const string EnvVarName = "TEST_APP_MASTER_KEY_UNIT";

    [Fact]
    public async Task GetMasterKeyAsync_WhenVariableNotSet_ShouldThrowInvalidOperationException() {
        // Arrange
        Environment.SetEnvironmentVariable(EnvVarName, null);
        EnvironmentMasterKeyProvider provider = new(EnvVarName);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => provider.GetMasterKeyAsync().AsTask());
    }

    [Fact]
    public async Task GetMasterKeyAsync_WhenVariableIsInvalidBase64_ShouldThrowInvalidOperationException() {
        // Arrange
        Environment.SetEnvironmentVariable(EnvVarName, "ThisIsNotBase64!");
        EnvironmentMasterKeyProvider provider = new(EnvVarName);

        try {
            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() => provider.GetMasterKeyAsync().AsTask());
        }
        finally {
            Environment.SetEnvironmentVariable(EnvVarName, null);
        }
    }

    [Theory]
    [InlineData(10)] // Çok kısa
    [InlineData(15)] // AES-128 (16) değil
    [InlineData(33)] // AES-256 (32) değil
    [InlineData(64)] // Çok uzun
    public async Task GetMasterKeyAsync_WhenKeySizeIsInvalid_ShouldThrowInvalidOperationException(int byteCount) {
        // Arrange
        byte[] invalidLengthKey = RandomNumberGenerator.GetBytes(byteCount);
        string base64 = Base64UrlString.FromBytes(invalidLengthKey).Value;
        Environment.SetEnvironmentVariable(EnvVarName, base64);
        EnvironmentMasterKeyProvider provider = new(EnvVarName);

        try {
            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() => provider.GetMasterKeyAsync().AsTask());
        }
        finally {
            Environment.SetEnvironmentVariable(EnvVarName, null);
        }
    }

    [Theory]
    [InlineData(16)] // AES-128
    [InlineData(24)] // AES-192
    [InlineData(32)] // AES-256
    public async Task GetMasterKeyAsync_WithValidKeySizes_ShouldReturnValidMasterKey(int validSize) {
        // Arrange
        byte[] validKey = RandomNumberGenerator.GetBytes(validSize);
        string base64 = Base64UrlString.FromBytes(validKey).Value;
        Environment.SetEnvironmentVariable(EnvVarName, base64);
        EnvironmentMasterKeyProvider provider = new(EnvVarName);

        try {
            // Act
            using MasterKey masterKey = await provider.GetMasterKeyAsync();

            // Assert
            masterKey.Expose(span => {
                Assert.Equal(validSize, span.Length);
                Assert.True(validKey.AsSpan().SequenceEqual(span));
            });
        }
        finally {
            Environment.SetEnvironmentVariable(EnvVarName, null);
        }
    }
}