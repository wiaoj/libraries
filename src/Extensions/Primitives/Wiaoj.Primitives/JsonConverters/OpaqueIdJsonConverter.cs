using System.Buffers;
using System.ComponentModel;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Wiaoj.Primitives.Obfuscation;
using Wiaoj.Primitives.Snowflake;

namespace Wiaoj.Primitives.JsonConverters; 
/// <inheritdoc/>
public sealed class OpaqueIdJsonConverter : JsonConverter<OpaqueId> {
    /// <inheritdoc/>
    public override OpaqueId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        if(reader.TokenType == JsonTokenType.String) {
            ReadOnlySpan<byte> utf8Bytes = reader.HasValueSequence ? reader.ValueSequence.ToArray() : reader.ValueSpan;

            // Stackalloc ile char çevrimi
            int maxCharCount = Encoding.UTF8.GetMaxCharCount(utf8Bytes.Length);
            Span<char> chars = maxCharCount <= 256 ? stackalloc char[maxCharCount] : new char[maxCharCount];

            int charsWritten = Encoding.UTF8.GetChars(utf8Bytes, chars);

            if(OpaqueId.TryParse(chars[..charsWritten], out OpaqueId result)) {
                return result;
            }
        }
        return OpaqueId.Empty;
    }

    public override void Write(Utf8JsonWriter writer, OpaqueId value, JsonSerializerOptions options) {
        // 64-bit ID'ler ~11 char, 128-bit ID'ler ~22 char tutar. 
        // 64 char buffer fazlasıyla yeterli ve güvenlidir.
        Span<char> buffer = stackalloc char[64];

        if(value.TryFormat(buffer, out int written, default, default)) {
            writer.WriteStringValue(buffer[..written]);
        }
        else {
            // Buffer yetmediyse (bu imkansız olmalı ama güvenli düşüş) string'e çevir.
            writer.WriteStringValue(value.ToString());
        }
    }

    /// <inheritdoc/>
    public override OpaqueId ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        string? s = reader.GetString();
        return s is null ? OpaqueId.Empty : OpaqueId.Parse(s);
    }

    /// <inheritdoc/>
    public override void WriteAsPropertyName(Utf8JsonWriter writer, OpaqueId value, JsonSerializerOptions options) {
        Span<char> buffer = stackalloc char[64];
        if(value.TryFormat(buffer, out int written, default, default)) {
            writer.WritePropertyName(buffer[..written]);
        }
        else {
            writer.WritePropertyName(value.ToString());
        }
    }
}

/// <inheritdoc/>
public sealed class OpaqueIdTypeConverter : TypeConverter {
    /// <inheritdoc/>
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType) {
        return sourceType == typeof(string) ||
               sourceType == typeof(SnowflakeId) ||
               sourceType == typeof(Guid) ||
               sourceType == typeof(long) ||
               base.CanConvertFrom(context, sourceType);
    }

    /// <inheritdoc/>
    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value) {
        if(value is string s) return OpaqueId.Parse(s);
        if(value is SnowflakeId id) return new OpaqueId(id);
        if(value is Guid g) return new OpaqueId(g);
        if(value is long l) return new OpaqueId(l);
        return base.ConvertFrom(context, culture, value);
    }

    /// <inheritdoc/>
    public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType) {
        return destinationType == typeof(string) ||
               destinationType == typeof(SnowflakeId) ||
               destinationType == typeof(Guid) ||
               destinationType == typeof(long) ||
               base.CanConvertTo(context, destinationType);
    }

    /// <inheritdoc/>
    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType) {
        if(value is OpaqueId pid) {
            if(destinationType == typeof(string)) return pid.ToString();
            if(destinationType == typeof(SnowflakeId)) return pid.AsSnowflake();
            if(destinationType == typeof(Guid)) return pid.AsGuid();
            if(destinationType == typeof(long)) return (long)(ulong)pid.Value;
        }
        return base.ConvertTo(context, culture, value, destinationType);
    }
}