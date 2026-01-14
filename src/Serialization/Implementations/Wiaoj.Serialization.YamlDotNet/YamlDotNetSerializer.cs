using System.Buffers;
using System.Text;
using YamlDotNet.Core;

using IYamlDeserializer = YamlDotNet.Serialization.IDeserializer;
using IYamlSerializer = YamlDotNet.Serialization.ISerializer;

namespace Wiaoj.Serialization.YamlDotNet;
/// <summary>
/// A concrete and complete implementation of ISerializer using the YamlDotNet library.
/// </summary>
/// <typeparam name="TKey">The key identifying this serializer instance.</typeparam>
public sealed class YamlDotNetSerializer<TKey>(IYamlSerializer serializer, IYamlDeserializer deserializer) : ISerializer<TKey>
    where TKey : ISerializerKey {
    private const int BufferSize = 1024;

    // --- Serialization Methods ---

    /// <inheritdoc />
    public string SerializeToString<TValue>(TValue value) {
        Preca.ThrowIfNull(value);
        try {
            return serializer.Serialize(value);
        }
        catch(YamlException ex) {
            throw new UnsupportedTypeException($"The type '{value.GetType().FullName}' or one of its members could not be serialized to YAML. See inner exception for details.", ex);
        }
    }

    /// <inheritdoc />
    public string SerializeToString<TValue>(TValue value, Type type) {
        Preca.ThrowIfNull(value);
        Preca.ThrowIfNull(type);
        try {
            return serializer.Serialize(value, type);
        }
        catch(YamlException ex) {
            throw new UnsupportedTypeException($"The type '{type.FullName}' or one of its members could not be serialized to YAML. See inner exception for details.", ex);
        }
    }

    /// <inheritdoc />
    public byte[] Serialize<TValue>(TValue value) {
        // This method relies on SerializeToString, which already has exception handling.
        string yamlString = SerializeToString(value);
        return Encoding.UTF8.GetBytes(yamlString);
    }

    /// <inheritdoc />
    public void Serialize<TValue>(IBufferWriter<byte> writer, TValue value) {
        Preca.ThrowIfNull(writer);
        Preca.ThrowIfNull(value);
        // This method relies on Serialize, which already has exception handling.
        byte[] bytes = Serialize(value);
        writer.Write(bytes);
    }

    // --- Deserialization Methods ---

    /// <inheritdoc />
    public TValue? DeserializeFromString<TValue>(string data) {
        Preca.ThrowIfNullOrWhiteSpace(data);
        try {
            return deserializer.Deserialize<TValue>(data);
        }
        catch(YamlException ex) {
            throw new DeserializationFormatException($"The input string is not a valid YAML representation for the target type '{typeof(TValue).FullName}'.", ex);
        }
    }

    /// <inheritdoc />
    public object? DeserializeFromString(string data, Type type) {
        Preca.ThrowIfNullOrWhiteSpace(data);
        Preca.ThrowIfNull(type);
        try {
            return deserializer.Deserialize(data, type);
        }
        catch(YamlException ex) {
            throw new DeserializationFormatException($"The input string is not a valid YAML representation for the target type '{type.FullName}'.", ex);
        }
    }

    /// <inheritdoc />
    public TValue? Deserialize<TValue>(byte[] data) {
        Preca.ThrowIfNull(data);
        // This relies on DeserializeFromString, which handles the exceptions.
        return DeserializeFromString<TValue>(Encoding.UTF8.GetString(data));
    }

    /// <inheritdoc />
    public object? Deserialize(byte[] data, Type type) {
        Preca.ThrowIfNull(data);
        Preca.ThrowIfNull(type);
        // This relies on DeserializeFromString, which handles the exceptions.
        return DeserializeFromString(Encoding.UTF8.GetString(data), type);
    }

    /// <inheritdoc />
    public TValue? Deserialize<TValue>(in ReadOnlySequence<byte> sequence) {
        // This relies on Deserialize methods that already handle exceptions.
        return sequence.IsSingleSegment
            ? DeserializeFromString<TValue>(Encoding.UTF8.GetString(sequence.FirstSpan))
            : Deserialize<TValue>(sequence.ToArray());
    }

    /// <inheritdoc />
    public object? Deserialize(in ReadOnlySequence<byte> sequence, Type type) {
        Preca.ThrowIfNull(type);
        // This relies on Deserialize methods that already handle exceptions.
        return sequence.IsSingleSegment
            ? DeserializeFromString(Encoding.UTF8.GetString(sequence.FirstSpan), type)
            : Deserialize(sequence.ToArray(), type);
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
        cancellationToken.ThrowIfCancellationRequested();

        try {
            // Use an intermediate writer to ensure the underlying stream is not closed.
            await using StreamWriter writer = new(stream, Encoding.UTF8, bufferSize: BufferSize, leaveOpen: true);
            serializer.Serialize(writer, value);
            await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        catch(YamlException ex) {
            throw new UnsupportedTypeException($"The type '{value.GetType().FullName}' could not be serialized to the stream as YAML.", ex);
        }
    }

    /// <inheritdoc />
    public async Task SerializeAsync(Stream stream, object value, Type type, CancellationToken cancellationToken) {
        Preca.ThrowIfNull(stream);
        Preca.ThrowIfNull(value);
        Preca.ThrowIfNull(type);
        cancellationToken.ThrowIfCancellationRequested();

        try {
            await using StreamWriter writer = new(stream, Encoding.UTF8, bufferSize: BufferSize, leaveOpen: true);
            serializer.Serialize(writer, value, type);
            await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        catch(YamlException ex) {
            throw new UnsupportedTypeException($"The type '{type.FullName}' could not be serialized to the stream as YAML.", ex);
        }
    }

    /// <inheritdoc />
    public async ValueTask<TValue?> DeserializeAsync<TValue>(Stream stream, CancellationToken cancellationToken) {
        Preca.ThrowIfNull(stream);
        cancellationToken.ThrowIfCancellationRequested();

        // NOTE: This implementation reads the entire stream into memory due to YamlDotNet API limitations.
        // It is not true streaming and may cause issues with very large files.
        try {
            using StreamReader reader = new(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: BufferSize, leaveOpen: true);
            string content = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
            return DeserializeFromString<TValue>(content);
        }
        catch(YamlException ex) // Catching exceptions from both ReadToEndAsync and DeserializeFromString
        {
            throw new DeserializationFormatException($"The input stream does not contain a valid YAML representation for the target type '{typeof(TValue).FullName}'.", ex);
        }
    }

    /// <inheritdoc />
    public async ValueTask<object?> DeserializeAsync(Stream stream, Type type, CancellationToken cancellationToken) {
        Preca.ThrowIfNull(stream);
        Preca.ThrowIfNull(type);
        cancellationToken.ThrowIfCancellationRequested();

        // NOTE: This implementation reads the entire stream into memory. See generic overload for details.
        try {
            using StreamReader reader = new(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: BufferSize, leaveOpen: true);
            string content = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
            return DeserializeFromString(content, type);
        }
        catch(YamlException ex) {
            throw new DeserializationFormatException($"The input stream does not contain a valid YAML representation for the target type '{type.FullName}'.", ex);
        }
    }

    /// <inheritdoc />
    public async ValueTask<(bool Success, TValue? Value)> TryDeserializeAsync<TValue>(Stream stream, CancellationToken cancellationToken) {
        Preca.ThrowIfNull(stream);
        try {
            TValue? result = await DeserializeAsync<TValue>(stream, cancellationToken);
            return (true, result);
        }
        catch(WiaojSerializationException) {
            return (false, default);
        }
    }
}