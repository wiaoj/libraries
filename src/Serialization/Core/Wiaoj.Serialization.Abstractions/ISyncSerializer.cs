using System.Buffers;

namespace Wiaoj.Serialization.Abstractions;
/// <summary>
/// Defines the core methods for synchronous object serialization and deserialization.
/// </summary>
public interface ISyncSerializer {
    /// <summary>
    /// Serializes the given object to a string.
    /// </summary>
    /// <typeparam name="TValue">The type of the object to serialize.</typeparam>
    /// <param name="value">The object to serialize.</param>
    /// <returns>The serialized string representation of the object.</returns>
    string SerializeToString<TValue>(TValue value);

    string SerializeToString<TValue>(TValue value, Type type);

    /// <summary>
    /// Serializes the given object to a byte array.
    /// </summary>
    /// <typeparam name="TValue">The type of the object to serialize.</typeparam>
    /// <param name="value">The object to serialize.</param>
    /// <returns>The serialized byte array representation of the object.</returns>
    byte[] Serialize<TValue>(TValue value);

    /// <summary>
    /// Serializes the given object to a buffer writer.
    /// </summary>
    /// <typeparam name="TValue">The type of the object to serialize.</typeparam>
    /// <param name="writer">The buffer writer to write the serialized data to.</param>
    /// <param name="value">The object to serialize.</param>
    /// <returns>The number of bytes written.</returns>
    void Serialize<TValue>(IBufferWriter<byte> writer, TValue value);

    /// <summary>
    /// Deserializes the given string to the specified type.
    /// </summary>
    /// <typeparam name="TValue">The type to deserialize to.</typeparam>
    /// <param name="data">The string data to deserialize.</param>
    /// <returns>The deserialized object.</returns>
    /// <exception cref="DeserializationFormatException">
    /// Thrown when the input string <paramref name="data"/> is not a valid representation for the target type.
    /// </exception>
    /// <exception cref="UnsupportedTypeException">
    /// Thrown when the type <typeparamref name="TValue"/> is not supported by this serializer.
    /// </exception>
    TValue? DeserializeFromString<TValue>(string data);

    /// <summary>
    /// Deserializes the given string to the specified type.
    /// </summary>
    /// <param name="type">The type to deserialize to.</param>
    /// <param name="data">The string data to deserialize.</param>
    /// <returns>The deserialized object.</returns>
    /// <exception cref="DeserializationFormatException">
    /// Thrown when the input string <paramref name="data"/> is not a valid representation for the target type.
    /// </exception>
    /// <exception cref="UnsupportedTypeException">
    /// Thrown when the type <paramref name="type"/> is not supported by this serializer.
    /// </exception>
    object? DeserializeFromString(string data, Type type);

    /// <summary>
    /// Deserializes the given byte array to the specified type.
    /// </summary>
    /// <typeparam name="TValue">The type to deserialize to.</typeparam>
    /// <param name="data">The byte array data to deserialize.</param>
    /// <returns>The deserialized object.</returns>
    /// <exception cref="DeserializationFormatException" /> 
    TValue? Deserialize<TValue>(byte[] data);

    /// <summary>
    /// Deserializes the given byte array to the specified runtime type.
    /// </summary>
    /// <param name="data">The byte array data to deserialize.</param>
    /// <param name="type">The type to deserialize to.</param>
    /// <returns>The deserialized object.</returns>
    object? Deserialize(byte[] data, Type type);

    /// <summary>
    /// Deserializes data from a sequence of bytes to the specified type.
    /// </summary>
    /// <typeparam name="TValue">The type to deserialize to.</typeparam>
    /// <param name="sequence">The sequence of bytes containing the data to deserialize.</param>
    /// <returns>The deserialized object.</returns>
    TValue? Deserialize<TValue>(in ReadOnlySequence<byte> sequence);

    /// <summary>
    /// Deserializes data from a sequence of bytes to the specified runtime type.
    /// </summary>
    /// <param name="sequence">The sequence of bytes containing the data to deserialize.</param>
    /// <param name="type">The type to deserialize to.</param>
    /// <returns>The deserialized object.</returns>
    object? Deserialize(in ReadOnlySequence<byte> sequence, Type type);

    /// <summary>
    /// Attempts to safely deserialize the given string to the specified type.
    /// </summary>
    /// <typeparam name="TValue">The type to deserialize to.</typeparam>
    /// <param name="data">The string data to deserialize.</param>
    /// <param name="result">When this method returns, contains the deserialized object if successful; otherwise, the default value for the type.</param>
    /// <returns>True if deserialization is successful; otherwise, false.</returns>
    bool TryDeserializeFromString<TValue>(string data, out TValue? result);

    /// <summary>
    /// Attempts to safely deserialize the given byte array to the specified type.
    /// </summary>
    /// <typeparam name="TValue">The type to deserialize to.</typeparam>
    /// <param name="data">The byte array data to deserialize.</param>
    /// <param name="result">When this method returns, contains the deserialized object if successful; otherwise, the default value for the type.</param>
    /// <returns>True if deserialization is successful; otherwise, false.</returns>
    bool TryDeserialize<TValue>(byte[] data, out TValue? result);

    /// <summary>
    /// Attempts to safely deserialize data from a sequence of bytes to the specified type.
    /// </summary>
    /// <typeparam name="TValue">The type to deserialize to.</typeparam>
    /// <param name="sequence">The sequence of bytes containing the data to deserialize.</param>
    /// <param name="result">When this method returns, contains the deserialized object if successful; otherwise, the default value for the type.</param>
    /// <returns>True if deserialization is successful; otherwise, false.</returns>
    bool TryDeserialize<TValue>(in ReadOnlySequence<byte> sequence, out TValue? result);
}
