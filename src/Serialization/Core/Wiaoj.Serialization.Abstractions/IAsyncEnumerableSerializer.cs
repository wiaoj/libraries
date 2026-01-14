using System.Diagnostics.CodeAnalysis;

namespace Wiaoj.Serialization;

[Experimental("WS0001", UrlFormat = "This interface is experimental and may change in future versions.")]
public interface IAsyncEnumerableSerializer<TKey> : IAsyncEnumerableSerializer where TKey : notnull, ISerializerKey;
[Experimental("WS0001", UrlFormat = "This interface is experimental and may change in future versions.")]
public interface IAsyncEnumerableSerializer {
    /// <summary>
    /// Asynchronously serializes a sequence of values as a JSON array to a stream.
    /// This method is highly efficient as it streams the data without buffering the entire collection in memory.
    /// </summary>
    /// <typeparam name="TValue">The type of objects in the sequence.</typeparam>
    /// <param name="stream">The target stream to write the JSON array to.</param>
    /// <param name="values">The asynchronous sequence of objects to serialize.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous write operation.</returns>
    Task SerializeAsync<TValue>(Stream stream, IAsyncEnumerable<TValue> values, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously deserializes a JSON array from a stream and returns each item as an <see cref="IAsyncEnumerable{TValue}"/> for immediate processing.
    /// </summary>
    /// <typeparam name="TValue">The type of each item in the JSON array.</typeparam>
    /// <param name="stream">The source stream containing the JSON array.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>An asynchronous enumerable of deserialized items.</returns>
    IAsyncEnumerable<TValue?> DeserializeAsyncEnumerable<TValue>(Stream stream, CancellationToken cancellationToken = default);
}