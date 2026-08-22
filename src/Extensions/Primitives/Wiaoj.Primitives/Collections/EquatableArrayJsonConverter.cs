using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using Wiaoj.Primitives.Buffers;

namespace Wiaoj.Primitives.Collections;

/// <summary>
/// A factory class that creates custom JSON converters for generic <see cref="EquatableArray{T}"/> collections.
/// </summary>
/// <remarks>
/// Integrates with <see cref="System.Text.Json"/> to handle serialization and deserialization of immutable <see cref="EquatableArray{T}"/> 
/// structures as native JSON arrays.
/// </remarks>
public sealed class EquatableArrayJsonConverterFactory : JsonConverterFactory {

    /// <summary>
    /// Determines whether the specified type can be converted to an <see cref="EquatableArray{T}"/>.
    /// </summary>
    /// <param name="typeToConvert">The type to check.</param>
    /// <returns><see langword="true"/> if the type is a generic <see cref="EquatableArray{T}"/>; otherwise, <see langword="false"/>.</returns>
    public override bool CanConvert(Type typeToConvert) {
        return typeToConvert.IsGenericType &&
               typeToConvert.GetGenericTypeDefinition() == typeof(EquatableArray<>);
    }

    /// <summary>
    /// Creates a <see cref="JsonConverter"/> for the specified <see cref="EquatableArray{T}"/> type.
    /// </summary>
    /// <param name="typeToConvert">The generic <see cref="EquatableArray{T}"/> type to convert.</param>
    /// <param name="options">The serialization options to use.</param>
    /// <returns>A configured JSON converter instance for the specified type.</returns>
    [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2026:RequiresUnreferencedCode",
        Justification = "The generic type EquatableArray<T> is preserved as long as the element type T is preserved.")]
    [UnconditionalSuppressMessage("AotAnalysis", "IL3050:RequiresDynamicCode",
        Justification = "The generic type EquatableArray<T> is preserved as long as the element type T is preserved.")]
    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options) {
        Type elementType = typeToConvert.GetGenericArguments()[0];
        JsonConverter elementConverter = options.GetConverter(elementType);

        Type converterType = typeof(EquatableArrayJsonConverter<>).MakeGenericType(elementType);

        return (JsonConverter)Activator.CreateInstance(converterType, elementConverter)!;
    }
}

/// <summary>
/// Custom JSON converter for <see cref="EquatableArray{T}"/> that serializes and deserializes native JSON arrays with high-performance memory pooling.
/// </summary>
/// <typeparam name="T">The type of elements in the array.</typeparam>
/// <param name="elementConverter">The pre-resolved JSON converter for individual elements of type <typeparamref name="T"/>.</param>
public sealed class EquatableArrayJsonConverter<T>(JsonConverter<T> elementConverter) : JsonConverter<EquatableArray<T>> {

    /// <summary>
    /// Reads and converts a JSON array into an <see cref="EquatableArray{T}"/>.
    /// Utilizes a stack-allocated <see cref="ValueList{T}"/> buffer to eliminate heap allocations for arrays up to 32 elements.
    /// </summary>
    /// <param name="reader">The reader used to read the JSON payload.</param>
    /// <param name="typeToConvert">The target collection type.</param>
    /// <param name="options">Serialization options used for converting elements.</param>
    /// <returns>An <see cref="EquatableArray{T}"/> instance containing the deserialized elements.</returns>
    /// <exception cref="JsonException">Thrown when the JSON payload does not start with a valid array token or encounters malformed JSON.</exception>
    public override EquatableArray<T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        if(reader.TokenType == JsonTokenType.Null) {
            return [];
        }

        if(reader.TokenType != JsonTokenType.StartArray) {
            throw new JsonException($"Expected StartArray token, got {reader.TokenType}.");
        }

        // Rents from ArrayPool to prevent GC allocations during deserialization and returns on dispose
        using ValueList<T> buffer = new();

        while(reader.Read()) {
            if(reader.TokenType == JsonTokenType.EndArray) {
                return EquatableArray.Create(buffer.AsSpan());
            }

            T? item = elementConverter.Read(ref reader, typeof(T), options);
            if(item is not null) {
                buffer.Add(item);
            }
        }

        throw new JsonException("Unexpected end of JSON while reading EquatableArray.");
    }

    /// <summary>
    /// Writes a specified <see cref="EquatableArray{T}"/> instance as a native JSON array.
    /// Bypasses LINQ and enumerators by iterating over a direct <see cref="ReadOnlySpan{T}"/>.
    /// </summary>
    /// <param name="writer">The writer used to write the JSON data.</param>
    /// <param name="value">The <see cref="EquatableArray{T}"/> value to serialize.</param>
    /// <param name="options">Serialization options used for converting elements.</param>
    public override void Write(Utf8JsonWriter writer, EquatableArray<T> value, JsonSerializerOptions options) {
        writer.WriteStartArray();

        // Direct span iteration avoids enumerator allocation
        foreach(T item in value.AsSpan()) {
            elementConverter.Write(writer, item, options);
        }

        writer.WriteEndArray();
    }
}