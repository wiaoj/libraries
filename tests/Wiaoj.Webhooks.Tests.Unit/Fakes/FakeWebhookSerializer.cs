using Wiaoj.Serialization;
using Wiaoj.Webhooks.Tests.Unit.TestData;

namespace Wiaoj.Webhooks.Tests.Unit.Fakes;

/// <summary>
/// Test spy implementation of <see cref="ISerializer{TKey}"/> that captures serialization parameters
/// and returns stubbed payload strings for unit testing.
/// </summary>
internal sealed class FakeWebhookSerializer : ISerializer<WebhookSerializerKey> {
    private readonly string _result;

    public FakeWebhookSerializer() : this(WebhookTestConstants.PayloadJson) {
    }

    public FakeWebhookSerializer(string result) {
        this._result = result;
    }

    /// <summary>
    /// Captures the value and runtime type passed to the last serialization call.
    /// </summary>
    public (object? Value, Type? Type)? LastSerializedCall { get; private set; }

    public string SerializeToString<TValue>(TValue value, Type type) {
        this.LastSerializedCall = (value, type);
        return this._result;
    }

    public string SerializeToString<TValue>(TValue value) {
        return SerializeToString(value, typeof(TValue));
    }

    // 🌟 ReplayAsync ve Deserialization testleri için implementasyon:
    public object? DeserializeFromString(string data, Type type) {
        try {
            return Activator.CreateInstance(type);
        }
        catch {
            return null;
        }
    }

    public TValue? DeserializeFromString<TValue>(string data) {
        return (TValue?)DeserializeFromString(data, typeof(TValue));
    }

    // ── Diğer Kullanılmayan Metotlar ──
    public byte[] Serialize<TValue>(TValue value) {
        throw new NotImplementedException();
    }

    public void Serialize<TValue>(System.Buffers.IBufferWriter<byte> writer, TValue value) {
        throw new NotImplementedException();
    }

    public TValue? Deserialize<TValue>(byte[] data) {
        throw new NotImplementedException();
    }

    public object? Deserialize(byte[] data, Type type) {
        throw new NotImplementedException();
    }

    public TValue? Deserialize<TValue>(in System.Buffers.ReadOnlySequence<byte> sequence) {
        throw new NotImplementedException();
    }

    public object? Deserialize(in System.Buffers.ReadOnlySequence<byte> sequence, Type type) {
        throw new NotImplementedException();
    }

    public bool TryDeserializeFromString<TValue>(string data, out TValue? result) {
        result = DeserializeFromString<TValue>(data);
        return result is not null;
    }
    public bool TryDeserialize<TValue>(byte[] data, out TValue? result) {
        throw new NotImplementedException();
    }

    public bool TryDeserialize<TValue>(in System.Buffers.ReadOnlySequence<byte> sequence, out TValue? result) {
        throw new NotImplementedException();
    }

    public Task SerializeAsync<TValue>(Stream stream, TValue value, CancellationToken cancellationToken = default) {
        throw new NotImplementedException();
    }

    public Task SerializeAsync(Stream stream, object value, Type type, CancellationToken cancellationToken = default) {
        throw new NotImplementedException();
    }

    public ValueTask<TValue?> DeserializeAsync<TValue>(Stream stream, CancellationToken cancellationToken = default) {
        throw new NotImplementedException();
    }

    public ValueTask<object?> DeserializeAsync(Stream stream, Type type, CancellationToken cancellationToken = default) {
        throw new NotImplementedException();
    }

    public ValueTask<(bool Success, TValue? Value)> TryDeserializeAsync<TValue>(Stream stream, CancellationToken cancellationToken = default) {
        throw new NotImplementedException();
    }
}