using MemoryPack;
using System.Buffers;

namespace Wiaoj.Serialization.MemoryPack;

public sealed class MemoryPackSerializer<TKey>(MemoryPackSerializerOptions options) : ISerializer<TKey> where TKey : ISerializerKey {
    // --- Serialization Methods ---

    /// <inheritdoc />
    public string SerializeToString<TValue>(TValue value) {
        Preca.ThrowIfNull(value);
        try {
            byte[] bytes = MemoryPackSerializer.Serialize(value, options);
            return Convert.ToBase64String(bytes);
        }
        catch(MemoryPackSerializationException ex) {
            throw new UnsupportedTypeException($"The type '{value.GetType().FullName}' or one of its members is not supported by the MemoryPack serializer.", ex);
        }
    }

    /// <inheritdoc />
    public string SerializeToString<TValue>(TValue value, Type type) {
        Preca.ThrowIfNull(value);
        Preca.ThrowIfNull(type);
        try {
            byte[] bytes = MemoryPackSerializer.Serialize(type, value, options);
            return Convert.ToBase64String(bytes);
        }
        catch(MemoryPackSerializationException ex) {
            throw new UnsupportedTypeException($"The type '{type.FullName}' or one of its members is not supported by the MemoryPack serializer.", ex);
        }
    }

    /// <inheritdoc />
    public byte[] Serialize<TValue>(TValue value) {
        Preca.ThrowIfNull(value);
        try {
            return MemoryPackSerializer.Serialize(value, options);
        }
        catch(MemoryPackSerializationException ex) {
            throw new UnsupportedTypeException($"The type '{value.GetType().FullName}' or one of its members is not supported by the MemoryPack serializer.", ex);
        }
    }

    /// <inheritdoc />
    public void Serialize<TValue>(IBufferWriter<byte> writer, TValue value) {
        Preca.ThrowIfNull(writer);
        Preca.ThrowIfNull(value);
        try {
            MemoryPackSerializer.Serialize(writer, value, options);
        }
        catch(MemoryPackSerializationException ex) {
            throw new UnsupportedTypeException($"The type '{value.GetType().FullName}' or one of its members is not supported by the MemoryPack serializer.", ex);
        }
    }

    // --- Deserialization Methods ---

    /// <inheritdoc />
    public TValue? DeserializeFromString<TValue>(string data) {
        Preca.ThrowIfNullOrWhiteSpace(data);
        try {
            byte[] bytes = Convert.FromBase64String(data);
            return MemoryPackSerializer.Deserialize<TValue>(bytes, options);
        }
        catch(Exception ex) when(ex is MemoryPackSerializationException or FormatException) {
            throw new DeserializationFormatException($"The input string is not a valid Base64 encoded MemoryPack representation for the target type '{typeof(TValue).FullName}'.", ex);
        }
    }

    /// <inheritdoc />
    public object? DeserializeFromString(string data, Type type) {
        Preca.ThrowIfNullOrWhiteSpace(data);
        Preca.ThrowIfNull(type);
        try {
            byte[] bytes = Convert.FromBase64String(data);
            return MemoryPackSerializer.Deserialize(type, bytes, options);
        }
        catch(Exception ex) when(ex is MemoryPackSerializationException or FormatException) {
            throw new DeserializationFormatException($"The input string is not a valid Base64 encoded MemoryPack representation for the target type '{type.FullName}'.", ex);
        }
    }

    /// <inheritdoc />
    public TValue? Deserialize<TValue>(byte[] data) {
        Preca.ThrowIfNull(data);
        try {
            return MemoryPackSerializer.Deserialize<TValue>(data, options);
        }
        catch(MemoryPackSerializationException ex) {
            throw new DeserializationFormatException($"The input byte array is not a valid MemoryPack representation for the target type '{typeof(TValue).FullName}'.", ex);
        }
    }

    /// <inheritdoc />
    public object? Deserialize(byte[] data, Type type) {
        Preca.ThrowIfNull(data);
        Preca.ThrowIfNull(type);
        try {
            return MemoryPackSerializer.Deserialize(type, data, options);
        }
        catch(MemoryPackSerializationException ex) {
            throw new DeserializationFormatException($"The input byte array is not a valid MemoryPack representation for the target type '{type.FullName}'.", ex);
        }
    }

    /// <inheritdoc />
    public TValue? Deserialize<TValue>(in ReadOnlySequence<byte> sequence) {
        try {
            return MemoryPackSerializer.Deserialize<TValue>(sequence, options);
        }
        catch(MemoryPackSerializationException ex) {
            throw new DeserializationFormatException($"The input byte sequence is not a valid MemoryPack representation for the target type '{typeof(TValue).FullName}'.", ex);
        }
    }

    /// <inheritdoc />
    public object? Deserialize(in ReadOnlySequence<byte> sequence, Type type) {
        Preca.ThrowIfNull(type);
        try {
            return MemoryPackSerializer.Deserialize(type, sequence, options);
        }
        catch(MemoryPackSerializationException ex) {
            throw new DeserializationFormatException($"The input byte sequence is not a valid MemoryPack representation for the target type '{type.FullName}'.", ex);
        }
    }

    // --- Try... Methods ---

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
        Preca.ThrowIfNull(value);
        try {
            await MemoryPackSerializer.SerializeAsync(stream, value, options, cancellationToken);
        }
        catch(MemoryPackSerializationException ex) {
            throw new UnsupportedTypeException($"The type '{value.GetType().FullName}' is not supported by the async MemoryPack serializer.", ex);
        }
    }

    /// <inheritdoc />
    public async Task SerializeAsync(Stream stream, object value, Type type, CancellationToken cancellationToken) {
        Preca.ThrowIfNull(stream);
        Preca.ThrowIfNull(value);
        Preca.ThrowIfNull(type);
        try {
            await MemoryPackSerializer.SerializeAsync(type, stream, value, options, cancellationToken);
        }
        catch(MemoryPackSerializationException ex) {
            throw new UnsupportedTypeException($"The type '{type.FullName}' is not supported by the async MemoryPack serializer.", ex);
        }
    }

    /// <summary>
    /// (Feature-parity with System.Text.Json and MessagePack) Asynchronously serializes a sequence of values.
    /// </summary>
    public async Task SerializeAsync<TValue>(Stream stream, IAsyncEnumerable<TValue> values, CancellationToken cancellationToken) {
        Preca.ThrowIfNull(stream);
        Preca.ThrowIfNull(values);
        try {
            await foreach(TValue? item in values.WithCancellation(cancellationToken)) {
                await MemoryPackSerializer.SerializeAsync(stream, item, options, cancellationToken);
            }
        }
        catch(MemoryPackSerializationException ex) {
            throw new UnsupportedTypeException($"The async enumerable of type '{typeof(TValue).FullName}' is not supported by the MemoryPack serializer.", ex);
        }
    }

    /// <inheritdoc />
    public async ValueTask<TValue?> DeserializeAsync<TValue>(Stream stream, CancellationToken cancellationToken) {
        Preca.ThrowIfNull(stream);
        try {
            return await MemoryPackSerializer.DeserializeAsync<TValue>(stream, options, cancellationToken);
        }
        catch(MemoryPackSerializationException ex) {
            throw new DeserializationFormatException($"The input stream does not contain a valid MemoryPack representation for the target type '{typeof(TValue).FullName}'.", ex);
        }
    }

    /// <inheritdoc />
    public async ValueTask<object?> DeserializeAsync(Stream stream, Type type, CancellationToken cancellationToken) {
        Preca.ThrowIfNull(stream);
        Preca.ThrowIfNull(type);
        try {
            return await MemoryPackSerializer.DeserializeAsync(type, stream, options, cancellationToken);
        }
        catch(MemoryPackSerializationException ex) {
            throw new DeserializationFormatException($"The input stream does not contain a valid MemoryPack representation for the target type '{type.FullName}'.", ex);
        }
    }

    /// <inheritdoc />
    public async ValueTask<(bool Success, TValue? Value)> TryDeserializeAsync<TValue>(Stream stream, CancellationToken cancellationToken) {
        Preca.ThrowIfNull(stream);
        try {
            TValue? value = await DeserializeAsync<TValue>(stream, cancellationToken);
            return (true, value);
        }
        catch(WiaojSerializationException) {
            return (false, default);
        }
    }
}