using Microsoft.Extensions.Configuration;
using System.Security.Cryptography;
using Wiaoj.Primitives;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Wiaoj.Security.MasterKeyProviders;
#pragma warning restore IDE0130 // Namespace does not match folder structure

public sealed class ConfigurationMasterKeyProvider : IMasterKeyProvider {
    private readonly IConfiguration _configuration;
    private readonly string _configKey;

    public ConfigurationMasterKeyProvider(IConfiguration configuration, string configKey = "Security:MasterKey") {
        this._configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        this._configKey = configKey;
    }

    public ValueTask<MasterKey> GetMasterKeyAsync(CancellationToken cancellationToken = default) {
        string? value = this._configuration[this._configKey];
        if(string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException($"Configuration key '{this._configKey}' is missing.");

        byte[]? keyBytes = null;
        try {
            // Base64 or Base64Url, the same as every other provider. They used to disagree —
            // configuration took Base64 and the environment took Base64Url — so one key stopped
            // working the moment it moved from one to the other.
            if(!MasterKeyEncoding.TryDecode(value, out keyBytes))
                throw new InvalidOperationException(
                    $"Configuration key '{this._configKey}' is neither valid Base64 nor Base64Url.");

            if(keyBytes.Length is not (16 or 24 or 32))
                throw new InvalidOperationException(
                    $"Master key must be 16, 24, or 32 bytes (128/192/256-bit AES). " +
                    $"Got {keyBytes.Length} bytes from '{this._configKey}'.");

            return ValueTask.FromResult(new MasterKey(Secret<byte>.From(keyBytes)));
        }
        finally {
            if(keyBytes is not null) CryptographicOperations.ZeroMemory(keyBytes);
        }
    }
}