using System;
using System.Buffers;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Wiaoj.Primitives.Snowflake;

namespace Wiaoj.Primitives.Obfuscation;

public class PublicIdJsonConverter : JsonConverter<PublicId> {
    public override PublicId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        if(reader.TokenType == JsonTokenType.String) {
            ReadOnlySpan<byte> utf8Bytes = reader.HasValueSequence ? reader.ValueSequence.ToArray() : reader.ValueSpan;

            // Stackalloc ile char çevrimi
            int maxCharCount = Encoding.UTF8.GetMaxCharCount(utf8Bytes.Length);
            Span<char> chars = maxCharCount <= 256 ? stackalloc char[maxCharCount] : new char[maxCharCount];

            int charsWritten = Encoding.UTF8.GetChars(utf8Bytes, chars);

            if(PublicId.TryParse(chars[..charsWritten], out PublicId result)) {
                return result;
            }
        }
        return PublicId.Empty;
    }

    public override void Write(Utf8JsonWriter writer, PublicId value, JsonSerializerOptions options) {
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

    public override PublicId ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        string? s = reader.GetString();
        return s is null ? PublicId.Empty : PublicId.Parse(s);
    }

    public override void WriteAsPropertyName(Utf8JsonWriter writer, PublicId value, JsonSerializerOptions options) {
        Span<char> buffer = stackalloc char[64];
        if(value.TryFormat(buffer, out int written, default, default)) {
            writer.WritePropertyName(buffer[..written]);
        }
        else {
            writer.WritePropertyName(value.ToString());
        }
    }
}

public class PublicIdTypeConverter : TypeConverter {
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType) {
        return sourceType == typeof(string) ||
               sourceType == typeof(SnowflakeId) ||
               sourceType == typeof(Guid) ||
               sourceType == typeof(long) ||
               base.CanConvertFrom(context, sourceType);
    }

    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value) {
        if(value is string s) return PublicId.Parse(s);
        if(value is SnowflakeId id) return new PublicId(id);
        if(value is Guid g) return new PublicId(g);
        if(value is long l) return new PublicId(l);
        return base.ConvertFrom(context, culture, value);
    }

    public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType) {
        return destinationType == typeof(string) ||
               destinationType == typeof(SnowflakeId) ||
               destinationType == typeof(Guid) ||
               destinationType == typeof(long) ||
               base.CanConvertTo(context, destinationType);
    }

    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType) {
        if(value is PublicId pid) {
            if(destinationType == typeof(string)) return pid.ToString();
            if(destinationType == typeof(SnowflakeId)) return pid.AsSnowflake();
            if(destinationType == typeof(Guid)) return pid.AsGuid();
            if(destinationType == typeof(long)) return (long)(ulong)pid.Value;
        }
        return base.ConvertTo(context, culture, value, destinationType);
    }
}