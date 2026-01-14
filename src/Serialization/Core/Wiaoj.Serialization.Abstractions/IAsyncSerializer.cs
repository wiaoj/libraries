namespace Wiaoj.Serialization;
/// <summary>
/// Defines the core methods for asynchronous object serialization and deserialization, typically for stream-based I/O operations.
/// </summary>
public interface IAsyncSerializer {
    /// <summary>
    /// Asynchronously serializes the given object to a stream.
    /// </summary>
    /// <typeparam name="TValue">The type of the object to serialize.</typeparam>
    /// <param name="stream">The target stream to write the serialized data to.</param>
    /// <param name="value">The object to serialize.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task SerializeAsync<TValue>(Stream stream, TValue value, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously serializes the given object of a known type to a stream.
    /// </summary>
    /// <param name="stream">The target stream to write the serialized data to.</param>
    /// <param name="value">The object to serialize.</param>
    /// <param name="type">The actual runtime type of the object to serialize.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task SerializeAsync(Stream stream, object value, Type type, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously deserializes data from a stream to the specified type.
    /// </summary>
    /// <typeparam name="TValue">The type to deserialize to.</typeparam>
    /// <param name="stream">The source stream containing the data to deserialize.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation, with the deserialized object as its result.</returns>
    ValueTask<TValue?> DeserializeAsync<TValue>(Stream stream, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously deserializes data from a stream to the specified runtime type.
    /// </summary>
    /// <param name="stream">The source stream containing the data to deserialize.</param>
    /// <param name="type">The type to deserialize to.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation, with the deserialized object as its result.</returns>
    ValueTask<object?> DeserializeAsync(Stream stream, Type type, CancellationToken cancellationToken = default);

    /// <summary>
    /// Attempts to safely deserialize data from a stream to the specified type asynchronously.
    /// </summary>
    /// <typeparam name="TValue">The type to deserialize to.</typeparam>
    /// <param name="stream">The source stream containing the data to deserialize.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation, with a tuple indicating success and the deserialized object.</returns>
    ValueTask<(bool Success, TValue? Value)> TryDeserializeAsync<TValue>(Stream stream, CancellationToken cancellationToken = default);
}