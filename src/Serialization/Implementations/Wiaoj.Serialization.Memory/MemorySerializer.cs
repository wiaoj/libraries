using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Wiaoj.Serialization.Memory;

/// <summary>
/// High-performance memory serializer for In-Process communication.
/// Uses MemoryData as the underlying zero-copy engine.
/// </summary>
public sealed class MemorySerializer<TKey> : ISerializer<TKey> where TKey : notnull, ISerializerKey {
    // --- STRING METOTLARI ---
    public string SerializeToString<TValue>(TValue value) {
        return MemoryData.Create(value).ToString();
    }

    public string SerializeToString<TValue>(TValue value, Type type) {
        return SerializeToString(value);
    }

    public TValue? DeserializeFromString<TValue>(string data) {
        return MemoryData.FromString(data).ReadAs<TValue>();
    }

    public object? DeserializeFromString(string data, Type type) {
        return Deserialize(Convert.FromBase64String(data), type);
    }

    // --- BYTE ARRAY / BUFFER WRITER METOTLARI ---
    public byte[] Serialize<TValue>(TValue value) {
        return MemoryData.Create(value).ToArray();
    }

    public void Serialize<TValue>(IBufferWriter<byte> writer, TValue value) {
        MemoryData.WriteTo(writer, in value);
    }

    public TValue? Deserialize<TValue>(byte[] data) {
        return new MemoryData(data).ReadAs<TValue>();
    }

    public TValue? Deserialize<TValue>(in ReadOnlySequence<byte> sequence) {
        if(sequence.IsSingleSegment)
            return new MemoryData(sequence.First).ReadAs<TValue>();

        // Sequence parçalıysa (Chunked), MemoryData'ya verebilmek için birleştiriyoruz
        int size = Unsafe.SizeOf<TValue>();
        byte[] buffer = ArrayPool<byte>.Shared.Rent(size);
        try {
            sequence.Slice(0, size).CopyTo(buffer);
            return new MemoryData(new ReadOnlyMemory<byte>(buffer, 0, size)).ReadAs<TValue>();
        }
        finally {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    // --- NON-GENERIC (TYPE) METOTLARI (Reflection Yerine GC Pinning) ---
    public object? Deserialize(byte[] data, Type type) {
        int size = Marshal.SizeOf(type);
        if(data.Length < size) throw new ArgumentOutOfRangeException(nameof(data));

        object boxed = RuntimeHelpers.GetUninitializedObject(type);
        GCHandle handle = GCHandle.Alloc(boxed, GCHandleType.Pinned);
        try {
            Marshal.Copy(data, 0, handle.AddrOfPinnedObject(), size);
            return boxed;
        }
        finally {
            handle.Free();
        }
    }

    public object? Deserialize(in ReadOnlySequence<byte> sequence, Type type) {
        int size = Marshal.SizeOf(type);
        byte[] buffer = ArrayPool<byte>.Shared.Rent(size);
        try {
            sequence.Slice(0, size).CopyTo(buffer);
            return Deserialize(buffer, type);
        }
        finally {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    // --- ASYNC & STREAM METOTLARI ---
    public async Task SerializeAsync<TValue>(Stream stream, TValue value, CancellationToken ct) {
        // MemoryData'dan gelen ReadOnlyMemory'yi direkt Stream'e yazıyoruz (Sıfır kopya)
        MemoryData data = MemoryData.Create(value);
        await stream.WriteAsync(data, ct).ConfigureAwait(false);
    }

    public Task SerializeAsync(Stream stream, object value, Type type, CancellationToken ct) {
        throw new NotSupportedException("Use generic version for unmanaged streaming.");
    }

    public async ValueTask<TValue?> DeserializeAsync<TValue>(Stream stream, CancellationToken ct) {
        int size = Unsafe.SizeOf<TValue>();
        byte[] buffer = ArrayPool<byte>.Shared.Rent(size);
        try {
            await stream.ReadExactlyAsync(new Memory<byte>(buffer, 0, size), ct).ConfigureAwait(false);
            return new MemoryData(new ReadOnlyMemory<byte>(buffer, 0, size)).ReadAs<TValue>();
        }
        finally {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    public ValueTask<object?> DeserializeAsync(Stream stream, Type type, CancellationToken ct) {
        throw new NotSupportedException();
    }

    // --- TRY METOTLARI ---
    public bool TryDeserializeFromString<TValue>(string data, out TValue? result) {
        try { result = DeserializeFromString<TValue>(data); return true; }
        catch { result = default; return false; }
    }

    public bool TryDeserialize<TValue>(byte[] data, out TValue? result) {
        if(data.Length >= Unsafe.SizeOf<TValue>()) {
            result = Deserialize<TValue>(data);
            return true;
        }
        result = default; return false;
    }

    public bool TryDeserialize<TValue>(in ReadOnlySequence<byte> sequence, out TValue? result) {
        if(sequence.Length >= Unsafe.SizeOf<TValue>()) {
            result = Deserialize<TValue>(in sequence);
            return true;
        }
        result = default; return false;
    }

    public async ValueTask<(bool Success, TValue? Value)> TryDeserializeAsync<TValue>(Stream stream, CancellationToken ct) {
        try {
            TValue? res = await DeserializeAsync<TValue>(stream, ct).ConfigureAwait(false);
            return (true, res);
        }
        catch { return (false, default); }
    }

    // --- IAsyncEnumerable (STREAMING) ---
    public async Task SerializeAsync<TValue>(Stream stream, IAsyncEnumerable<TValue> values, CancellationToken ct) {
        await foreach(TValue? value in values.WithCancellation(ct).ConfigureAwait(false)) {
            MemoryData data = MemoryData.Create(value);
            await stream.WriteAsync(data, ct).ConfigureAwait(false);
        }
    }

    public async IAsyncEnumerable<TValue?> DeserializeAsyncEnumerable<TValue>(Stream stream, [EnumeratorCancellation] CancellationToken ct = default) {
        int size = Unsafe.SizeOf<TValue>();
        byte[] buffer = ArrayPool<byte>.Shared.Rent(size);
        try {
            Memory<byte> memBuffer = new(buffer, 0, size);
            while(true) {
                int bytesRead = 0;
                while(bytesRead < size) {
                    int read = await stream.ReadAsync(memBuffer[bytesRead..], ct).ConfigureAwait(false);
                    if(read == 0) yield break; // EOF
                    bytesRead += read;
                }

                yield return new MemoryData(memBuffer).ReadAs<TValue>();
            }
        }
        finally {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
}