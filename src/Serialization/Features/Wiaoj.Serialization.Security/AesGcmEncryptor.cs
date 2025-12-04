using System.Runtime.Versioning;
using System.Security.Cryptography;
using Microsoft.IO;
using Wiaoj.Serialization.Security.Abstractions;

namespace Wiaoj.Serialization.Security;
/// <summary>
/// An implementation of <see cref="IAuthenticatedEncryptor"/> using the AES-GCM algorithm.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="AesGcmEncryptor"/> with the specified key.
/// </remarks>
/// <param name="key">The encryption key. Must be 128, 192, or 256 bits (16, 24, or 32 bytes).</param>
/// <param name="streamManager"></param>
[UnsupportedOSPlatform("browser")]
[UnsupportedOSPlatform("ios")]
[UnsupportedOSPlatform("tvos")]
[SupportedOSPlatform("ios13.0")]
[SupportedOSPlatform("tvos13.0")]
internal sealed class AesGcmEncryptor(ReadOnlyMemory<byte> key, RecyclableMemoryStreamManager streamManager) : IAuthenticatedEncryptor {
    private const int NonceSize = 12; // AES-GCM standard nonce size is 96 bits (12 bytes)
    private const int TagSize = 16; // AES-GCM standard tag size is 128 bits (16 bytes)

    /// <inheritdoc />
    public byte[] Encrypt(ReadOnlySpan<byte> plainBytes) {
        int totalLength = NonceSize + TagSize + plainBytes.Length;
        byte[] encryptedData = new byte[totalLength];

        Span<byte> nonceSpan = encryptedData.AsSpan(0, NonceSize);
        Span<byte> tagSpan = encryptedData.AsSpan(NonceSize, TagSize);
        Span<byte> ciphertextSpan = encryptedData.AsSpan(NonceSize + TagSize);

        RandomNumberGenerator.Fill(nonceSpan);

        using (AesGcm aes = new(key.Span, TagSize)) {
            aes.Encrypt(nonceSpan, plainBytes, ciphertextSpan, tagSpan);
        }
        return encryptedData;
    }

    /// <inheritdoc />
    public byte[] Decrypt(ReadOnlySpan<byte> encryptedData) {
        if (encryptedData.Length < NonceSize + TagSize)
            throw new CryptographicException("Invalid encrypted data size.");

        ReadOnlySpan<byte> nonce = encryptedData[..NonceSize];
        ReadOnlySpan<byte> tag = encryptedData.Slice(NonceSize, TagSize);
        ReadOnlySpan<byte> ciphertext = encryptedData[(NonceSize + TagSize)..];

        byte[] decryptedBytes = new byte[ciphertext.Length];

        using (AesGcm aes = new(key.Span, TagSize)) {
            aes.Decrypt(nonce, ciphertext, tag, decryptedBytes);
        }
        return decryptedBytes;
    }

    /// <summary>
    /// Creates a stream that decrypts data on the fly.
    /// NOTE: Due to the nature of AEAD ciphers like AES-GCM, this implementation buffers the entire
    /// underlying stream to verify the authentication tag before providing the decrypted data.
    /// This is by design to ensure security and data integrity.
    /// </summary>
    public CryptoStream CreateDecryptionStream(Stream streamToReadFrom) {
        // CryptoStream'in kendisi AES-GCM'i doğrudan desteklemez.
        // Bu nedenle, deşifre edilmiş veriyi tutan bir MemoryStream'i
        // okuyan "sahte" bir CryptoStream döndürüyoruz.
        // Bu, IAuthenticatedEncryptor arayüzünü tatmin ederken güvenliği sağlar.

        // Önce tüm şifreli veriyi oku
        using RecyclableMemoryStream encryptedStream = streamManager.GetStream();
        streamToReadFrom.CopyTo(encryptedStream);
        encryptedStream.Position = 0;

        // Tek seferde deşifrele
        byte[] decryptedBytes = Decrypt(encryptedStream.GetBuffer().AsSpan(0, (int)encryptedStream.Length));

        // Deşifre edilmiş veriyi yeni bir stream'e koy
        RecyclableMemoryStream decryptedStream = streamManager.GetStream(decryptedBytes);

        // Standart CryptoStream'i "noop" bir dönüşümle sarmala,
        // çünkü asıl işi zaten yaptık. Bu, arayüz kontratını yerine getirir.
        return new CryptoStream(decryptedStream, new NoopTransform(), CryptoStreamMode.Read);
    }

    /// <summary>
    /// CryptoStream'in bir ICryptoTransform nesnesi beklemesi nedeniyle kullanılan
    /// "hiçbir şey yapmayan" bir dönüşüm sınıfı.
    /// </summary>
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