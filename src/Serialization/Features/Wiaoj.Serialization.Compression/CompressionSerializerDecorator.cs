using System.Buffers;
using System.Text;
using Microsoft.IO;
using Wiaoj.Serialization.Abstractions;
using Wiaoj.Serialization.Compression.Abstractions;

namespace Wiaoj.Serialization.Compression;
/// <summary>
/// Decorator that wraps another serializer to apply compression and decompression.
/// This is an internal implementation detail.
/// </summary>
internal sealed class CompressionSerializerDecorator<TKey> : ISerializer<TKey> where TKey : ISerializerKey {
    private readonly ISerializer<TKey> _innerSerializer;
    private readonly ICompressor _compressor;
    private readonly RecyclableMemoryStreamManager _streamManager;

    public CompressionSerializerDecorator(ISerializer<TKey> innerSerializer, ICompressor compressor, RecyclableMemoryStreamManager streamManager) {
        this._innerSerializer = innerSerializer;
        this._compressor = compressor;
        this._streamManager = streamManager;
    }

    // --- Serialization Methods ---

    public byte[] Serialize<TValue>(TValue value) {
        // Inner serializer can throw UnsupportedTypeException, let it propagate.
        byte[] plainBytes = this._innerSerializer.Serialize(value);
        return this._compressor.Compress(plainBytes);
    }

    public string SerializeToString<TValue>(TValue value) {
        // Relies on Serialize<TValue> which is already handled.
        byte[] compressedBytes = Serialize(value);
        return Convert.ToBase64String(compressedBytes);
    }

    public string SerializeToString<TValue>(TValue value, Type type) {
        string plainText = this._innerSerializer.SerializeToString(value, type);
        byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
        byte[] compressedBytes = this._compressor.Compress(plainBytes);
        return Convert.ToBase64String(compressedBytes);
    }

    public void Serialize<TValue>(IBufferWriter<byte> writer, TValue value) {
        byte[] compressedBytes = Serialize(value);
        writer.Write(compressedBytes);
    }

    // --- Deserialization Methods ---

    public TValue? Deserialize<TValue>(byte[] data) {
        try {
            byte[] plainBytes = this._compressor.Decompress(data);
            // Inner serializer will throw DeserializationFormatException if plainBytes are invalid for the target type.
            return this._innerSerializer.Deserialize<TValue>(plainBytes);
        }
        catch (Exception ex) {
            // Catch exceptions from the decompressor itself (e.g., invalid gzip header).
            throw new DeserializationFormatException($"Failed to decompress and deserialize data for type '{typeof(TValue).FullName}'. The data may be corrupt or in an unexpected format.", ex);
        }
    }

    public object? Deserialize(byte[] data, Type type) {
        try {
            byte[] plainBytes = this._compressor.Decompress(data);
            return this._innerSerializer.Deserialize(plainBytes, type);
        }
        catch (Exception ex) {
            throw new DeserializationFormatException($"Failed to decompress and deserialize data for type '{type.FullName}'. See inner exception for details.", ex);
        }
    }

    public TValue? DeserializeFromString<TValue>(string data) {
        try {
            byte[] compressedBytes = Convert.FromBase64String(data);
            return Deserialize<TValue>(compressedBytes);
        }
        catch (FormatException ex) // Invalid Base64
        {
            throw new DeserializationFormatException($"The input string is not a valid Base64 encoded representation for the target type '{typeof(TValue).FullName}'.", ex);
        }
    }

    public object? DeserializeFromString(string data, Type type) {
        try {
            byte[] compressedBytes = Convert.FromBase64String(data);
            return Deserialize(compressedBytes, type);
        }
        catch (FormatException ex) // Invalid Base64
        {
            throw new DeserializationFormatException($"The input string is not a valid Base64 encoded representation for the target type '{type.FullName}'.", ex);
        }
    }

    public TValue? Deserialize<TValue>(in ReadOnlySequence<byte> sequence) {
        // Decompressor needs a contiguous array/stream.
        return Deserialize<TValue>(sequence.ToArray());
    }

    public object? Deserialize(in ReadOnlySequence<byte> sequence, Type type) {
        return Deserialize(sequence.ToArray(), type);
    }

    // --- Async Methods ---

    public async Task SerializeAsync<TValue>(Stream stream, TValue value, CancellationToken cancellationToken) {
        // This still buffers, but it's a reasonable approach for compression.
        await using MemoryStream memoryStream = this._streamManager.GetStream();
        await this._innerSerializer.SerializeAsync(memoryStream, value, cancellationToken);
        memoryStream.Position = 0; // Rewind before reading for compression.

        await using Stream compressionStream = this._compressor.CreateCompressionStream(stream);
        await memoryStream.CopyToAsync(compressionStream, cancellationToken);
    }

    public async Task SerializeAsync(Stream stream, object value, Type type, CancellationToken cancellationToken) {
        await using MemoryStream memoryStream = this._streamManager.GetStream();
        await this._innerSerializer.SerializeAsync(memoryStream, value, type, cancellationToken);
        memoryStream.Position = 0;

        await using Stream compressionStream = this._compressor.CreateCompressionStream(stream);
        await memoryStream.CopyToAsync(compressionStream, cancellationToken);
    }

    public async ValueTask<TValue?> DeserializeAsync<TValue>(Stream stream, CancellationToken cancellationToken) {
        try {
            // TRUE STREAMING: Chain the decompression stream to the inner serializer.
            await using Stream decompressionStream = this._compressor.CreateDecompressionStream(stream);
            return await this._innerSerializer.DeserializeAsync<TValue>(decompressionStream, cancellationToken);
        }
        catch (Exception ex) // Catches errors from both decompression and inner deserialization
        {
            throw new DeserializationFormatException($"Failed to asynchronously decompress and deserialize stream for type '{typeof(TValue).FullName}'.", ex);
        }
    }

    public async ValueTask<object?> DeserializeAsync(Stream stream, Type type, CancellationToken cancellationToken) {
        try {
            await using Stream decompressionStream = this._compressor.CreateDecompressionStream(stream);
            return await this._innerSerializer.DeserializeAsync(decompressionStream, type, cancellationToken);
        }
        catch (Exception ex) {
            throw new DeserializationFormatException($"Failed to asynchronously decompress and deserialize stream for type '{type.FullName}'.", ex);
        }
    }

    // --- Try... Methods ---

    public bool TryDeserialize<TValue>(byte[] data, out TValue? result) {
        try {
            result = Deserialize<TValue>(data);
            return true;
        }
        catch (WiaojSerializationException) { // Catches our wrapped exceptions
            result = default;
            return false;
        }
    }

    public bool TryDeserializeFromString<TValue>(string data, out TValue? result) {
        try {
            result = DeserializeFromString<TValue>(data);
            return true;
        }
        catch (WiaojSerializationException) {
            result = default;
            return false;
        }
    }

    public bool TryDeserialize<TValue>(in ReadOnlySequence<byte> sequence, out TValue? result) {
        try {
            result = Deserialize<TValue>(in sequence);
            return true;
        }
        catch (WiaojSerializationException) {
            result = default;
            return false;
        }
    }

    public async ValueTask<(bool Success, TValue? Value)> TryDeserializeAsync<TValue>(Stream stream, CancellationToken cancellationToken = default) {
        try {
            TValue? result = await DeserializeAsync<TValue>(stream, cancellationToken);
            return (true, result);
        }
        catch (WiaojSerializationException) {
            return (false, default);
        }
    }
}