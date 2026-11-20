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

        byte[] encodedBytes = await File.ReadAllBytesAsync(this._filePath, cancellationToken);
        char[]? encodedChars = null;
        byte[]? keyBytes = null;
        try {
            encodedChars = System.Text.Encoding.UTF8.GetChars(encodedBytes);
            if(!MasterKeyEncoding.TryDecode(encodedChars, out keyBytes))
                throw new FormatException("Master key file must contain Base64 or Base64Url text.");
            if(keyBytes.Length is not (16 or 24 or 32))
                throw new InvalidOperationException("Master key must be 16, 24, or 32 bytes.");

            return new MasterKey(Secret<byte>.From(keyBytes));
        }
        finally {
            CryptographicOperations.ZeroMemory(encodedBytes);
            if(encodedChars is not null) CryptographicOperations.ZeroMemory(System.Runtime.InteropServices.MemoryMarshal.AsBytes(encodedChars.AsSpan()));
            if(keyBytes is not null) CryptographicOperations.ZeroMemory(keyBytes);
        }
    }
}