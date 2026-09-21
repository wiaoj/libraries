using System.Security.Cryptography;
using Wiaoj.Primitives;

namespace Wiaoj.Security;

/// <summary>
/// Master key provider that reads a key from an environment variable, written as Base64 or Base64Url.
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
                "Set it to 16, 24 or 32 random bytes, Base64 or Base64Url — either is read. " +
                "For example: openssl rand -base64 32");

        byte[]? keyBytes = null;
        try {
            if(!MasterKeyEncoding.TryDecode(value, out keyBytes))
                throw new InvalidOperationException(
                    $"Environment variable '{this._variableName}' is neither valid Base64 nor Base64Url.");

            if(keyBytes.Length is not (16 or 24 or 32))
                throw new InvalidOperationException(
                    $"Master key must be 16, 24, or 32 bytes (128/192/256-bit AES). " +
                    $"Got {keyBytes.Length} bytes from '{this._variableName}'.");

            return ValueTask.FromResult(new MasterKey(Secret.From(keyBytes)));
        }
        finally {
            // Zero the same array that was decoded into, so the key material itself is cleared
            // rather than a copy of it.
            if(keyBytes is not null)
                CryptographicOperations.ZeroMemory(keyBytes);
        }
    }
}
