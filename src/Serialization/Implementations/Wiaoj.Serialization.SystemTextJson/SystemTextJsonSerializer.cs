using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace Wiaoj.Serialization.SystemTextJson;

[UnconditionalSuppressMessage("ReflectionAnalysis", "IL2026:RequiresUnreferencedCode",
    Justification = "Native AOT and trimming compatibility are supported when JsonSerializerOptions is configured with a JsonSerializerContext or IJsonTypeInfoResolver.")]
[UnconditionalSuppressMessage("Aot", "IL3050:RequiresDynamicCode",
    Justification = "Native AOT and trimming compatibility are supported when JsonSerializerOptions is configured with a JsonSerializerContext or IJsonTypeInfoResolver.")]
public sealed class SystemTextJsonSerializer<TKey>
    : ISerializer<TKey>,
#pragma warning disable WS0001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
      IAsyncEnumerableSerializer<TKey> where TKey : ISerializerKey
#pragma warning restore WS0001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
{
    private readonly JsonSerializerOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="SystemTextJsonSerializer{TKey}"/> class using default <see cref="JsonSerializerOptions"/>.
    /// </summary>
    public SystemTextJsonSerializer() : this(new JsonSerializerOptions()) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="SystemTextJsonSerializer{TKey}"/> class with the specified <see cref="JsonSerializerOptions"/>.
    /// </summary>
    /// <param name="options">The <see cref="JsonSerializerOptions"/> to use.</param>
    public SystemTextJsonSerializer(JsonSerializerOptions options) {
        Preca.ThrowIfNull(options);
        this._options = options;
    }

    /// <summary>
    /// Gets the configured <see cref="JsonSerializerOptions"/> used by this serializer.
    /// </summary>
    public JsonSerializerOptions Options => this._options;

    /// <inheritdoc />  
    public string SerializeToString<TValue>(TValue value) {
        Preca.ThrowIfNull(value);
        try {
            // Use value.GetType() to correctly handle polymorphism (e.g., serializing a derived type via a base type variable).
            return JsonSerializer.Serialize(value, value.GetType(), this._options);
        }
        catch(NotSupportedException ex) {
            throw new UnsupportedTypeException($"The type '{value.GetType().FullName}' or one of its members is not configured for JSON serialization.", ex);
        }
    }

    /// <inheritdoc />
    public string SerializeToString<TValue>(TValue value, Type type) {
        Preca.ThrowIfNull(value);
        Preca.ThrowIfNull(type);
        try {
            return JsonSerializer.Serialize(value, type, this._options);
        }
        catch(NotSupportedException ex) {
            throw new UnsupportedTypeException($"The type '{type.FullName}' or one of its members is not configured for JSON serialization.", ex);
        }
    }

    /// <inheritdoc />
    public byte[] Serialize<TValue>(TValue value) {
        Preca.ThrowIfNull(value);
        try {
            return JsonSerializer.SerializeToUtf8Bytes(value, this._options);
        }
        catch(NotSupportedException ex) {
            throw new UnsupportedTypeException($"The type '{value.GetType().FullName}' or one of its members is not configured for JSON serialization.", ex);
        }
    }

    /// <inheritdoc />
    public void Serialize<TValue>(IBufferWriter<byte> writer, TValue value) {
        Preca.ThrowIfNull(writer);
        Preca.ThrowIfNull(value);
        try {
            using Utf8JsonWriter jsonWriter = new(writer);
            JsonSerializer.Serialize(jsonWriter, value, this._options);
        }
        catch(NotSupportedException ex) {
            throw new UnsupportedTypeException($"The type '{value.GetType().FullName}' or one of its members is not configured for JSON serialization.", ex);
        }
    }

    /// <inheritdoc />
    public TValue? DeserializeFromString<TValue>(string data) {
        Preca.ThrowIfNullOrWhiteSpace(data);
        try {
            return JsonSerializer.Deserialize<TValue>(data, this._options);
        }
        catch(JsonException ex) {
            throw new DeserializationFormatException($"The input string is not a valid JSON representation for the target type '{typeof(TValue).FullName}'.", ex);
        }
    }

    /// <inheritdoc />
    public object? DeserializeFromString(string data, Type type) {
        Preca.ThrowIfNullOrWhiteSpace(data);
        Preca.ThrowIfNull(type);
        try {
            return JsonSerializer.Deserialize(data, type, this._options);
        }
        catch(JsonException ex) {
            throw new DeserializationFormatException($"The input string is not a valid JSON representation for the target type '{type.FullName}'.", ex);
        }
    }

    /// <inheritdoc />
    public TValue? Deserialize<TValue>(byte[] data) {
        Preca.ThrowIfNull(data);
        try {
            return JsonSerializer.Deserialize<TValue>(data, this._options);
        }
        catch(JsonException ex) {
            throw new DeserializationFormatException($"The input byte array is not a valid JSON representation for the target type '{typeof(TValue).FullName}'.", ex);
        }
    }

    /// <inheritdoc />
    public object? Deserialize(byte[] data, Type type) {
        Preca.ThrowIfNull(data);
        Preca.ThrowIfNull(type);
        try {
            return JsonSerializer.Deserialize(data, type, this._options);
        }
        catch(JsonException ex) {
            throw new DeserializationFormatException($"The input byte array is not a valid JSON representation for the target type '{type.FullName}'.", ex);
        }
    }

    /// <inheritdoc />
    public TValue? Deserialize<TValue>(in ReadOnlySequence<byte> sequence) {
        try {
            Utf8JsonReader reader = new(sequence);
            return JsonSerializer.Deserialize<TValue>(ref reader, this._options);
        }
        catch(JsonException ex) {
            throw new DeserializationFormatException($"The input byte sequence is not a valid JSON representation for the target type '{typeof(TValue).FullName}'.", ex);
        }
    }

    /// <inheritdoc />
    public object? Deserialize(in ReadOnlySequence<byte> sequence, Type type) {
        Preca.ThrowIfNull(type);
        try {
            Utf8JsonReader reader = new(sequence);
            return JsonSerializer.Deserialize(ref reader, type, this._options);
        }
        catch(JsonException ex) {
            throw new DeserializationFormatException($"The input byte sequence is not a valid JSON representation for the target type '{type.FullName}'.", ex);
        }
    }

    // --- Try... Methods ---

    /// <inheritdoc />
    public bool TryDeserializeFromString<TValue>(string data, out TValue? result) {
        Preca.ThrowIfNullOrWhiteSpace(data);
        try {
            // This call will throw our wrapped WiaojSerializationException on failure.
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
        Preca.ThrowIfNull(data);
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

    /// <inheritdoc />
    public async Task SerializeAsync<TValue>(Stream stream, TValue value, CancellationToken cancellationToken) {
        Preca.ThrowIfNull(stream);
        Preca.ThrowIfNull(value);
        try {
            await JsonSerializer.SerializeAsync(stream, value, value.GetType(), this._options, cancellationToken);
        }
        catch(NotSupportedException ex) {
            throw new UnsupportedTypeException($"The type '{value.GetType().FullName}' is not supported by the async JSON serializer.", ex);
        }
    }

    /// <inheritdoc />
    public async Task SerializeAsync(Stream stream, object value, Type type, CancellationToken cancellationToken) {
        Preca.ThrowIfNull(stream);
        Preca.ThrowIfNull(value);
        Preca.ThrowIfNull(type);
        try {
            await JsonSerializer.SerializeAsync(stream, value, type, this._options, cancellationToken);
        }
        catch(NotSupportedException ex) {
            throw new UnsupportedTypeException($"The type '{type.FullName}' is not supported by the async JSON serializer.", ex);
        }
    }

    /// <inheritdoc />
    public async ValueTask<TValue?> DeserializeAsync<TValue>(Stream stream, CancellationToken cancellationToken) {
        Preca.ThrowIfNull(stream);
        try {
            return await JsonSerializer.DeserializeAsync<TValue>(stream, this._options, cancellationToken);
        }
        catch(JsonException ex) {
            throw new DeserializationFormatException($"The input stream does not contain a valid JSON representation for the target type '{typeof(TValue).FullName}'.", ex);
        }
    }

    /// <inheritdoc />
    public async ValueTask<object?> DeserializeAsync(Stream stream, Type type, CancellationToken cancellationToken) {
        Preca.ThrowIfNull(stream);
        Preca.ThrowIfNull(type);
        try {
            return await JsonSerializer.DeserializeAsync(stream, type, this._options, cancellationToken);
        }
        catch(JsonException ex) {
            throw new DeserializationFormatException($"The input stream does not contain a valid JSON representation for the target type '{type.FullName}'.", ex);
        }
        catch(NotSupportedException ex) {
            throw new UnsupportedTypeException($"The type '{type.FullName}' is not supported for async deserialization.", ex);
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

    /// <inheritdoc />
    public async Task SerializeAsync<TValue>(Stream stream, IAsyncEnumerable<TValue> values, CancellationToken cancellationToken) {
        Preca.ThrowIfNull(stream);
        Preca.ThrowIfNull(values);
        try {
            await JsonSerializer.SerializeAsync(stream, values, this._options, cancellationToken);
        }
        catch(NotSupportedException ex) {
            throw new UnsupportedTypeException($"The async enumerable of type '{typeof(TValue).FullName}' is not supported by the JSON serializer.", ex);
        }
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<TValue?> DeserializeAsyncEnumerable<TValue>(Stream stream, [EnumeratorCancellation] CancellationToken cancellationToken = default) {
        Preca.ThrowIfNull(stream);

        IAsyncEnumerable<TValue?> sourceEnumerable;
        try {
            sourceEnumerable = JsonSerializer.DeserializeAsyncEnumerable<TValue>(stream, this._options, cancellationToken);
        }
        catch(Exception ex) when(ex is JsonException or NotSupportedException) {
            throw new DeserializationFormatException("Failed to start deserializing the async JSON enumerable. The stream might be empty or malformed at the start.", ex);
        }

        await using IAsyncEnumerator<TValue?> enumerator = sourceEnumerable.GetAsyncEnumerator(cancellationToken);
        while(true) {
            try {
                if(!await enumerator.MoveNextAsync()) {
                    break;
                }
            }
            catch(JsonException ex) {
                throw new DeserializationFormatException($"The JSON stream became invalid while enumerating objects of type '{typeof(TValue).FullName}'.", ex);
            }

            yield return enumerator.Current;
        }
    }
}