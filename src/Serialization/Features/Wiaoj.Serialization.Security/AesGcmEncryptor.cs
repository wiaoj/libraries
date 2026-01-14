using Microsoft.IO;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using Wiaoj.Primitives;
using Wiaoj.Serialization.Security.Abstractions;

namespace Wiaoj.Serialization.Security;

[UnsupportedOSPlatform("browser")]
[UnsupportedOSPlatform("ios")]
[UnsupportedOSPlatform("tvos")]
[SupportedOSPlatform("ios13.0")]
[SupportedOSPlatform("tvos13.0")]
internal sealed class AesGcmEncryptor(Secret<byte> key, RecyclableMemoryStreamManager streamManager) : IAuthenticatedEncryptor {
    private const int NonceSize = 12;
    private const int TagSize = 16;

    /// <inheritdoc />
    public byte[] Encrypt(ReadOnlySpan<byte> plainBytes) {
        int totalLength = NonceSize + TagSize + plainBytes.Length;
        byte[] encryptedData = new byte[totalLength];

        Span<byte> nonceSpan = encryptedData.AsSpan(0, NonceSize);
        Span<byte> tagSpan = encryptedData.AsSpan(NonceSize, TagSize);
        Span<byte> ciphertextSpan = encryptedData.AsSpan(NonceSize + TagSize);

        RandomNumberGenerator.Fill(nonceSpan);

        using AesGcm aes = key.Expose(k => new AesGcm(k, TagSize));

        aes.Encrypt(nonceSpan, plainBytes, ciphertextSpan, tagSpan);

        return encryptedData;
    }

    /// <inheritdoc />
    public byte[] Decrypt(ReadOnlySpan<byte> encryptedData) {
        if(encryptedData.Length < NonceSize + TagSize) {
            throw new CryptographicException("Invalid encrypted data size.");
        }

        ReadOnlySpan<byte> nonce = encryptedData[..NonceSize];
        ReadOnlySpan<byte> tag = encryptedData.Slice(NonceSize, TagSize);
        ReadOnlySpan<byte> ciphertext = encryptedData[(NonceSize + TagSize)..];

        byte[] decryptedBytes = new byte[ciphertext.Length];

        // DÜZELTME:
        // Aynı mantık burada da geçerli.
        using AesGcm aes = key.Expose(k => new AesGcm(k, TagSize));

        aes.Decrypt(nonce, ciphertext, tag, decryptedBytes);

        return decryptedBytes;
    }

    public CryptoStream CreateDecryptionStream(Stream streamToReadFrom) {
        using RecyclableMemoryStream encryptedStream = streamManager.GetStream();
        streamToReadFrom.CopyTo(encryptedStream);
        encryptedStream.Position = 0;

        byte[] decryptedBytes = Decrypt(encryptedStream.GetBuffer().AsSpan(0, (int)encryptedStream.Length));

        RecyclableMemoryStream decryptedStream = streamManager.GetStream(decryptedBytes);

        return new CryptoStream(decryptedStream, new NoopTransform(), CryptoStreamMode.Read);
    }

    private sealed class NoopTransform : ICryptoTransform {
        public int InputBlockSize => 1;
        public int OutputBlockSize => 1;
        public bool CanTransformMultipleBlocks => true;
        public bool CanReuseTransform => true;

        public int TransformBlock(byte[] inputBuffer, int inputOffset, int inputCount, byte[] outputBuffer, int outputOffset) {
            Buffer.BlockCopy(inputBuffer, inputOffset, outputBuffer, outputOffset, inputCount);
            return inputCount;
        }

        public byte[] TransformFinalBlock(byte[] inputBuffer, int inputOffset, int inputCount) {
            byte[] output = new byte[inputCount];
            Buffer.BlockCopy(inputBuffer, inputOffset, output, 0, inputCount);
            return output;
        }

        public void Dispose() { }
    }
}