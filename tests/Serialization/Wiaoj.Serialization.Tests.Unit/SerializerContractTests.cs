using System.Buffers;
using System.Text;
using Microsoft.Extensions.DependencyInjection;

namespace Wiaoj.Serialization.Tests.Unit;

/// <summary>
/// What every <see cref="ISerializer"/> in the family must do, run once per format. Each format is registered the way
/// applications register it — <c>AddWiaojSerializer(s =&gt; s.UseX&lt;TKey&gt;())</c> — so its default options are
/// covered too.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "SerializerContract")]
public abstract class SerializerContractTests : IDisposable {
    private readonly ServiceProvider _provider;

    protected SerializerContractTests() {
        ServiceCollection services = new();
        services.AddWiaojSerializer(this.Register);
        this._provider = services.BuildServiceProvider();
        this.Serializer = this._provider.GetRequiredService<ISerializer<ContractKey>>();
    }

    /// <summary>Registers the serializer under test for <see cref="ContractKey"/>.</summary>
    protected abstract void Register(ISerializationBuilder builder);

    protected ISerializer<ContractKey> Serializer { get; }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>An unterminated JSON/YAML object; for the binary formats, a length or count larger than the input.</summary>
    private static byte[] Malformed => Encoding.UTF8.GetBytes("{not valid");

    [Fact]
    public void Bytes_RoundTrip() {
        Order order = Order.Sample();

        Order? back = this.Serializer.Deserialize<Order>(this.Serializer.Serialize(order));

        Assert.Equivalent(order, back, strict: true);
    }

    [Fact]
    public void String_RoundTrip() {
        Order order = Order.Sample();

        Order? back = this.Serializer.DeserializeFromString<Order>(this.Serializer.SerializeToString(order));

        Assert.Equivalent(order, back, strict: true);
    }

    [Fact]
    public void BufferWriter_Writes_The_Same_Bytes_And_Reads_Back_From_A_Sequence() {
        Order order = Order.Sample();
        ArrayBufferWriter<byte> writer = new();

        this.Serializer.Serialize(writer, order);
        Order? back = this.Serializer.Deserialize<Order>(new ReadOnlySequence<byte>(writer.WrittenMemory));

        Assert.Equal(this.Serializer.Serialize(order), writer.WrittenSpan.ToArray());
        Assert.Equivalent(order, back, strict: true);
    }

    [Fact]
    public void MultiSegment_Sequence_RoundTrip() {
        Order order = Order.Sample();

        Order? back = this.Serializer.Deserialize<Order>(Segmented(this.Serializer.Serialize(order), parts: 3));

        Assert.Equivalent(order, back, strict: true);
    }

    [Fact]
    public void NonGeneric_Overloads_RoundTrip() {
        Order order = Order.Sample();
        byte[] bytes = this.Serializer.Serialize(order);
        string text = this.Serializer.SerializeToString(order, typeof(Order));

        Assert.Equivalent(order, this.Serializer.Deserialize(bytes, typeof(Order)), strict: true);
        Assert.Equivalent(order, this.Serializer.Deserialize(Segmented(bytes, parts: 2), typeof(Order)), strict: true);
        Assert.Equivalent(order, this.Serializer.DeserializeFromString(text, typeof(Order)), strict: true);
    }

    [Fact]
    public async Task Stream_RoundTrip() {
        Order order = Order.Sample();
        using MemoryStream stream = new();

        await this.Serializer.SerializeAsync(stream, order, Ct);
        stream.Position = 0;
        Order? back = await this.Serializer.DeserializeAsync<Order>(stream, Ct);

        Assert.Equivalent(order, back, strict: true);
    }

    [Fact]
    public async Task Stream_NonGeneric_RoundTrip() {
        Order order = Order.Sample();
        using MemoryStream stream = new();

        await this.Serializer.SerializeAsync(stream, order, typeof(Order), Ct);
        stream.Position = 0;
        object? back = await this.Serializer.DeserializeAsync(stream, typeof(Order), Ct);

        Assert.Equivalent(order, back, strict: true);
    }

    [Fact]
    public void Malformed_Bytes_Throw_A_Serialization_Exception() {
        Assert.ThrowsAny<WiaojSerializationException>(() => this.Serializer.Deserialize<Order>(Malformed));
    }

    [Fact]
    public async Task TryDeserializeAsync_Malformed_Returns_False() {
        using MemoryStream stream = new(Malformed);

        (bool success, Order? value) = await this.Serializer.TryDeserializeAsync<Order>(stream, Ct);

        Assert.False(success);
        Assert.Null(value);
    }

    [Fact]
    public async Task TryDeserializeAsync_Valid_Returns_The_Value() {
        Order order = Order.Sample();
        using MemoryStream stream = new(this.Serializer.Serialize(order));

        (bool success, Order? value) = await this.Serializer.TryDeserializeAsync<Order>(stream, Ct);

        Assert.True(success);
        Assert.Equivalent(order, value, strict: true);
    }

    private static ReadOnlySequence<byte> Segmented(byte[] bytes, int parts) {
        int size = Math.Max(1, bytes.Length / parts);
        Segment first = new(bytes.AsMemory(0, Math.Min(size, bytes.Length)));
        Segment last = first;
        for(int offset = size; offset < bytes.Length; offset += size) {
            last = last.Append(bytes.AsMemory(offset, Math.Min(size, bytes.Length - offset)));
        }

        return new ReadOnlySequence<byte>(first, 0, last, last.Memory.Length);
    }

    private sealed class Segment : ReadOnlySequenceSegment<byte> {
        public Segment(ReadOnlyMemory<byte> memory) => this.Memory = memory;

        public Segment Append(ReadOnlyMemory<byte> memory) {
            Segment next = new(memory) { RunningIndex = this.RunningIndex + this.Memory.Length };
            this.Next = next;
            return next;
        }
    }

    public void Dispose() {
        this._provider.Dispose();
        GC.SuppressFinalize(this);
    }
}
