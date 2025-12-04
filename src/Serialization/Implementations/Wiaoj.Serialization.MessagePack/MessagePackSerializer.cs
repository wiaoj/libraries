using System.Buffers;
using MessagePack;
using Wiaoj.Serialization.Abstractions;

namespace Wiaoj.Serialization.MessagePack; 
public sealed class MessagePackSerializer<TKey>(MessagePackSerializerOptions options) : ISerializer<TKey> where TKey : ISerializerKey {
    // --- Serialization Methods ---

    /// <inheritdoc />
    public string SerializeToString<TValue>(TValue value) {
        Preca.ThrowIfNull(value);
        try {
            byte[] bytes = MessagePackSerializer.Serialize(value, options);
            return Convert.ToBase64String(bytes);
        }
        catch (MessagePackSerializationException ex) {
            throw new UnsupportedTypeException($"The type '{value.GetType().FullName}' or one of its members is not supported by the MessagePack serializer.", ex);
        }
    }

    /// <inheritdoc />
    public string SerializeToString<TValue>(TValue value, Type type) {
        Preca.ThrowIfNull(value);
        Preca.ThrowIfNull(type);
        try {
            byte[] bytes = MessagePackSerializer.Serialize(type, value, options);
            return Convert.ToBase64String(bytes);
        }
        catch (MessagePackSerializationException ex) {
            throw new UnsupportedTypeException($"The type '{type.FullName}' or one of its members is not supported by the MessagePack serializer.", ex);
        }
    }

    /// <inheritdoc />
    public byte[] Serialize<TValue>(TValue value) {
        Preca.ThrowIfNull(value);
        try {
            return MessagePackSerializer.Serialize(value, options);
        }
        catch (MessagePackSerializationException ex) {
            throw new UnsupportedTypeException($"The type '{value.GetType().FullName}' or one of its members is not supported by the MessagePack serializer.", ex);
        }
    }

    /// <inheritdoc />
    public void Serialize<TValue>(IBufferWriter<byte> writer, TValue value) {
        Preca.ThrowIfNull(writer);
        Preca.ThrowIfNull(value);
        try {
            MessagePackSerializer.Serialize(writer, value, options);
        }
        catch (MessagePackSerializationException ex) {
            throw new UnsupportedTypeException($"The type '{value.GetType().FullName}' or one of its members is not supported by the MessagePack serializer.", ex);
        }
    }

    // --- Deserialization Methods ---

    /// <inheritdoc />
    public TValue? DeserializeFromString<TValue>(string data) {
        Preca.ThrowIfNullOrWhiteSpace(data);
        try {
            byte[] bytes = Convert.FromBase64String(data);
            return MessagePackSerializer.Deserialize<TValue>(bytes, options);
        }
        catch (Exception ex) when (ex is MessagePackSerializationException or FormatException) {
            throw new DeserializationFormatException($"The input string is not a valid Base64 encoded MessagePack representation for the target type '{typeof(TValue).FullName}'.", ex);
        }
    }

    /// <inheritdoc />
    public object? DeserializeFromString(string data, Type type) {
        Preca.ThrowIfNullOrWhiteSpace(data);
        Preca.ThrowIfNull(type);
        try {
            byte[] bytes = Convert.FromBase64String(data);
            return MessagePackSerializer.Deserialize(type, bytes, options);
        }
        catch (Exception ex) when (ex is MessagePackSerializationException or FormatException) {
            throw new DeserializationFormatException($"The input string is not a valid Base64 encoded MessagePack representation for the target type '{type.FullName}'.", ex);
        }
    }

    /// <inheritdoc />
    public TValue? Deserialize<TValue>(byte[] data) {
        Preca.ThrowIfNull(data);
        try {
            return MessagePackSerializer.Deserialize<TValue>(data, options);
        }
        catch (MessagePackSerializationException ex) {
            throw new DeserializationFormatException($"The input byte array is not a valid MessagePack representation for the target type '{typeof(TValue).FullName}'.", ex);
        }
    }

    /// <inheritdoc />
    public object? Deserialize(byte[] data, Type type) {
        Preca.ThrowIfNull(data);
        Preca.ThrowIfNull(type);
        try {
            return MessagePackSerializer.Deserialize(type, data, options);
        }
        catch (MessagePackSerializationException ex) {
            throw new DeserializationFormatException($"The input byte array is not a valid MessagePack representation for the target type '{type.FullName}'.", ex);
        }
    }

    /// <inheritdoc />
    public TValue? Deserialize<TValue>(in ReadOnlySequence<byte> sequence) {
        try {
            return MessagePackSerializer.Deserialize<TValue>(sequence, options);
        }
        catch (MessagePackSerializationException ex) {
            throw new DeserializationFormatException($"The input byte sequence is not a valid MessagePack representation for the target type '{typeof(TValue).FullName}'.", ex);
        }
    }

    /// <inheritdoc />
    public object? Deserialize(in ReadOnlySequence<byte> sequence, Type type) {
        Preca.ThrowIfNull(type);
        try {
            return MessagePackSerializer.Deserialize(type, sequence, options);
        }
        catch (MessagePackSerializationException ex) {
            throw new DeserializationFormatException($"The input byte sequence is not a valid MessagePack representation for the target type '{type.FullName}'.", ex);
        }
    }

    // --- Try... Methods ---

    /// <inheritdoc />
    public bool TryDeserializeFromString<TValue>(string data, out TValue? result) {
        // No need for Preca check here, the main method already does it.
        try {
            result = DeserializeFromString<TValue>(data);
            return true;
        }
        catch (WiaojSerializationException) {
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
        catch (WiaojSerializationException) {
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
        catch (WiaojSerializationException) {
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
            await MessagePackSerializer.SerializeAsync(stream, value, options, cancellationToken);
        }
        catch (MessagePackSerializationException ex) {
            throw new UnsupportedTypeException($"The type '{value.GetType().FullName}' is not supported by the async MessagePack serializer.", ex);
        }
    }

    /// <inheritdoc />
    public async Task SerializeAsync(Stream stream, object value, Type type, CancellationToken cancellationToken) {
        Preca.ThrowIfNull(stream);
        Preca.ThrowIfNull(value);
        Preca.ThrowIfNull(type);
        try {
            await MessagePackSerializer.SerializeAsync(type, stream, value, options, cancellationToken);
        }
        catch (MessagePackSerializationException ex) {
            throw new UnsupportedTypeException($"The type '{type.FullName}' is not supported by the async MessagePack serializer.", ex);
        }
    }

    /// <summary>
    /// (Feature-parity with System.Text.Json) Asynchronously serializes a sequence of values.
    /// </summary>
    public async Task SerializeAsync<TValue>(Stream stream, IAsyncEnumerable<TValue> values, CancellationToken cancellationToken) {
        Preca.ThrowIfNull(stream);
        Preca.ThrowIfNull(values);
        try {
            await MessagePackSerializer.SerializeAsync(stream, values, options, cancellationToken);
        }
        catch (MessagePackSerializationException ex) {
            throw new UnsupportedTypeException($"The async enumerable of type '{typeof(TValue).FullName}' is not supported by the MessagePack serializer.", ex);
        }
    }

    /// <inheritdoc />
    public async ValueTask<TValue?> DeserializeAsync<TValue>(Stream stream, CancellationToken cancellationToken) {
        Preca.ThrowIfNull(stream);
        try {
            return await MessagePackSerializer.DeserializeAsync<TValue>(stream, options, cancellationToken);
        }
        catch (MessagePackSerializationException ex) {
            throw new DeserializationFormatException($"The input stream does not contain a valid MessagePack representation for the target type '{typeof(TValue).FullName}'.", ex);
        }
    }

    /// <inheritdoc />
    public async ValueTask<object?> DeserializeAsync(Stream stream, Type type, CancellationToken cancellationToken) {
        Preca.ThrowIfNull(stream);
        Preca.ThrowIfNull(type);
        try {
            return await MessagePackSerializer.DeserializeAsync(type, stream, options, cancellationToken);
        }
        catch (MessagePackSerializationException ex) {
            throw new DeserializationFormatException($"The input stream does not contain a valid MessagePack representation for the target type '{type.FullName}'.", ex);
        }
    }

    /// <inheritdoc />
    public async ValueTask<(bool Success, TValue? Value)> TryDeserializeAsync<TValue>(Stream stream, CancellationToken cancellationToken) {
        Preca.ThrowIfNull(stream);
        try {
            TValue? value = await DeserializeAsync<TValue>(stream, cancellationToken);
            return (true, value);
        }
        catch (WiaojSerializationException) {
            return (false, default);
        }
    }
}