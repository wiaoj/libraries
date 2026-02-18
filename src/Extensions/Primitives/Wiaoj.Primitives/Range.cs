using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Wiaoj.Primitives;
/// <summary>
/// Represents an inclusive interval [Min, Max] that ensures the start is always less than or equal to the end.
/// </summary>
/// <typeparam name="T">The type of value (e.g. <see cref="int"/>, <see cref="DateTime"/>, <see cref="SemVer"/>).</typeparam>
[DebuggerDisplay("[{Min}, {Max}]")]
[JsonConverter(typeof(RangeJsonConverterFactory))]
public readonly record struct Range<T> : IEquatable<Range<T>> where T : IComparable<T> {

    /// <summary>Gets the inclusive lower bound of the range.</summary>
    public T Min { get; }

    /// <summary>Gets the inclusive upper bound of the range.</summary>
    public T Max { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="Range{T}"/> struct.
    /// Automatically swaps values if <paramref name="min"/> is greater than <paramref name="max"/>.
    /// </summary>
    public Range(T min, T max) {
        if(min.CompareTo(max) > 0) {
            this.Min = max;
            this.Max = min;
        }
        else {
            this.Min = min;
            this.Max = max;
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Range{T}"/> struct using a standard C# <see cref="System.Range"/>.
    /// </summary>
    /// <param name="range">The range expression (e.g., <c>10..50</c>).</param>
    /// <remarks>
    /// <para>
    /// <strong>Type Constraint:</strong> This constructor is only valid when <typeparamref name="T"/> is <see cref="int"/>.
    /// Attempting to use it with other types will result in an <see cref="InvalidOperationException"/>.
    /// </para>
    /// <para>
    /// <strong>Performance:</strong> Uses <see cref="Unsafe.As{TFrom, TTo}"/> to perform zero-allocation type casting.
    /// </para>
    /// </remarks>
    /// <exception cref="InvalidOperationException">Thrown when <typeparamref name="T"/> is not <see cref="int"/>.</exception>
    /// <exception cref="ArgumentException">Thrown if the provided <paramref name="range"/> uses relative-from-end indices (e.g. <c>^1</c>).</exception>
    public Range(System.Range range) {
        if(typeof(T) != typeof(int)) {
            throw new InvalidOperationException($"Conversion from System.Range is only supported for Range<int>, not Range<{typeof(T).Name}>.");
        }

        // System.Range'in Index değerleri absolute (mutlak) olmalıdır, tersten (^1) olamaz.
        if(range.Start.IsFromEnd || range.End.IsFromEnd) {
            throw new ArgumentException("Domain Range does not support 'from-end' (^) indices. Use absolute values.");
        }

        int start = range.Start.Value;
        int end = range.End.Value;

        T min = Unsafe.As<int, T>(ref start);
        T max = Unsafe.As<int, T>(ref end);

        if(min.CompareTo(max) > 0) {
            this.Min = max;
            this.Max = min;
        }
        else {
            this.Min = min;
            this.Max = max;
        }
    }

    /// <summary>Determines whether the specified value is within the inclusive range.</summary>
    public bool Contains(T value) => value.CompareTo(this.Min) >= 0 && value.CompareTo(this.Max) <= 0;

    /// <summary>Determines whether the specified range is entirely contained within this range.</summary>
    public bool Contains(Range<T> other) => other.Min.CompareTo(this.Min) >= 0 && other.Max.CompareTo(this.Max) <= 0;

    /// <summary>Checks if this arange overlaps with another range.</summary>
    public bool Overlaps(Range<T> other) => this.Min.CompareTo(other.Max) <= 0 && this.Max.CompareTo(other.Min) >= 0;

    /// <summary>Returns the intersection of two ranges, or <see langword="null"/> if they do not overlap.</summary>
    public Range<T>? Intersect(Range<T> other) {
        if(!Overlaps(other)) return null;
        T newMin = this.Min.CompareTo(other.Min) > 0 ? this.Min : other.Min;
        T newMax = this.Max.CompareTo(other.Max) < 0 ? this.Max : other.Max;
        return new Range<T>(newMin, newMax);
    }

    /// <summary>Returns the smallest range that encompasses both ranges.</summary>
    public Range<T> Union(Range<T> other) {
        T newMin = this.Min.CompareTo(other.Min) < 0 ? this.Min : other.Min;
        T newMax = this.Max.CompareTo(other.Max) > 0 ? this.Max : other.Max;
        return new Range<T>(newMin, newMax);
    }

    public override string ToString() => $"[{this.Min}, {this.Max}]";

    /// <summary>Deconstructs the range into its Min and Max components.</summary>
    public void Deconstruct(out T min, out T max) {
        min = this.Min;
        max = this.Max;
    }
}

// --- JSON Converter Factory for Generics ---
public class RangeJsonConverterFactory : JsonConverterFactory {
    public override bool CanConvert(Type typeToConvert) {
        return typeToConvert.IsGenericType && typeToConvert.GetGenericTypeDefinition() == typeof(Range<>);
    }

    public override JsonConverter? CreateConverter(Type typeToConvert, JsonSerializerOptions options) {
        Type itemType = typeToConvert.GetGenericArguments()[0];
        return (JsonConverter)Activator.CreateInstance(
            typeof(RangeJsonConverter<>).MakeGenericType(itemType))!;
    }
}

public class RangeJsonConverter<T> : JsonConverter<Range<T>> where T : IComparable<T> {
    public override Range<T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        if(reader.TokenType != JsonTokenType.StartObject) throw new JsonException();

        T min = default!;
        T max = default!;
        bool minSet = false, maxSet = false;

        while(reader.Read()) {
            if(reader.TokenType == JsonTokenType.EndObject) break;
            if(reader.TokenType != JsonTokenType.PropertyName) throw new JsonException();

            string? prop = reader.GetString();
            reader.Read();

            if(string.Equals(prop, "Min", StringComparison.OrdinalIgnoreCase)) {
                min = JsonSerializer.Deserialize<T>(ref reader, options)!;
                minSet = true;
            }
            else if(string.Equals(prop, "Max", StringComparison.OrdinalIgnoreCase)) {
                max = JsonSerializer.Deserialize<T>(ref reader, options)!;
                maxSet = true;
            }
        }

        if(!minSet || !maxSet) throw new JsonException("Range must contain 'Min' and 'Max' properties.");
        return new Range<T>(min, max);
    }

    public override void Write(Utf8JsonWriter writer, Range<T> value, JsonSerializerOptions options) {
        writer.WriteStartObject();
        writer.WritePropertyName("Min");
        JsonSerializer.Serialize(writer, value.Min, options);
        writer.WritePropertyName("Max");
        JsonSerializer.Serialize(writer, value.Max, options);
        writer.WriteEndObject();
    }
}

public static class RangeExtensions {

    /// <summary>
    /// Generates a random value within the specified range using <see cref="Random.Shared"/>.
    /// </summary>
    /// <typeparam name="T">A type that implements <see cref="INumber{T}"/>.</typeparam>
    /// <remarks>
    /// Optimized with direct casting for <see cref="int"/> and <see cref="double"/> to avoid overhead.
    /// </remarks>
    public static T NextRandom<T>(this Range<T> range) where T : INumber<T> { 
        T minT = range.Min;
        T maxT = range.Max;

        if(typeof(T) == typeof(int)) { 
            int min = Unsafe.As<T, int>(ref minT);
            int max = Unsafe.As<T, int>(ref maxT);

            int result = Random.Shared.Next(min, max + 1);
            return Unsafe.As<int, T>(ref result);
        }

        if(typeof(T) == typeof(double)) {
            double min = Unsafe.As<T, double>(ref minT);
            double max = Unsafe.As<T, double>(ref maxT);

            double result = min + (Random.Shared.NextDouble() * (max - min));
            return Unsafe.As<double, T>(ref result);
        }

        // Diğer tipler için (long, decimal vb.) generic fallback
        double factor = Random.Shared.NextDouble();

        // INumber üzerinden aritmetik işlem (range.Max - range.Min)
        T diff = maxT - minT;

        // T değerini double'a, sonra sonucu tekrar T'ye güvenli çevirme
        return minT + T.CreateTruncating(double.CreateTruncating(diff) * factor);
    }

    /// <summary>
    /// Generates a random integer within the specified range using the provided <see cref="Random"/> instance.
    /// </summary>
    public static int Next(this Random rng, Range<int> range) {
        return rng.Next(range.Min, range.Max + 1);
    }
}