using System.Buffers;
using System.Text.Json;
using Wiaoj.Serialization;
using Wiaoj.Webhooks;

namespace Wiaoj.Samples.Webhooks.Infrastructure;

public sealed class SampleJsonWebhookSerializer : ISerializer<WebhookSerializerKey> {
    private static readonly JsonSerializerOptions _options = new(JsonSerializerDefaults.Web);

    public string SerializeToString<TValue>(TValue value, Type type) =>
        JsonSerializer.Serialize(value, type, _options);

    public string SerializeToString<TValue>(TValue value) =>
        JsonSerializer.Serialize(value, _options);

    public byte[] Serialize<TValue>(TValue value) =>
        JsonSerializer.SerializeToUtf8Bytes(value, _options);

    public void Serialize<TValue>(IBufferWriter<byte> writer, TValue value) {
        using Utf8JsonWriter jsonWriter = new(writer);
        JsonSerializer.Serialize(jsonWriter, value, _options);
    }

    public TValue? DeserializeFromString<TValue>(string data) =>
        JsonSerializer.Deserialize<TValue>(data, _options);

    public object? DeserializeFromString(string data, Type type) =>
        JsonSerializer.Deserialize(data, type, _options);

    public TValue? Deserialize<TValue>(byte[] data) =>
        JsonSerializer.Deserialize<TValue>(data, _options);

    public object? Deserialize(byte[] data, Type type) =>
        JsonSerializer.Deserialize(data, type, _options);

    public TValue? Deserialize<TValue>(in ReadOnlySequence<byte> sequence) {
        Utf8JsonReader reader = new(sequence);
        return JsonSerializer.Deserialize<TValue>(ref reader, _options);
    }

    public object? Deserialize(in ReadOnlySequence<byte> sequence, Type type) {
        Utf8JsonReader reader = new(sequence);
        return JsonSerializer.Deserialize(ref reader, type, _options);
    }

    public bool TryDeserializeFromString<TValue>(string data, out TValue? result) {
        try {
            result = DeserializeFromString<TValue>(data);
            return true;
        }
        catch {
            result = default;
            return false;
        }
    }

    public bool TryDeserialize<TValue>(byte[] data, out TValue? result) {
        try {
            result = Deserialize<TValue>(data);
            return true;
        }
        catch {
            result = default;
            return false;
        }
    }

    public bool TryDeserialize<TValue>(in ReadOnlySequence<byte> sequence, out TValue? result) {
        try {
            result = Deserialize<TValue>(sequence);
            return true;
        }
        catch {
            result = default;
            return false;
        }
    }

    public Task SerializeAsync<TValue>(Stream stream, TValue value, CancellationToken cancellationToken = default) =>
        JsonSerializer.SerializeAsync(stream, value, _options, cancellationToken);

    public Task SerializeAsync(Stream stream, object value, Type type, CancellationToken cancellationToken = default) =>
        JsonSerializer.SerializeAsync(stream, value, type, _options, cancellationToken);

    public ValueTask<TValue?> DeserializeAsync<TValue>(Stream stream, CancellationToken cancellationToken = default) =>
        JsonSerializer.DeserializeAsync<TValue>(stream, _options, cancellationToken);

    public ValueTask<object?> DeserializeAsync(Stream stream, Type type, CancellationToken cancellationToken = default) =>
        JsonSerializer.DeserializeAsync(stream, type, _options, cancellationToken);

    public async ValueTask<(bool Success, TValue? Value)> TryDeserializeAsync<TValue>(Stream stream, CancellationToken cancellationToken = default) {
        try {
            TValue? value = await DeserializeAsync<TValue>(stream, cancellationToken);
            return (true, value);
        }
        catch {
            return (false, default);
        }
    }
}
