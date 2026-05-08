using System.Security.Cryptography;
using Wiaoj.Primitives;

namespace Wiaoj.Security;

/// <summary>
/// Master key provider that reads a Base64-encoded key from an environment variable.
/// Suitable for development and staging. For production use a cloud KMS provider.
/// </summary>
public sealed class EnvironmentMasterKeyProvider : IMasterKeyProvider {
    private readonly string _variableName;

    public EnvironmentMasterKeyProvider(string variableName = "APP_MASTER_KEY") {
        ArgumentException.ThrowIfNullOrWhiteSpace(variableName);
        this._variableName = variableName;
    }

    /// <inheritdoc />
    public ValueTask<MasterKey> GetMasterKeyAsync(CancellationToken cancellationToken = default) {
        string? value = Environment.GetEnvironmentVariable(this._variableName);

        if(string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException(
                $"Master key environment variable '{this._variableName}' is not set or empty. " +
                "Set it to a Base64-encoded 32-byte (256-bit) random value.");

        byte[]? keyBytes = null;
        try {
            keyBytes = Base64UrlString.Parse(value).ToBytes();
            if(keyBytes.Length is not (16 or 24 or 32))
                throw new InvalidOperationException(
                    $"Master key must be 16, 24, or 32 bytes (128/192/256-bit AES). " +
                    $"Got {keyBytes.Length} bytes from '{this._variableName}'.");

            return ValueTask.FromResult(new MasterKey(Secret.From(keyBytes)));
        }
        catch(FormatException ex) {
            throw new InvalidOperationException(
                $"Environment variable '{this._variableName}' is not valid Base64.", ex);
        }
        finally {
            // Zero the same array we decoded into — ToBytes() is called only once above,
            // so ZeroMemory operates on the actual key material, not a fresh copy.
            if(keyBytes is not null)
                CryptographicOperations.ZeroMemory(keyBytes);
        }
    }
}