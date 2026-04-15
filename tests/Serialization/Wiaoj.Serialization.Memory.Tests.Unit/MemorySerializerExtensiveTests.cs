using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Wiaoj.Serialization.Memory.Tests.Unit;

// --- DUMMY TİPLER VE STRUCT'LAR (TEST VERİLERİ İÇİN) ---

public class DummyKey : ISerializerKey { }

public struct SimpleStruct : IEquatable<SimpleStruct> {
    public int Id;
    public double Value;
    public bool Equals(SimpleStruct other) {
        return this.Id == other.Id && this.Value == other.Value;
    }
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct ComplexStruct : IEquatable<ComplexStruct> {
    public long Timestamp;
    public SimpleStruct Data;
    public float X, Y, Z;
    public bool Equals(ComplexStruct other) {
        return this.Timestamp == other.Timestamp && this.Data.Equals(other.Data) && this.X == other.X && this.Y == other.Y && this.Z == other.Z;
    }
}

public struct InvalidStruct {
    public int Id;
    public string ReferenceTypeNotAllowed; // Bu tip memory serializer'ı çöktürmeli (Bilinçli)
}

// --- TEST SINIFI ---

public sealed class MemorySerializerExtensiveTests {
    private readonly MemorySerializer<DummyKey> _serializer;

    public MemorySerializerExtensiveTests() {
        this._serializer = new MemorySerializer<DummyKey>();
    }

    #region 1. MemoryData (Çekirdek Motor) Testleri

    [Fact]
    public void MemoryData_Create_FromPrimitive_Works() {
        int value = 1024;
        MemoryData data = MemoryData.Create(value);

        Assert.Equal(4, data.Length);
        Assert.Equal(1024, data.ReadAs<int>());
    }

    [Fact]
    public void MemoryData_Create_FromStruct_Works() {
        ComplexStruct original = new() { Timestamp = 123456, X = 1.1f, Y = 2.2f, Z = 3.3f, Data = new SimpleStruct { Id = 5, Value = 10.5 } };
        MemoryData data = MemoryData.Create(original);

        var read = data.ReadAs<ComplexStruct>();
        Assert.True(original.Equals(read));
    }

    [Fact]
    public void MemoryData_Create_WithReferenceType_ThrowsInvalidOperationException() {
        InvalidStruct invalid = new() { Id = 1, ReferenceTypeNotAllowed = "Test" };
        Assert.Throws<InvalidOperationException>(() => MemoryData.Create(invalid));
    }

    [Fact]
    public void MemoryData_ReadAs_BufferTooSmall_ThrowsArgumentOutOfRangeException() {
        byte[] smallBuffer = new byte[2]; // int için 4 lazım
        MemoryData data = new(smallBuffer);
        Assert.Throws<ArgumentOutOfRangeException>(() => data.ReadAs<int>());
    }

    [Fact]
    public void MemoryData_ToString_ReturnsValidBase64() {
        int value = 42;
        MemoryData data = MemoryData.Create(value);
        string b64 = data.ToString();

        Assert.False(string.IsNullOrWhiteSpace(b64));
        byte[] decoded = Convert.FromBase64String(b64);
        Assert.Equal(4, decoded.Length);
    }

    [Fact]
    public void MemoryData_FromString_ReconstructsProperly() {
        double value = 3.14159;
        string base64 = MemoryData.Create(value).ToString();

        MemoryData data = MemoryData.FromString(base64);
        Assert.Equal(value, data.ReadAs<double>());
    }

    [Fact]
    public void MemoryData_ImplicitOperators_WorkCorrectly() {
        long value = 999999999;
        MemoryData data = MemoryData.Create(value);

        ReadOnlySpan<byte> span = data;
        ReadOnlyMemory<byte> memory = data;
        string str = data;

        Assert.Equal(8, span.Length);
        Assert.Equal(8, memory.Length);
        Assert.NotNull(str);
    }

    [Fact]
    public void MemoryData_Equality_WorksCorrectly() {
        int v1 = 100;
        int v2 = 100;
        int v3 = 200;

        MemoryData d1 = MemoryData.Create(v1);
        MemoryData d2 = MemoryData.Create(v2);
        MemoryData d3 = MemoryData.Create(v3);

        Assert.True(d1.Equals(d2));
        Assert.False(d1.Equals(d3));
        Assert.Equal(d1.GetHashCode(), d2.GetHashCode());
        Assert.NotEqual(d1.GetHashCode(), d3.GetHashCode());
    }

    [Fact]
    public void MemoryData_WriteTo_BufferWriter_AdvancesCorrectly() {
        ArrayBufferWriter<byte> writer = new();
        SimpleStruct value = new() { Id = 1, Value = 99.9 };

        MemoryData.WriteTo(writer, value);

        Assert.Equal(Unsafe.SizeOf<SimpleStruct>(), writer.WrittenCount);
        //Assert.Equal(12, writer.WrittenCount); // int(4) + double(8) = 12 bytes
        var readValue = MemoryMarshal.Read<SimpleStruct>(writer.WrittenSpan);
        Assert.Equal(value.Id, readValue.Id);
    }

    #endregion

    #region 2. MemorySerializer Senkron Serileştirme Testleri

    [Theory]
    [InlineData(byte.MaxValue)]
    [InlineData(short.MinValue)]
    [InlineData(int.MaxValue)]
    [InlineData(long.MinValue)]
    public void Serializer_Serialize_PrimitiveTypes_ReturnsCorrectByteArray<T>(T value) where T : unmanaged {
        byte[] data = this._serializer.Serialize(value);
        T deserialized = this._serializer.Deserialize<T>(data);
        Assert.Equal(value, deserialized);
    }

    [Fact]
    public void Serializer_SerializeToString_DeserializeFromString_Works() {
        SimpleStruct original = new() { Id = 77, Value = 77.77 };

        string str = this._serializer.SerializeToString(original);
        var deserialized = this._serializer.DeserializeFromString<SimpleStruct>(str);

        Assert.Equal(original, deserialized);
    }

    [Fact]
    public void Serializer_DeserializeFromString_InvalidBase64_ThrowsFormatException() {
        Assert.Throws<FormatException>(() => this._serializer.DeserializeFromString<int>("NotABase64String!!!"));
    }

    [Fact]
    public void Serializer_SerializeToBufferWriter_Works() {
        ArrayBufferWriter<byte> writer = new();
        int val = 555;

        this._serializer.Serialize(writer, val);

        Assert.Equal(4, writer.WrittenCount);
        Assert.Equal(555, this._serializer.Deserialize<int>(writer.WrittenSpan.ToArray()));
    }

    #endregion

    #region 3. ReadOnlySequence (Çoklu Segment / Chunking) Testleri

    [Fact]
    public void Serializer_Deserialize_Sequence_SingleSegment_Works() {
        SimpleStruct original = new() { Id = 1, Value = 2.2 };
        byte[] raw = this._serializer.Serialize(original);
        ReadOnlySequence<byte> sequence = new(raw);

        var result = this._serializer.Deserialize<SimpleStruct>(sequence);
        Assert.Equal(original, result);
    }

    [Fact]
    public void Serializer_Deserialize_Sequence_MultiSegment_Works() {
        ComplexStruct original = new() { Timestamp = 999, X = 1, Y = 2, Z = 3 };
        byte[] raw = this._serializer.Serialize(original);

        // Array'i manuel olarak iki parçaya bölüyoruz
        CustomMemorySegment segment1 = new(raw.AsMemory(0, raw.Length / 2));
        var segment2 = segment1.Append(raw.AsMemory(raw.Length / 2));
        ReadOnlySequence<byte> sequence = new(segment1, 0, segment2, segment2.Memory.Length);

        Assert.False(sequence.IsSingleSegment); // Çoklu segment olduğundan emin olalım

        var result = this._serializer.Deserialize<ComplexStruct>(sequence);
        Assert.Equal(original, result);
    }

    [Fact]
    public void Serializer_Deserialize_Sequence_TooSmall_ThrowsArgumentOutOfRangeException() {
        ReadOnlySequence<byte> sequence = new(new byte[1]); // int için çok küçük
        Assert.Throws<ArgumentOutOfRangeException>(() => this._serializer.Deserialize<int>(sequence));
    }

    #endregion

    #region 4. Non-Generic (Type Tabanlı) GC Pinning Testleri

    [Fact]
    public void Serializer_NonGeneric_Deserialize_FromByteArray_Works() {
        SimpleStruct original = new() { Id = 88, Value = 88.88 };
        byte[] raw = this._serializer.Serialize(original);

        object result = this._serializer.Deserialize(raw, typeof(SimpleStruct));

        Assert.NotNull(result);
        Assert.IsType<SimpleStruct>(result);
        Assert.Equal(original, (SimpleStruct)result);
    }

    [Fact]
    public void Serializer_NonGeneric_Deserialize_Sequence_Works() {
        SimpleStruct original = new() { Id = 12, Value = 34.56 };
        byte[] raw = this._serializer.Serialize(original);
        ReadOnlySequence<byte> sequence = new(raw);

        object result = this._serializer.Deserialize(sequence, typeof(SimpleStruct));
        Assert.Equal(original, (SimpleStruct)result);
    }

    [Fact]
    public void Serializer_NonGeneric_Deserialize_ArrayTooSmall_Throws() {
        byte[] raw = new byte[1];
        Assert.Throws<ArgumentOutOfRangeException>(() => this._serializer.Deserialize(raw, typeof(long)));
    }

    #endregion

    #region 5. Try* Metot Testleri

    [Fact]
    public void Serializer_TryDeserializeFromString_ReturnsTrue_OnSuccess() {
        string data = this._serializer.SerializeToString(12345);
        bool success = this._serializer.TryDeserializeFromString(data, out int result);

        Assert.True(success);
        Assert.Equal(12345, result);
    }

    [Fact]
    public void Serializer_TryDeserializeFromString_ReturnsFalse_OnFailure() {
        bool success = this._serializer.TryDeserializeFromString("BadData", out int result);
        Assert.False(success);
        Assert.Equal(0, result);
    }

    [Fact]
    public void Serializer_TryDeserialize_ByteArray_ReturnsTrue_OnSuccess() {
        byte[] raw = this._serializer.Serialize(99.9f);
        bool success = this._serializer.TryDeserialize(raw, out float result);
        Assert.True(success);
        Assert.Equal(99.9f, result);
    }

    [Fact]
    public void Serializer_TryDeserialize_ByteArray_ReturnsFalse_OnTooSmall() {
        bool success = this._serializer.TryDeserialize(new byte[1], out long result);
        Assert.False(success);
    }

    #endregion

    #region 6. Async & Stream Testleri

    [Fact]
    public async Task Serializer_SerializeAsync_DeserializeAsync_Works() {
        ComplexStruct original = new() { Timestamp = 101010, X = 5 };
        using MemoryStream ms = new();

        await this._serializer.SerializeAsync(ms, original, CancellationToken.None);

        ms.Position = 0; // Başa sar

        var result = await this._serializer.DeserializeAsync<ComplexStruct>(ms, CancellationToken.None);
        Assert.Equal(original, result);
    }

    [Fact]
    public async Task Serializer_TryDeserializeAsync_ReturnsTrue_OnSuccess() {
        using MemoryStream ms = new(this._serializer.Serialize(42));
        var (success, value) = await this._serializer.TryDeserializeAsync<int>(ms, CancellationToken.None);

        Assert.True(success);
        Assert.Equal(42, value);
    }

    [Fact]
    public async Task Serializer_DeserializeAsync_RespectsCancellationToken() {
        using MemoryStream ms = new(new byte[100]);
        CancellationTokenSource cts = new();
        cts.Cancel(); // Anında iptal et

        await Assert.ThrowsAsync<TaskCanceledException>(async () =>
            await this._serializer.DeserializeAsync<int>(ms, cts.Token));
    }

    #endregion

    #region 7. IAsyncEnumerable (Streaming) Testleri

    [Fact]
    public async Task Serializer_AsyncEnumerable_StreamMultipleItems_Works() {
        List<SimpleStruct> items = new() {
            new SimpleStruct { Id = 1, Value = 10.1 },
            new SimpleStruct { Id = 2, Value = 20.2 },
            new SimpleStruct { Id = 3, Value = 30.3 }
        };

        using MemoryStream ms = new();

        // Yaz
        await this._serializer.SerializeAsync(ms, ToAsyncEnumerable(items), CancellationToken.None);

        ms.Position = 0; // Başa sar

        // Oku
        List<SimpleStruct> results = new();
        await foreach(var item in this._serializer.DeserializeAsyncEnumerable<SimpleStruct>(ms)) {
            results.Add(item);
        }

        Assert.Equal(3, results.Count);
        Assert.Equal(items[0], results[0]);
        Assert.Equal(items[2], results[2]);
    }

    [Fact]
    public async Task Serializer_AsyncEnumerable_EmptyStream_YieldsNothing() {
        using MemoryStream ms = new(); // Boş stream
        List<int> results = new();

        await foreach(var item in this._serializer.DeserializeAsyncEnumerable<int>(ms)) {
            results.Add(item);
        }

        Assert.Empty(results);
    }

    // Listeyi IAsyncEnumerable'a çeviren yardımcı metot
    private async IAsyncEnumerable<T> ToAsyncEnumerable<T>(IEnumerable<T> source) {
        foreach(var item in source) {
            await Task.Yield(); // Asenkron simülasyonu
            yield return item;
        }
    }

    #endregion
}

// --- YARDIMCI SINIF: READONLYSEQUENCE (CHUNKING) SİMÜLASYONU ---
// Testlerde sequence'in çok parçalı olabilmesi için .NET içindeki ReadOnlySequenceSegment'i implemente ediyoruz.
internal class CustomMemorySegment : ReadOnlySequenceSegment<byte> {
    public CustomMemorySegment(ReadOnlyMemory<byte> memory) {
        this.Memory = memory;
    }

    public CustomMemorySegment Append(ReadOnlyMemory<byte> memory) {
        CustomMemorySegment segment = new(memory) {
            RunningIndex = this.RunningIndex + this.Memory.Length
        };
        this.Next = segment;
        return segment;
    }
}