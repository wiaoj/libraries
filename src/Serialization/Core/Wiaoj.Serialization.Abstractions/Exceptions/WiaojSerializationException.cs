namespace Wiaoj.Serialization;
/// <summary>
/// Represents errors that occur during Wiaoj serialization or deserialization operations.
/// This exception wraps the underlying serializer-specific exception.
/// </summary>
public class WiaojSerializationException : Exception {  
    public WiaojSerializationException(string message) : base(message) { }
    public WiaojSerializationException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// Thrown when the input data (string, byte array, or stream) is malformed and cannot be parsed
/// by the target serializer. This typically indicates a problem with the source data.
/// </summary>
public class DeserializationFormatException(string message, Exception innerException) : WiaojSerializationException(message, innerException);

/// <summary>
/// Thrown when a serializer encounters a type that it does not know how to handle,
/// or a type that is not configured for serialization. This is typically a developer configuration error.
/// </summary>
public class UnsupportedTypeException(string message, Exception innerException) : WiaojSerializationException(message, innerException);