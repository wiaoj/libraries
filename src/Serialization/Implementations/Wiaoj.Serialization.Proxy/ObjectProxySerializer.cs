using System.Buffers;
using System.Buffers.Binary;
using Wiaoj.Preconditions;

namespace Wiaoj.Serialization.Proxy;

public sealed class ObjectProxySerializer<TKey>(ObjectProxyRegistry registry) : ISerializer<TKey> where TKey : ISerializerKey {

    // --- SERIALIZE (Dolaba koy, ID'yi gönder) ---

    public byte[] Serialize<TValue>(TValue value) {
        Preca.ThrowIfNull(value);
        long id = registry.Register(value);
        byte[] buffer = new byte[sizeof(long)];
        BinaryPrimitives.WriteInt64LittleEndian(buffer, id);
        return buffer;
    }

    public void Serialize<TValue>(IBufferWriter<byte> writer, TValue value) {
        Preca.ThrowIfNull(writer);
        Preca.ThrowIfNull(value);
        long id = registry.Register(value);
        Span<byte> span = writer.GetSpan(sizeof(long));
        BinaryPrimitives.WriteInt64LittleEndian(span, id);
        writer.Advance(sizeof(long));
    }

    public string SerializeToString<TValue>(TValue value) {
        Preca.ThrowIfNull(value);
        return registry.Register(value).ToString();
    }

    public string SerializeToString<TValue>(TValue value, Type type) {
        Preca.ThrowIfNull(value);
        Preca.ThrowIfNull(type);
        return registry.Register(value).ToString();
    }

    // --- DESERIALIZE (ID'yi al, dolaptan orijinali çıkar) ---

    public TValue? Deserialize<TValue>(byte[] data) {
        Preca.ThrowIfNull(data);
        if(data.Length < sizeof(long)) return default;

        long id = BinaryPrimitives.ReadInt64LittleEndian(data);
        object? obj = registry.Get(id);
        return obj is TValue value ? value : default;
    }

    public object? Deserialize(byte[] data, Type type) {
        Preca.ThrowIfNull(data);
        Preca.ThrowIfNull(type);
        if(data.Length < sizeof(long)) return default;
        long id = BinaryPrimitives.ReadInt64LittleEndian(data);
        return registry.Get(id);
    }

    public TValue? Deserialize<TValue>(in ReadOnlySequence<byte> sequence) {
        if(sequence.Length < sizeof(long)) return default;

        Span<byte> buffer = stackalloc byte[sizeof(long)];
        sequence.Slice(0, sizeof(long)).CopyTo(buffer);
        long id = BinaryPrimitives.ReadInt64LittleEndian(buffer);

        object? obj = registry.Get(id);
        return obj is TValue value ? value : default;
    }

    public object? Deserialize(in ReadOnlySequence<byte> sequence, Type type) {
        Preca.ThrowIfNull(type);
        if(sequence.Length < sizeof(long)) return default;
        Span<byte> buffer = stackalloc byte[sizeof(long)];
        sequence.Slice(0, sizeof(long)).CopyTo(buffer);
        long id = BinaryPrimitives.ReadInt64LittleEndian(buffer);
        return registry.Get(id);
    }

    public TValue? DeserializeFromString<TValue>(string data) {
        Preca.ThrowIfNullOrWhiteSpace(data);
        if(!long.TryParse(data, out long id)) return default;

        object? obj = registry.Get(id);
        return obj is TValue value ? value : default;
    }

    public object? DeserializeFromString(string data, Type type) {
        Preca.ThrowIfNullOrWhiteSpace(data);
        Preca.ThrowIfNull(type);
        return long.TryParse(data, out long id)
            ? registry.Get(id)
            : default;
    }

    // --- TRY METHODS ---

    public bool TryDeserializeFromString<TValue>(string data, out TValue? result) {
        if(long.TryParse(data, out long id)) {
            result = (TValue?)registry.Get(id);
            return result is not null;
        }
        result = default;
        return false;
    }

    public bool TryDeserialize<TValue>(byte[] data, out TValue? result) {
        if(data != null && data.Length >= sizeof(long)) {
            long id = BinaryPrimitives.ReadInt64LittleEndian(data);
            result = (TValue?)registry.Get(id);
            return result is not null;
        }
        result = default;
        return false;
    }

    public bool TryDeserialize<TValue>(in ReadOnlySequence<byte> sequence, out TValue? result) {
        if(sequence.Length >= sizeof(long)) {
            Span<byte> buffer = stackalloc byte[sizeof(long)];
            sequence.Slice(0, sizeof(long)).CopyTo(buffer);
            long id = BinaryPrimitives.ReadInt64LittleEndian(buffer);
            result = (TValue?)registry.Get(id);
            return result is not null;
        }
        result = default;
        return false;
    }

    // --- ASYNC METHODS (In-process olduğu için senkron gibi davranır) --- 
    public async Task SerializeAsync<TValue>(Stream stream, TValue value, CancellationToken cancellationToken = default) {
        Preca.ThrowIfNull(stream);
        Preca.ThrowIfNull(value);
        long id = registry.Register(value);

        // Havuzdan bellek kiralıyoruz (Allocation yok)
        byte[] buffer = ArrayPool<byte>.Shared.Rent(sizeof(long));
        try {
            BinaryPrimitives.WriteInt64LittleEndian(buffer, id);
            await stream.WriteAsync(buffer.AsMemory(0, sizeof(long)), cancellationToken).ConfigureAwait(false);
        }
        finally {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    public async Task SerializeAsync(Stream stream, object value, Type type, CancellationToken cancellationToken = default) {
        Preca.ThrowIfNull(stream);
        Preca.ThrowIfNull(value);
        Preca.ThrowIfNull(type);
        long id = registry.Register(value);

        byte[] buffer = ArrayPool<byte>.Shared.Rent(sizeof(long));
        try {
            BinaryPrimitives.WriteInt64LittleEndian(buffer, id);
            await stream.WriteAsync(buffer.AsMemory(0, sizeof(long)), cancellationToken).ConfigureAwait(false);
        }
        finally {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    public async ValueTask<TValue?> DeserializeAsync<TValue>(Stream stream, CancellationToken cancellationToken = default) {
        Preca.ThrowIfNull(stream);

        byte[] buffer = ArrayPool<byte>.Shared.Rent(sizeof(long));
        try {
            await stream.ReadExactlyAsync(buffer.AsMemory(0, sizeof(long)), cancellationToken).ConfigureAwait(false);
            long id = BinaryPrimitives.ReadInt64LittleEndian(buffer);
            object? obj = registry.Get(id);
            return obj is TValue value ? value : default;
        }
        finally {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    public async ValueTask<object?> DeserializeAsync(Stream stream, Type type, CancellationToken cancellationToken = default) {
        Preca.ThrowIfNull(stream);
        Preca.ThrowIfNull(type);
        byte[] buffer = ArrayPool<byte>.Shared.Rent(sizeof(long));
        try {
            await stream.ReadExactlyAsync(buffer, cancellationToken).ConfigureAwait(false);
            long id = BinaryPrimitives.ReadInt64LittleEndian(buffer);
            return registry.Get(id);
        }
        finally {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    public async ValueTask<(bool Success, TValue? Value)> TryDeserializeAsync<TValue>(Stream stream,
                                                                                      CancellationToken cancellationToken = default) {
        try {
            TValue? result = await DeserializeAsync<TValue>(stream, cancellationToken).ConfigureAwait(false);
            return (true, result);
        }
        catch {
            return (false, default);
        }
    }
}