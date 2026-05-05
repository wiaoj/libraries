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
            keyBytes = Convert.FromBase64String(value);
            return ValueTask.FromResult(new MasterKey(Secret<byte>.From(keyBytes)));
        }
        finally {
            if(keyBytes is not null) CryptographicOperations.ZeroMemory(keyBytes);
        }
    }
}