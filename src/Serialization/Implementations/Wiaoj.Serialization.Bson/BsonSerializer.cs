using Microsoft.IO;
using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization;
using System.Buffers;

namespace Wiaoj.Serialization.Bson;

/// <summary>
/// A high-performance implementation of ISerializer using the MongoDB.Bson library.
/// </summary>
/// <typeparam name="TKey">The key identifying this serializer instance.</typeparam>
public sealed class BsonSerializer<TKey>(
    Action<BsonSerializationContext.Builder> serializationConfigurator,
    Action<BsonDeserializationContext.Builder> deserializationConfigurator,
    BsonSerializationArgs defaultSerializationArgs,
    RecyclableMemoryStreamManager streamManager) : ISerializer<TKey> where TKey : ISerializerKey {
    // --- Serialization Methods ---

    /// <inheritdoc />
    public byte[] Serialize<TValue>(TValue value) {
        if(value is null) return [];
        // Delegate to the private, type-aware Serialize method.
        return Serialize(value, typeof(TValue));
    }

    /// <summary>
    /// Central private method for serialization to handle exceptions consistently.
    /// </summary>
    private byte[] Serialize(object? value, Type type) {
        if(value is null) return [];
        Preca.ThrowIfNull(type);

        try {
            using RecyclableMemoryStream stream = streamManager.GetStream();
            using(BsonBinaryWriter writer = new(stream)) {
                // CORRECTED: Pass the type and value in the correct order.
                BsonSerializer.Serialize(writer, type, value, serializationConfigurator, defaultSerializationArgs);
            }
            return stream.ToArray();
        }
        catch(Exception ex) when(ex is BsonException or InvalidOperationException) {
            throw new UnsupportedTypeException($"The type '{type.FullName}' or one of its members could not be serialized to BSON.", ex);
        }
    }

    /// <inheritdoc />
    public string SerializeToString<TValue>(TValue value) {
        return Convert.ToBase64String(Serialize(value));
    }

    /// <inheritdoc />
    public string SerializeToString<TValue>(TValue value, Type type) {
        return Convert.ToBase64String(Serialize(value, type));
    }

    /// <inheritdoc />
    public void Serialize<TValue>(IBufferWriter<byte> writer, TValue value) {
        Preca.ThrowIfNull(writer);
        writer.Write(Serialize(value));
    }

    // --- Deserialization Methods ---

    /// <inheritdoc />
    public TValue? Deserialize<TValue>(byte[] data) {
        if(data is null or { Length: 0 }) return default;
        try {
            using RecyclableMemoryStream stream = streamManager.GetStream(data);
            using BsonBinaryReader reader = new(stream);
            return BsonSerializer.Deserialize<TValue>(reader, deserializationConfigurator);
        }
        catch(Exception ex) when(ex is BsonException or FormatException or IndexOutOfRangeException or IOException) {
            throw new DeserializationFormatException($"The input byte array is not a valid BSON representation for the target type '{typeof(TValue).FullName}'.", ex);
        }
    }

    /// <inheritdoc />
    public object? Deserialize(byte[] data, Type type) {
        Preca.ThrowIfNull(type);
        if(data is null or { Length: 0 }) return null;
        try {
            using RecyclableMemoryStream stream = streamManager.GetStream(data);
            using BsonBinaryReader reader = new(stream);
            return BsonSerializer.Deserialize(reader, type, deserializationConfigurator);
        }
        catch(Exception ex) when(ex is BsonException or FormatException or IndexOutOfRangeException or IOException) {
            throw new DeserializationFormatException($"The input byte array is not a valid BSON representation for the target type '{type.FullName}'.", ex);
        }
    }

    /// <inheritdoc />
    public TValue? DeserializeFromString<TValue>(string data) {
        Preca.ThrowIfNullOrWhiteSpace(data);
        try {
            return Deserialize<TValue>(Convert.FromBase64String(data));
        }
        catch(FormatException ex) // Catches invalid Base64 string
        {
            throw new DeserializationFormatException($"The input string is not a valid Base64 encoded BSON representation for the target type '{typeof(TValue).FullName}'.", ex);
        }
    }

    /// <inheritdoc />
    public object? DeserializeFromString(string data, Type type) {
        Preca.ThrowIfNullOrWhiteSpace(data);
        Preca.ThrowIfNull(type);
        try {
            return Deserialize(Convert.FromBase64String(data), type);
        }
        catch(FormatException ex) {
            throw new DeserializationFormatException($"The input string is not a valid Base64 encoded BSON representation for the target type '{type.FullName}'.", ex);
        }
    }

    /// <inheritdoc />
    public TValue? Deserialize<TValue>(in ReadOnlySequence<byte> sequence) {
        return Deserialize<TValue>(sequence.ToArray());
    }

    /// <inheritdoc />
    public object? Deserialize(in ReadOnlySequence<byte> sequence, Type type) {
        return Deserialize(sequence.ToArray(), type);
    }

    // --- Try... Methods ---

    /// <inheritdoc />
    public bool TryDeserialize<TValue>(byte[] data, out TValue? result) {
        try {
            result = Deserialize<TValue>(data);
            return true;
        }
        catch(WiaojSerializationException) {
            result = default;
            return false;
        }
    }

    /// <inheritdoc />
    public bool TryDeserializeFromString<TValue>(string data, out TValue? result) {
        try {
            result = DeserializeFromString<TValue>(data);
            return true;
        }
        catch(WiaojSerializationException) {
            result = default;
            return false;
        }
    }

    /// <inheritdoc />
    public bool TryDeserialize<TValue>(in ReadOnlySequence<byte> sequence, out TValue? result) {
        try {
            result = Deserialize<TValue>(in sequence);
            return true;
        }
        catch(WiaojSerializationException) {
            result = default;
            return false;
        }
    }

    // --- Async Methods ---

    /// <inheritdoc />
    public async Task SerializeAsync<TValue>(Stream stream, TValue value, CancellationToken cancellationToken) {
        Preca.ThrowIfNull(stream);
        cancellationToken.ThrowIfCancellationRequested();
        if(value is null) return;

        try {
            await using RecyclableMemoryStream tempStream = streamManager.GetStream();
            using(BsonBinaryWriter writer = new(tempStream)) {
                // CORRECTED: Pass the type and value in the correct order.
                BsonSerializer.Serialize(writer, typeof(TValue), value, serializationConfigurator, defaultSerializationArgs);
            }
            tempStream.Position = 0;
            await tempStream.CopyToAsync(stream, cancellationToken).ConfigureAwait(false);
        }
        catch(Exception ex) when(ex is BsonException or InvalidOperationException) {
            throw new UnsupportedTypeException($"The type '{value?.GetType().FullName ?? typeof(TValue).FullName}' could not be serialized to the stream as BSON.", ex);
        }
    }

    /// <inheritdoc />
    public Task SerializeAsync(Stream stream, object value, Type type, CancellationToken cancellationToken) {
        Preca.ThrowIfNull(stream);
        Preca.ThrowIfNull(value);
        Preca.ThrowIfNull(type);
        // Delegate to the private Serialize method wrapped in an async task structure.
        return Task.Run(() => {
            byte[] data = Serialize(value, type);
            return stream.WriteAsync(data, 0, data.Length, cancellationToken);
        }, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask<TValue?> DeserializeAsync<TValue>(Stream stream, CancellationToken cancellationToken) {
        Preca.ThrowIfNull(stream);
        cancellationToken.ThrowIfCancellationRequested();
        try {
            using BsonBinaryReader reader = new(stream);
            TValue? result = BsonSerializer.Deserialize<TValue>(reader, deserializationConfigurator);
            return new ValueTask<TValue?>(result);
        }
        catch(Exception ex) when(ex is BsonException or FormatException or IOException) {
            throw new DeserializationFormatException($"The input stream does not contain a valid BSON representation for the target type '{typeof(TValue).FullName}'.", ex);
        }
    }

    /// <inheritdoc />
    public ValueTask<object?> DeserializeAsync(Stream stream, Type type, CancellationToken cancellationToken) {
        Preca.ThrowIfNull(stream);
        Preca.ThrowIfNull(type);
        cancellationToken.ThrowIfCancellationRequested();
        try {
            using BsonBinaryReader reader = new(stream);
            object? result = BsonSerializer.Deserialize(reader, type, deserializationConfigurator);
            return new ValueTask<object?>(result);
        }
        catch(Exception ex) when(ex is BsonException or FormatException or IOException) {
            throw new DeserializationFormatException($"The input stream does not contain a valid BSON representation for the target type '{type.FullName}'.", ex);
        }
    }

    /// <inheritdoc />
    public async ValueTask<(bool Success, TValue? Value)> TryDeserializeAsync<TValue>(Stream stream, CancellationToken cancellationToken) {
        try {
            TValue? result = await DeserializeAsync<TValue>(stream, cancellationToken);
            return (true, result);
        }
        catch(WiaojSerializationException) {
            return (false, default);
        }
    }
}