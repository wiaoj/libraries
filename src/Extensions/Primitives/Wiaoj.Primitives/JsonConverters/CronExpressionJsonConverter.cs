using System.Buffers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Wiaoj.Primitives.JsonConverters; 
/// <inheritdoc/>
public sealed class CronExpressionJsonConverter : JsonConverter<CronExpression> {
    /// <inheritdoc/>
    public override CronExpression Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        if(reader.TokenType != JsonTokenType.String)
            throw new JsonException("Expected string token for CronExpression.");

        // Span-based zero allocation read if possible
        ReadOnlySpan<byte> span = reader.HasValueSequence ? reader.ValueSequence.ToArray() : reader.ValueSpan;
        if(CronExpression.TryParse(span, out CronExpression result))
            return result;

        throw new JsonException($"'{reader.GetString()}' is not a valid CronExpression.");
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, CronExpression value, JsonSerializerOptions options) {
        writer.WriteStringValue(value.Value);
    }
}