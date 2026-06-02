using System.Security.Cryptography;
using Wiaoj.Primitives;

namespace Wiaoj.Security.MasterKeyProviders;

/// <summary>
/// Reads the previous master key from an environment variable
/// (default: <c>APP_MASTER_KEY_PREVIOUS</c>). Returns <see langword="null"/>
/// when the variable is unset — the rewrap service treats this as "no rotation pending".
/// </summary>
/// <remarks>
/// Suitable for dev/staging. For production use a KMS-aware provider that exposes
/// historical key versions (Azure Key Vault, AWS KMS, HashiCorp Vault).
/// </remarks>
public sealed class EnvironmentPreviousMasterKeyProvider : IPreviousMasterKeyProvider {
    private readonly string _variableName;

    public EnvironmentPreviousMasterKeyProvider(string variableName = "APP_MASTER_KEY_PREVIOUS") {
        ArgumentException.ThrowIfNullOrWhiteSpace(variableName);
        this._variableName = variableName;
    }

    /// <inheritdoc />
    public ValueTask<MasterKey?> GetPreviousMasterKeyAsync(CancellationToken cancellationToken = default) {
        string? value = Environment.GetEnvironmentVariable(this._variableName);
        if(string.IsNullOrWhiteSpace(value))
            return ValueTask.FromResult<MasterKey?>(null);

        byte[]? keyBytes = null;
        try {
            keyBytes = Base64UrlString.Parse(value).ToBytes();
            if(keyBytes.Length is not (16 or 24 or 32))
                throw new InvalidOperationException(
                    $"Previous master key must be 16, 24, or 32 bytes (128/192/256-bit AES). " +
                    $"Got {keyBytes.Length} bytes from '{this._variableName}'.");

            return ValueTask.FromResult<MasterKey?>(new MasterKey(Secret.From(keyBytes)));
        }
        catch(FormatException ex) {
            throw new InvalidOperationException(
                $"Environment variable '{this._variableName}' is not valid Base64.", ex);
        }
        finally {
            if(keyBytes is not null)
                CryptographicOperations.ZeroMemory(keyBytes);
        }
    }
}
