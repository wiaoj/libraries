namespace Wiaoj.Serialization.Abstractions;
public interface ISerializer : IAsyncSerializer, ISyncSerializer;

/// <summary>
/// Provides a unified interface for both synchronous and asynchronous serialization and deserialization operations.
/// This interface is intended for use as the default (keyless) serializer in the system.
/// </summary>
/// <remarks>
/// Implementations should support both string and binary serialization, as well as stream-based asynchronous operations.
/// </remarks>
public interface ISerializer<TKey> : ISerializer where TKey : notnull, ISerializerKey;