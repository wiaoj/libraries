using System;
using System.Buffers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Wiaoj.Primitives.JsonConverters;

/// <summary>
/// A custom <see cref="JsonConverter"/> for serializing and deserializing the <see cref="HexString"/> struct.
/// </summary>
public sealed class HexStringJsonConverter : JsonConverter<HexString> {
    /// <inheritdoc/>
    public override HexString Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        if(reader.TokenType == JsonTokenType.String) {
            if(reader.HasValueSequence) {
                long len = reader.ValueSequence.Length;
                if(len <= 256) {
                    Span<byte> stackSpan = stackalloc byte[(int)len];
                    reader.ValueSequence.CopyTo(stackSpan);
                    return HexString.Parse(stackSpan);
                }
                else {
                    byte[] rented = ArrayPool<byte>.Shared.Rent((int)len);
                    try {
                        Span<byte> span = rented.AsSpan(0, (int)len);
                        reader.ValueSequence.CopyTo(span);
                        return HexString.Parse(span);
                    }
                    finally {
                        ArrayPool<byte>.Shared.Return(rented);
                    }
                }
            }

            return HexString.Parse(reader.ValueSpan);
        }

        // Fallback for non-string or null tokens.
        string? value = reader.GetString();
        return value is null ? HexString.Empty : HexString.Parse(value);
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, HexString value, JsonSerializerOptions options) {
        writer.WriteStringValue(value.Value);
    }
}