using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Wiaoj.Primitives.Snowflake;

namespace Wiaoj.Primitives.JsonConverters;
/// <summary>
/// A factory for creating JSON converters for <see cref="Range{T}"/>.
/// This factory uses explicit mapping for all .NET numeric types to ensure Native AOT and Trimming compatibility.
/// </summary>
public sealed class RangeJsonConverterFactory : JsonConverterFactory {
    /// <inheritdoc/>
    public override bool CanConvert(Type typeToConvert) {
        return typeToConvert.IsGenericType && typeToConvert.GetGenericTypeDefinition() == typeof(Range<>);
    }

    /// <inheritdoc/>
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "The switch handles all known primitive numeric types explicitly without reflection.")]
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "The switch handles all known primitive numeric types explicitly without dynamic code generation.")]
    public override JsonConverter? CreateConverter(Type typeToConvert, JsonSerializerOptions options) {
        Type itemType = typeToConvert.GetGenericArguments()[0];

        return itemType switch {
            _ when itemType == typeof(int) => new RangeInt32Converter(),
            _ when itemType == typeof(long) => new RangeInt64Converter(),
            _ when itemType == typeof(double) => new RangeDoubleConverter(),
            _ when itemType == typeof(float) => new RangeSingleConverter(),
            _ when itemType == typeof(decimal) => new RangeDecimalConverter(),
            _ when itemType == typeof(byte) => new RangeByteConverter(),
            _ when itemType == typeof(uint) => new RangeUInt32Converter(),
            _ when itemType == typeof(ulong) => new RangeUInt64Converter(),
            _ when itemType == typeof(short) => new RangeInt16Converter(),
            _ when itemType == typeof(ushort) => new RangeUInt16Converter(),
            _ when itemType == typeof(sbyte) => new RangeSByteConverter(),
            _ when itemType == typeof(Int128) => new RangeInt128Converter(),
            _ when itemType == typeof(UInt128) => new RangeUInt128Converter(),
            _ when itemType == typeof(BigInteger) => new RangeBigIntegerConverter(),
            _ when itemType == typeof(Half) => new RangeHalfConverter(),
            _ when itemType == typeof(DateTime) => new RangeDateTimeConverter(),
            _ when itemType == typeof(DateTimeOffset) => new RangeDateTimeOffsetConverter(),
            _ when itemType == typeof(DateOnly) => new RangeDateOnlyConverter(),
            _ when itemType == typeof(TimeOnly) => new RangeTimeOnlyConverter(),
            _ when itemType == typeof(TimeSpan) => new RangeTimeSpanConverter(),
            _ when itemType == typeof(char) => new RangeCharConverter(),
            _ when itemType == typeof(Percentage) => new RangePercentageConverter(),
            _ when itemType == typeof(NanoId) => new RangeNanoIdConverter(),
            _ when itemType == typeof(UnixTimestamp) => new RangeUnixTimestampConverter(),
            _ when itemType == typeof(GuidV7) => new RangeGuidV7Converter(),
            _ when itemType == typeof(SemVer) => new RangeSemVerConverter(),
            _ when itemType == typeof(SnowflakeId) => new RangeSnowflakeIdConverter(),
            _ => CreateGenericConverter(itemType, options) 
        };
    }

    [RequiresDynamicCode("Generic fallback for non-numeric types requires dynamic code generation.")]
    [RequiresUnreferencedCode("Generic fallback for non-numeric types requires dynamic access to members.")]
    private static JsonConverter? CreateGenericConverter(Type itemType, JsonSerializerOptions options) {
        return (JsonConverter?)Activator.CreateInstance(typeof(RangeJsonConverter<>).MakeGenericType(itemType), [options]);
    }
}

/// <summary>
/// Provides a base implementation for high-performance Range converters.
/// </summary>
/// <typeparam name="T">The numeric type.</typeparam>
internal abstract class RangeConverterBase<T> : JsonConverter<Range<T>> where T : IComparable<T> {
    protected abstract T ReadValue(ref Utf8JsonReader reader);
    protected abstract void WriteValue(Utf8JsonWriter writer, T value);

    public override Range<T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        if(reader.TokenType != JsonTokenType.StartObject) throw new JsonException("Expected StartObject token.");
        T min = default!, max = default!;
        while(reader.Read() && reader.TokenType != JsonTokenType.EndObject) {
            if(reader.TokenType != JsonTokenType.PropertyName) continue;
            string prop = reader.GetString()!; reader.Read();
            if(prop == "Min") min = ReadValue(ref reader);
            else if(prop == "Max") max = ReadValue(ref reader);
        }
        return new Range<T>(min, max);
    }
    public override void Write(Utf8JsonWriter writer, Range<T> value, JsonSerializerOptions options) {
        writer.WriteStartObject();
        writer.WritePropertyName("Min"); WriteValue(writer, value.Min);
        writer.WritePropertyName("Max"); WriteValue(writer, value.Max);
        writer.WriteEndObject();
    }
}

// --- REFLECTION-FREE CONVERTERS ---
internal sealed class RangeInt32Converter : RangeConverterBase<int> {
    protected override int ReadValue(ref Utf8JsonReader r) {
        return r.GetInt32();
    }

    protected override void WriteValue(Utf8JsonWriter w, int v) {
        w.WriteNumberValue(v);
    }
}
internal sealed class RangeInt64Converter : RangeConverterBase<long> {
    protected override long ReadValue(ref Utf8JsonReader r) {
        return r.GetInt64();
    }

    protected override void WriteValue(Utf8JsonWriter w, long v) {
        w.WriteNumberValue(v);
    }
}
internal sealed class RangeDoubleConverter : RangeConverterBase<double> {
    protected override double ReadValue(ref Utf8JsonReader r) {
        return r.GetDouble();
    }

    protected override void WriteValue(Utf8JsonWriter w, double v) {
        w.WriteNumberValue(v);
    }
}
internal sealed class RangeSingleConverter : RangeConverterBase<float> {
    protected override float ReadValue(ref Utf8JsonReader r) {
        return r.GetSingle();
    }

    protected override void WriteValue(Utf8JsonWriter w, float v) {
        w.WriteNumberValue(v);
    }
}
internal sealed class RangeDecimalConverter : RangeConverterBase<decimal> {
    protected override decimal ReadValue(ref Utf8JsonReader r) {
        return r.GetDecimal();
    }

    protected override void WriteValue(Utf8JsonWriter w, decimal v) {
        w.WriteNumberValue(v);
    }
}
internal sealed class RangeByteConverter : RangeConverterBase<byte> {
    protected override byte ReadValue(ref Utf8JsonReader r) {
        return r.GetByte();
    }

    protected override void WriteValue(Utf8JsonWriter w, byte v) {
        w.WriteNumberValue(v);
    }
}
internal sealed class RangeUInt32Converter : RangeConverterBase<uint> {
    protected override uint ReadValue(ref Utf8JsonReader r) {
        return r.GetUInt32();
    }

    protected override void WriteValue(Utf8JsonWriter w, uint v) {
        w.WriteNumberValue(v);
    }
}
internal sealed class RangeUInt64Converter : RangeConverterBase<ulong> {
    protected override ulong ReadValue(ref Utf8JsonReader r) {
        return r.GetUInt64();
    }

    protected override void WriteValue(Utf8JsonWriter w, ulong v) {
        w.WriteNumberValue(v);
    }
}
internal sealed class RangeInt16Converter : RangeConverterBase<short> {
    protected override short ReadValue(ref Utf8JsonReader r) {
        return r.GetInt16();
    }

    protected override void WriteValue(Utf8JsonWriter w, short v) {
        w.WriteNumberValue(v);
    }
}
internal sealed class RangeUInt16Converter : RangeConverterBase<ushort> {
    protected override ushort ReadValue(ref Utf8JsonReader r) {
        return r.GetUInt16();
    }

    protected override void WriteValue(Utf8JsonWriter w, ushort v) {
        w.WriteNumberValue(v);
    }
}
internal sealed class RangeSByteConverter : RangeConverterBase<sbyte> {
    protected override sbyte ReadValue(ref Utf8JsonReader r) {
        return r.GetSByte();
    }

    protected override void WriteValue(Utf8JsonWriter w, sbyte v) {
        w.WriteNumberValue(v);
    }
}
internal sealed class RangeNIntConverter : RangeConverterBase<nint> {
    protected override nint ReadValue(ref Utf8JsonReader r) {
        return (nint)r.GetInt64();
    }

    protected override void WriteValue(Utf8JsonWriter w, nint v) {
        w.WriteNumberValue(v);
    }
}
internal sealed class RangeNUIntConverter : RangeConverterBase<nuint> {
    protected override nuint ReadValue(ref Utf8JsonReader r) {
        return (nuint)r.GetUInt64();
    }

    protected override void WriteValue(Utf8JsonWriter w, nuint v) {
        w.WriteNumberValue(v);
    }
}

// --- PARSE-BASED CONVERTERS (BigInt, Int128, Half) ---
internal sealed class RangeBigIntegerConverter : RangeConverterBase<BigInteger> {
    protected override BigInteger ReadValue(ref Utf8JsonReader r) {
        return BigInteger.Parse(r.GetString()!, CultureInfo.InvariantCulture);
    }

    protected override void WriteValue(Utf8JsonWriter w, BigInteger v) {
        w.WriteStringValue(v.ToString(CultureInfo.InvariantCulture));
    }
}
internal sealed class RangeInt128Converter : RangeConverterBase<Int128> {
    protected override Int128 ReadValue(ref Utf8JsonReader r) {
        return Int128.Parse(r.GetString()!, CultureInfo.InvariantCulture);
    }

    protected override void WriteValue(Utf8JsonWriter w, Int128 v) {
        w.WriteStringValue(v.ToString(CultureInfo.InvariantCulture));
    }
}
internal sealed class RangeUInt128Converter : RangeConverterBase<UInt128> {
    protected override UInt128 ReadValue(ref Utf8JsonReader r) {
        return UInt128.Parse(r.GetString()!, CultureInfo.InvariantCulture);
    }

    protected override void WriteValue(Utf8JsonWriter w, UInt128 v) {
        w.WriteStringValue(v.ToString(CultureInfo.InvariantCulture));
    }
}
internal sealed class RangeHalfConverter : RangeConverterBase<Half> {
    protected override Half ReadValue(ref Utf8JsonReader r) {
        return Half.Parse(r.GetString()!, CultureInfo.InvariantCulture);
    }

    protected override void WriteValue(Utf8JsonWriter w, Half v) {
        w.WriteStringValue(v.ToString(CultureInfo.InvariantCulture));
    }
}

internal sealed class RangeUnixTimestampConverter : RangeConverterBase<UnixTimestamp> {
    protected override UnixTimestamp ReadValue(ref Utf8JsonReader r) => UnixTimestamp.FromMilliseconds(r.GetInt64());
    protected override void WriteValue(Utf8JsonWriter w, UnixTimestamp v) => w.WriteNumberValue(v.TotalMilliseconds);
}

internal sealed class RangeGuidV7Converter : RangeConverterBase<GuidV7> {
    protected override GuidV7 ReadValue(ref Utf8JsonReader r) => GuidV7.Parse(r.GetString()!);
    protected override void WriteValue(Utf8JsonWriter w, GuidV7 v) => w.WriteStringValue(v.ToString());
}

internal sealed class RangeSemVerConverter : RangeConverterBase<SemVer> {
    protected override SemVer ReadValue(ref Utf8JsonReader r) => SemVer.Parse(r.GetString()!);
    protected override void WriteValue(Utf8JsonWriter w, SemVer v) => w.WriteStringValue(v.ToString());
}

internal sealed class RangeSnowflakeIdConverter : RangeConverterBase<SnowflakeId> {
    protected override SnowflakeId ReadValue(ref Utf8JsonReader r) => SnowflakeId.Parse(r.GetString()!);
    protected override void WriteValue(Utf8JsonWriter w, SnowflakeId v) => w.WriteStringValue(v.ToString());
}
internal sealed class RangeDateTimeConverter : RangeConverterBase<DateTime> {
    protected override DateTime ReadValue(ref Utf8JsonReader r) => r.GetDateTime();
    protected override void WriteValue(Utf8JsonWriter w, DateTime v) => w.WriteStringValue(v);
}

internal sealed class RangeDateTimeOffsetConverter : RangeConverterBase<DateTimeOffset> {
    protected override DateTimeOffset ReadValue(ref Utf8JsonReader r) => r.GetDateTimeOffset();
    protected override void WriteValue(Utf8JsonWriter w, DateTimeOffset v) => w.WriteStringValue(v);
}

internal sealed class RangeDateOnlyConverter : RangeConverterBase<DateOnly> {
    protected override DateOnly ReadValue(ref Utf8JsonReader r) => DateOnly.Parse(r.GetString()!);
    protected override void WriteValue(Utf8JsonWriter w, DateOnly v) => w.WriteStringValue(v.ToString("O"));
}

internal sealed class RangeTimeOnlyConverter : RangeConverterBase<TimeOnly> {
    protected override TimeOnly ReadValue(ref Utf8JsonReader r) => TimeOnly.Parse(r.GetString()!);
    protected override void WriteValue(Utf8JsonWriter w, TimeOnly v) => w.WriteStringValue(v.ToString("O"));
}

internal sealed class RangeTimeSpanConverter : RangeConverterBase<TimeSpan> {
    protected override TimeSpan ReadValue(ref Utf8JsonReader r) => TimeSpan.Parse(r.GetString()!);
    protected override void WriteValue(Utf8JsonWriter w, TimeSpan v) => w.WriteStringValue(v.ToString());
}

internal sealed class RangeCharConverter : RangeConverterBase<char> {
    protected override char ReadValue(ref Utf8JsonReader r) => r.GetString()![0];
    protected override void WriteValue(Utf8JsonWriter w, char v) => w.WriteStringValue(v.ToString());
}

internal sealed class RangePercentageConverter : RangeConverterBase<Percentage> {
    protected override Percentage ReadValue(ref Utf8JsonReader r) => Percentage.FromDouble(r.GetDouble());
    protected override void WriteValue(Utf8JsonWriter w, Percentage v) => w.WriteNumberValue(v.Value);
}

internal sealed class RangeNanoIdConverter : RangeConverterBase<NanoId> {
    protected override NanoId ReadValue(ref Utf8JsonReader r) => NanoId.Parse(r.GetString()!);
    protected override void WriteValue(Utf8JsonWriter w, NanoId v) => w.WriteStringValue(v.Value);
}

/// <summary>
/// A generic fallback JSON converter for <see cref="Range{T}"/> types that are not explicitly handled 
/// (e.g., custom <see cref="IComparable{T}"/> types like DateTime or SemVer).
/// </summary>
/// <typeparam name="T">The type of the range values.</typeparam>
/// <remarks>
/// Initializes a new instance of the <see cref="RangeJsonConverter{T}"/> class.
/// </remarks>
/// <param name="o">The <see cref="JsonSerializerOptions"/> to resolve the internal value converter.</param>
[method: RequiresDynamicCode("Uses reflection to obtain the converter for type T at runtime.")]
[method: RequiresUnreferencedCode("Uses reflection to obtain the converter for type T which may be trimmed.")]
public sealed class RangeJsonConverter<T>(JsonSerializerOptions o) : JsonConverter<Range<T>> where T : IComparable<T> {
    private readonly JsonConverter<T> _v = (JsonConverter<T>)o.GetConverter(typeof(T));

    /// <inheritdoc/>
    public override Range<T> Read(ref Utf8JsonReader r, Type t, JsonSerializerOptions o) {
        if(r.TokenType != JsonTokenType.StartObject) throw new JsonException("Expected StartObject token for Range.");
        T min = default!, max = default!;
        while(r.Read() && r.TokenType != JsonTokenType.EndObject) {
            if(r.TokenType != JsonTokenType.PropertyName) continue;
            string p = r.GetString()!; r.Read();
            if(p == "Min") min = this._v.Read(ref r, typeof(T), o)!;
            else if(p == "Max") max = this._v.Read(ref r, typeof(T), o)!;
        }
        return new Range<T>(min, max);
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter w, Range<T> v, JsonSerializerOptions o) {
        w.WriteStartObject();
        w.WritePropertyName("Min"); this._v.Write(w, v.Min, o);
        w.WritePropertyName("Max"); this._v.Write(w, v.Max, o);
        w.WriteEndObject();
    }
}