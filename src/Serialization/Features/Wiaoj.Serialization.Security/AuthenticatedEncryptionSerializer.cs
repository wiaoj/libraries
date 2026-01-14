using Microsoft.IO;
using System.Buffers;
using System.Security.Cryptography;
using System.Text;
using Wiaoj.Serialization.Security.Abstractions;

namespace Wiaoj.Serialization.Security;
/// <summary>
/// A decorator that wraps another serializer to apply authenticated encryption.
/// This class is an internal implementation detail of the security extensions.
/// </summary>
internal sealed class AuthenticatedEncryptionSerializer<TKey>(
    ISerializer<TKey> innerSerializer,
    IAuthenticatedEncryptor encryptor,
    RecyclableMemoryStreamManager streamManager) : ISerializer<TKey> where TKey : ISerializerKey {


    public byte[] Serialize<TValue>(TValue value) {
        // Inner serializer can throw UnsupportedTypeException
        byte[] plainBytes = innerSerializer.Serialize(value);
        return encryptor.Encrypt(plainBytes);
    }

    public string SerializeToString<TValue>(TValue value) {
        byte[] plainBytes = innerSerializer.Serialize(value);
        byte[] encryptedBytes = encryptor.Encrypt(plainBytes);
        return Convert.ToBase64String(encryptedBytes);
    }

    public string SerializeToString<TValue>(TValue value, Type type) {
        string plainText = innerSerializer.SerializeToString(value, type);
        byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
        byte[] encryptedBytes = encryptor.Encrypt(plainBytes);
        return Convert.ToBase64String(encryptedBytes);
    }

    public void Serialize<TValue>(IBufferWriter<byte> writer, TValue value) {
        byte[] encryptedBytes = Serialize(value);
        writer.Write(encryptedBytes);
    }

    // --- Deserialization Methods ---

    public TValue? Deserialize<TValue>(byte[] data) {
        try {
            byte[] decryptedBytes = encryptor.Decrypt(data);
            return innerSerializer.Deserialize<TValue>(decryptedBytes);
        }
        catch(CryptographicException ex) {
            throw new DecryptionFailedException($"Decryption failed for the provided data. It might be corrupted, tampered with, or the encryption key is incorrect.", ex);
        }
    }

    public object? Deserialize(byte[] data, Type type) {
        try {
            byte[] decryptedBytes = encryptor.Decrypt(data);
            return innerSerializer.Deserialize(decryptedBytes, type);
        }
        catch(CryptographicException ex) {
            throw new DecryptionFailedException($"Decryption failed for the provided data targeting type '{type.FullName}'. Data might be corrupted or key is incorrect.", ex);
        }
    }

    public TValue? DeserializeFromString<TValue>(string data) {
        try {
            byte[] encryptedBytes = Convert.FromBase64String(data);
            return Deserialize<TValue>(encryptedBytes);
        }
        catch(FormatException ex) // Catches invalid Base64
        {
            throw new DecryptionFailedException($"The input string is not a valid Base64 encoded representation.", ex);
        }
    }

    public object? DeserializeFromString(string data, Type type) {
        try {
            byte[] encryptedBytes = Convert.FromBase64String(data);
            return Deserialize(encryptedBytes, type);
        }
        catch(FormatException ex) // Catches invalid Base64
        {
            throw new DecryptionFailedException($"The input string is not a valid Base64 encoded representation.", ex);
        }
    }

    public TValue? Deserialize<TValue>(in ReadOnlySequence<byte> sequence) {
        // Decrypt needs a contiguous span, so we must copy.
        byte[] encryptedBytes = sequence.ToArray();
        return Deserialize<TValue>(encryptedBytes);
    }

    public object? Deserialize(in ReadOnlySequence<byte> sequence, Type type) {
        byte[] encryptedBytes = sequence.ToArray();
        return Deserialize(encryptedBytes, type);
    }

    // --- Async Methods ---

    public async Task SerializeAsync<TValue>(Stream stream, TValue value, CancellationToken cancellationToken) {
        await using RecyclableMemoryStream memoryStream = streamManager.GetStream();
        await innerSerializer.SerializeAsync(memoryStream, value, cancellationToken);
        memoryStream.Position = 0;

        byte[] plainBytes = memoryStream.ToArray(); // Use ToArray for correct length
        byte[] encryptedData = encryptor.Encrypt(plainBytes);
        await stream.WriteAsync(encryptedData, cancellationToken);
    }

    public async Task SerializeAsync(Stream stream, object value, Type type, CancellationToken cancellationToken) {
        await using RecyclableMemoryStream memoryStream = streamManager.GetStream();
        // CORRECTED: Call the correct overload that accepts a Type.
        await innerSerializer.SerializeAsync(memoryStream, value, type, cancellationToken);
        memoryStream.Position = 0;

        byte[] plainBytes = memoryStream.ToArray();
        byte[] encryptedData = encryptor.Encrypt(plainBytes);
        await stream.WriteAsync(encryptedData, cancellationToken);
    }

    public async ValueTask<TValue?> DeserializeAsync<TValue>(Stream stream, CancellationToken cancellationToken) {
        try {
            // TRUE STREAMING: Use CryptoStream for on-the-fly decryption.
            await using CryptoStream cryptoStream = encryptor.CreateDecryptionStream(stream);
            return await innerSerializer.DeserializeAsync<TValue>(cryptoStream, cancellationToken);
        }
        catch(Exception ex) when(ex is CryptographicException or IOException or NotSupportedException) {
            throw new DecryptionFailedException($"Asynchronous decryption failed for the stream targeting type '{typeof(TValue).FullName}'. The stream might be corrupted or the key incorrect.", ex);
        }
    }

    public async ValueTask<object?> DeserializeAsync(Stream stream, Type type, CancellationToken cancellationToken) {
        try {
            await using CryptoStream cryptoStream = encryptor.CreateDecryptionStream(stream);
            return await innerSerializer.DeserializeAsync(cryptoStream, type, cancellationToken);
        }
        catch(Exception ex) when(ex is CryptographicException or IOException or NotSupportedException) {
            throw new DecryptionFailedException($"Asynchronous decryption failed for the stream targeting type '{type.FullName}'. The stream might be corrupted or the key incorrect.", ex);
        }
    }

    // --- Try... Methods ---

    public bool TryDeserialize<TValue>(byte[] data, out TValue? result) {
        try {
            result = Deserialize<TValue>(data);
            return true;
        }
        catch(WiaojSecurityException) // Catch our specific, wrapped exception
        {
            result = default;
            return false;
        }
    }

    public bool TryDeserializeFromString<TValue>(string data, out TValue? result) {
        try {
            result = DeserializeFromString<TValue>(data);
            return true;
        }
        catch(WiaojSecurityException) {
            result = default;
            return false;
        }
    }

    public bool TryDeserialize<TValue>(in ReadOnlySequence<byte> sequence, out TValue? result) {
        try {
            result = Deserialize<TValue>(in sequence);
            return true;
        }
        catch(WiaojSecurityException) {
            result = default;
            return false;
        }
    }

    public async ValueTask<(bool Success, TValue? Value)> TryDeserializeAsync<TValue>(Stream stream, CancellationToken cancellationToken = default) {
        try {
            TValue? result = await DeserializeAsync<TValue>(stream, cancellationToken);
            return (true, result);
        }
        catch(WiaojSecurityException) {
            return (false, default);
        }
    }
}