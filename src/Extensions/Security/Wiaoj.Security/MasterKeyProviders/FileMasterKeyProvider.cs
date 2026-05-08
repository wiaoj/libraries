using System.Security.Cryptography;
using Wiaoj.Primitives;

namespace Wiaoj.Security.MasterKeyProviders;

public sealed class FileMasterKeyProvider : IMasterKeyProvider {
    private readonly string _filePath;

    public FileMasterKeyProvider(string filePath) {
        if(string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path must not be null or whitespace.", nameof(filePath));
        this._filePath = filePath;
    }

    /// <inheritdoc/>
    public async ValueTask<MasterKey> GetMasterKeyAsync(CancellationToken cancellationToken = default) {
        if(!File.Exists(this._filePath))
            throw new FileNotFoundException($"Master key file not found at '{this._filePath}'.");

        // Dosya içeriğini string olarak değil, direkt byte olarak okuyup parse etmek daha güvenlidir
        byte[] encodedBytes = await File.ReadAllBytesAsync(this._filePath, cancellationToken);
        byte[]? keyBytes = null;
        try {
            // Base64 içeriğini byte array'den direkt parse et (string allocation'ı engeller)
            string base64String = System.Text.Encoding.UTF8.GetString(encodedBytes).Trim();
            keyBytes = Convert.FromBase64String(base64String);

            return new MasterKey(Secret<byte>.From(keyBytes));
        }
        finally {
            CryptographicOperations.ZeroMemory(encodedBytes);
            if(keyBytes is not null) CryptographicOperations.ZeroMemory(keyBytes);
        }
    }
}