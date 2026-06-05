using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using Wiaoj.Primitives.Buffers;

namespace Wiaoj.Primitives.Collections;

/// <summary>
/// A factory class that creates JSON converters for <see cref="EquatableArray{T}"/> types.
/// </summary>
[RequiresDynamicCode("This factory uses MakeGenericType which is not compatible with Native AOT. Provide the converter explicitly in JsonSerializerOptions.")]
[RequiresUnreferencedCode("This factory uses Reflection which is not compatible with Trimming. Provide the converter explicitly in JsonSerializerOptions.")]
public sealed class EquatableArrayJsonConverterFactory : JsonConverterFactory {

    /// <summary>
    /// Determines whether the specified type can be converted to an <see cref="EquatableArray{T}"/>.
    /// </summary>
    /// <param name="typeToConvert">The type to check.</param>
    /// <returns><see langword="true"/> if the type is an <see cref="EquatableArray{T}"/>; otherwise, <see langword="false"/>.</returns>
    public override bool CanConvert(Type typeToConvert) {
        return typeToConvert.IsGenericType && typeToConvert.GetGenericTypeDefinition() == typeof(EquatableArray<>);
    }

    /// <summary>
    /// Creates a <see cref="JsonConverter"/> for the specified <see cref="EquatableArray{T}"/> type.
    /// </summary>
    /// <param name="typeToConvert">The type of the <see cref="EquatableArray{T}"/> to convert.</param>
    /// <param name="options">The serialization options to use.</param>
    /// <returns>A configured JSON converter for the specified type.</returns>
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(EquatableArrayJsonConverter<>))] 
    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options) {
        Type elementType = typeToConvert.GetGenericArguments()[0];

        JsonConverter elementConverter = options.GetConverter(elementType);

        return (JsonConverter)Activator.CreateInstance(
            typeof(EquatableArrayJsonConverter<>).MakeGenericType(elementType),
            elementConverter)!; 
    }
}

/// <summary>
/// Handles JSON serialization and deserialization for <see cref="EquatableArray{T}"/> collections.
/// This converter is fully Native AOT and Trim compatible, and utilizes a zero-allocation 
/// buffer strategy during deserialization.
/// </summary>
/// <typeparam name="T">The type of elements in the array.</typeparam>
/// <param name="elementConverter">The specific JSON converter for type <typeparamref name="T"/>, injected by the factory.</param>
/// <remarks>
/// Initializes a new instance of the converter using a primary constructor, taking the pre-resolved element converter from the factory.
/// </remarks>
public sealed class EquatableArrayJsonConverter<T>(JsonConverter<T> elementConverter) : JsonConverter<EquatableArray<T>> {

    /// <summary>
    /// Reads and converts the JSON array to an <see cref="EquatableArray{T}"/>.
    /// Utilizes a high-performance <see cref="ValueList{T}"/> to prevent unnecessary heap allocations during parsing.
    /// </summary>
    /// <param name="reader">The reader used to read the JSON data.</param>
    /// <param name="typeToConvert">The type to convert.</param>
    /// <param name="options">An object that specifies serialization options to use.</param>
    /// <returns>An <see cref="EquatableArray{T}"/> instance created from the JSON array.</returns>
    /// <exception cref="JsonException">Thrown when the JSON payload does not start with a valid array token.</exception>
    public override EquatableArray<T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        if(reader.TokenType != JsonTokenType.StartArray) {
            throw new JsonException($"Expected StartArray token, but got {reader.TokenType}.");
        }
         
        using ValueList<T> a = new();

        while(reader.Read() && reader.TokenType != JsonTokenType.EndArray) {
            T? element = elementConverter.Read(ref reader, typeof(T), options);
            a.Add(element!);
        }

        return EquatableArray.Create(a.AsSpan());
    }

    /// <summary>
    /// Writes a specified <see cref="EquatableArray{T}"/> instance as a JSON array.
    /// Bypasses LINQ and enumerators by iterating over a zero-allocation span.
    /// </summary>
    /// <param name="writer">The writer used to write the JSON data.</param>
    /// <param name="value">The <see cref="EquatableArray{T}"/> value to serialize.</param>
    /// <param name="options">An object that specifies serialization options to use.</param>
    public override void Write(Utf8JsonWriter writer, EquatableArray<T> value, JsonSerializerOptions options) {
        writer.WriteStartArray();

        // Iterating over Span prevents allocation of enumerators
        foreach(T item in value.AsSpan()) {
            elementConverter.Write(writer, item, options);
        }

        writer.WriteEndArray();
    }
}
