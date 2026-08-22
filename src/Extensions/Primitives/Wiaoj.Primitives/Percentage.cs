using System.Buffers.Text;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Wiaoj.Primitives.JsonConverters; // Ensure this namespace exists in your project

namespace Wiaoj.Primitives;

/// <summary>
/// Represents a percentage value as a double-precision floating-point number between 0.0 (0%) and 1.0 (100%).
/// This struct is immutable and designed for high-performance mathematical operations.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Parsing Behavior:</strong> This type expects raw decimal values during parsing. 
/// For example, to represent "50%", the input string must be "0.5". 
/// Input strings containing the percentage symbol (e.g., "50%") are <strong>not supported</strong> and will throw a <see cref="FormatException"/>.
/// </para>
/// <para>
/// <strong>Formatting:</strong> The <see cref="ToString()"/> method formats the value using the "P0" format (e.g., outputting "50%").
/// </para>
/// </remarks>
[DebuggerDisplay("{ToString(),nq}")]
[JsonConverter(typeof(PercentageJsonConverter))]
public readonly record struct Percentage :
    IEquatable<Percentage>,
    IComparable,
    IComparable<Percentage>,
    IFormattable,
    IParsable<Percentage>,
    ISpanParsable<Percentage>,
    ISpanFormattable,
    IUtf8SpanFormattable,
    IUtf8SpanParsable<Percentage>,
    IMinMaxValue<Percentage>,
    IComparisonOperators<Percentage, Percentage, bool>,
    IComparisonOperators<Percentage, double, bool>,
    IComparisonOperators<Percentage, int, bool>,
    IMultiplyOperators<Percentage, Percentage, Percentage>,
    IMultiplyOperators<Percentage, double, double>,
    IMultiplyOperators<Percentage, TimeSpan, TimeSpan>,
    IDivisionOperators<Percentage, Percentage, double>,
    IDivisionOperators<Percentage, double, Percentage> {
    #region Constants and Properties

    /// <summary>Represents a percentage of 0% (Value: 0.0).</summary>
    public static readonly Percentage Zero = new(0.0);

    /// <summary>Represents a percentage of 50% (Value: 0.5).</summary>
    public static readonly Percentage Half = new(0.5);

    /// <summary>Represents a percentage of 100% (Value: 1.0).</summary>
    public static readonly Percentage Full = new(1.0);

    /// <inheritdoc/>
    static Percentage IMinMaxValue<Percentage>.MinValue => Zero;

    /// <inheritdoc/>
    static Percentage IMinMaxValue<Percentage>.MaxValue => Full;

    /// <summary>
    /// Gets the underlying raw value of the percentage, ranging from 0.0 to 1.0.
    /// </summary>
    /// <value>A <see cref="double"/> value between 0.0 and 1.0.</value>
    public double Value { get; }

    /// <summary>
    /// Gets a value indicating whether this percentage represents zero (0%).
    /// </summary>
    public bool IsZero => this.Value is 0.0D;

    /// <summary>
    /// Gets a value indicating whether this percentage represents the whole (100%).
    /// </summary>
    public bool IsOne => this.Value is 1.0D;

    // Private constructor to enforce validation via factory methods
    private Percentage(double value) { this.Value = value; }

    #endregion

    #region Factory Methods

    /// <summary>
    /// Creates a <see cref="Percentage"/> from a raw double value between 0.0 and 1.0.
    /// </summary>
    /// <param name="value">The raw value (e.g., 0.25 for 25%).</param>
    /// <returns>A new <see cref="Percentage"/> instance.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="value"/> is less than 0.0, greater than 1.0, or NaN.</exception>
    public static Percentage FromDouble(double value) {
        if(double.IsNaN(value) || value < 0.0 || value > 1.0)
            throw new ArgumentOutOfRangeException(nameof(value), value, "Value must be a valid number between 0.0 (0%) and 1.0 (100%).");

        return new Percentage(value);
    }

    /// <summary>
    /// Creates a <see cref="Percentage"/> from an integer value between 0 and 100.
    /// </summary>
    /// <param name="value">The integer value (e.g., 25 for 25%).</param>
    /// <returns>A new <see cref="Percentage"/> instance.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="value"/> is less than 0 or greater than 100.</exception>
    public static Percentage FromInt(int value) {
        if(value < 0 || value > 100)
            throw new ArgumentOutOfRangeException(nameof(value), value, "Value must be between 0 and 100.");

        return new Percentage(value / 100.0);
    }

    #endregion

    #region Math Operations

    /// <summary>
    /// Adds another percentage to this one, clamping the result to a maximum of 1.0 (100%).
    /// </summary>
    /// <param name="other">The percentage to add.</param>
    /// <returns>A new <see cref="Percentage"/> representing the sum, capped at 100%.</returns>
    public Percentage AddClamped(Percentage other) {
        return new Percentage(Math.Min(this.Value + other.Value, 1.0));
    }

    /// <summary>
    /// Subtracts another percentage from this one, clamping the result to a minimum of 0.0 (0%).
    /// </summary>
    /// <param name="other">The percentage to subtract.</param>
    /// <returns>A new <see cref="Percentage"/> representing the difference, floored at 0%.</returns>
    public Percentage SubtractClamped(Percentage other) {
        return new Percentage(Math.Max(this.Value - other.Value, 0.0));
    }

    /// <summary>
    /// Calculates the remaining percentage to reach 100%.
    /// </summary>
    /// <value>A new percentage representing (1.0 - Value).</value>
    public Percentage Remaining => new(1.0 - this.Value);

    /// <summary>
    /// Applies this percentage to a scalar value.
    /// </summary>
    /// <param name="value">The base value to scale.</param>
    /// <returns>The result of <paramref name="value"/> multiplied by this percentage.</returns>
    public double ApplyTo(double value) => value * this.Value;

    #endregion

    #region Equality & Comparison

    /// <inheritdoc/>
    public bool Equals(Percentage other) => this.Value.Equals(other.Value);

    /// <inheritdoc/>
    public override int GetHashCode() => this.Value.GetHashCode();

    /// <inheritdoc/>
    public int CompareTo(Percentage other) => this.Value.CompareTo(other.Value);

    /// <inheritdoc/>
    int IComparable.CompareTo(object? obj) {
        if(obj is null) return 1;
        if(obj is Percentage other) return CompareTo(other);
        throw new ArgumentException($"Object must be of type {nameof(Percentage)}", nameof(obj));
    }

    #endregion

    #region Operators

    // --- Comparison Operators (Percentage vs Percentage) ---

    /// <inheritdoc cref="IComparisonOperators{TSelf, TOther, TResult}.op_GreaterThan(TSelf, TOther)" />
    public static bool operator >(Percentage left, Percentage right) => left.Value > right.Value;

    /// <inheritdoc cref="IComparisonOperators{TSelf, TOther, TResult}.op_LessThan(TSelf, TOther)" />
    public static bool operator <(Percentage left, Percentage right) => left.Value < right.Value;

    /// <inheritdoc cref="IComparisonOperators{TSelf, TOther, TResult}.op_GreaterThanOrEqual(TSelf, TOther)" />
    public static bool operator >=(Percentage left, Percentage right) => left.Value >= right.Value;

    /// <inheritdoc cref="IComparisonOperators{TSelf, TOther, TResult}.op_LessThanOrEqual(TSelf, TOther)" />
    public static bool operator <=(Percentage left, Percentage right) => left.Value <= right.Value;

    // --- Comparison Operators (Percentage vs Double) ---

    /// <inheritdoc cref="IComparisonOperators{TSelf, TOther, TResult}.op_GreaterThan(TSelf, TOther)" />
    public static bool operator >(Percentage left, double right) => left.Value > right;

    /// <inheritdoc cref="IComparisonOperators{TSelf, TOther, TResult}.op_LessThan(TSelf, TOther)" />
    public static bool operator <(Percentage left, double right) => left.Value < right;

    /// <inheritdoc cref="IComparisonOperators{TSelf, TOther, TResult}.op_GreaterThanOrEqual(TSelf, TOther)" />
    public static bool operator >=(Percentage left, double right) => left.Value >= right;

    /// <inheritdoc cref="IComparisonOperators{TSelf, TOther, TResult}.op_LessThanOrEqual(TSelf, TOther)" />
    public static bool operator <=(Percentage left, double right) => left.Value <= right;

    /// <inheritdoc cref="IEqualityOperators{TSelf, TOther, TResult}.op_Equality(TSelf, TOther)" />
    public static bool operator ==(Percentage left, double right) => left.Value == right;

    /// <inheritdoc cref="IEqualityOperators{TSelf, TOther, TResult}.op_Inequality(TSelf, TOther)" />
    public static bool operator !=(Percentage left, double right) => left.Value != right;

    /// <inheritdoc cref="IEqualityOperators{TSelf, TOther, TResult}.op_Equality(TSelf, TOther)" />
    public static bool operator ==(double left, Percentage right) => left == right.Value;

    /// <inheritdoc cref="IEqualityOperators{TSelf, TOther, TResult}.op_Inequality(TSelf, TOther)" />
    public static bool operator !=(double left, Percentage right) => left != right.Value;

    // --- Comparison Operators (Percentage vs Int) ---

    /// <inheritdoc cref="IComparisonOperators{TSelf, TOther, TResult}.op_GreaterThan(TSelf, TOther)" />
    public static bool operator >(Percentage left, int right) => left.Value > right;

    /// <inheritdoc cref="IComparisonOperators{TSelf, TOther, TResult}.op_LessThan(TSelf, TOther)" />
    public static bool operator <(Percentage left, int right) => left.Value < right;

    /// <inheritdoc cref="IComparisonOperators{TSelf, TOther, TResult}.op_GreaterThanOrEqual(TSelf, TOther)" />
    public static bool operator >=(Percentage left, int right) => left.Value >= right;

    /// <inheritdoc cref="IComparisonOperators{TSelf, TOther, TResult}.op_LessThanOrEqual(TSelf, TOther)" />
    public static bool operator <=(Percentage left, int right) => left.Value <= right;

    /// <inheritdoc cref="IEqualityOperators{TSelf, TOther, TResult}.op_Equality(TSelf, TOther)" />
    public static bool operator ==(Percentage left, int right) => left.Value == right;

    /// <inheritdoc cref="IEqualityOperators{TSelf, TOther, TResult}.op_Inequality(TSelf, TOther)" />
    public static bool operator !=(Percentage left, int right) => left.Value != right;

    /// <inheritdoc cref="IEqualityOperators{TSelf, TOther, TResult}.op_Equality(TSelf, TOther)" />
    public static bool operator ==(int left, Percentage right) => left == right.Value;

    /// <inheritdoc cref="IEqualityOperators{TSelf, TOther, TResult}.op_Inequality(TSelf, TOther)" />
    public static bool operator !=(int left, Percentage right) => left != right.Value;

    // --- Arithmetic Operators ---

    /// <inheritdoc cref="IMultiplyOperators{TSelf, TOther, TResult}.op_Multiply(TSelf, TOther)" />
    public static double operator *(Percentage p, double baseValue) => p.Value * baseValue;

    /// <inheritdoc cref="IMultiplyOperators{TSelf, TOther, TResult}.op_Multiply(TSelf, TOther)" />
    public static double operator *(double baseValue, Percentage p) => baseValue * p.Value;

    /// <inheritdoc cref="IMultiplyOperators{TSelf, TOther, TResult}.op_Multiply(TSelf, TOther)" />
    public static TimeSpan operator *(Percentage p, TimeSpan timeSpan) => timeSpan * p.Value;

    /// <inheritdoc cref="IMultiplyOperators{TSelf, TOther, TResult}.op_Multiply(TSelf, TOther)" />
    public static TimeSpan operator *(TimeSpan timeSpan, Percentage p) => timeSpan * p.Value;

    /// <inheritdoc cref="IMultiplyOperators{TSelf, TOther, TResult}.op_Multiply(TSelf, TOther)" />
    public static Percentage operator *(Percentage left, Percentage right) => new(left.Value * right.Value);

    /// <inheritdoc cref="IDivisionOperators{TSelf, TOther, TResult}.op_Division(TSelf, TOther)" />
    public static double operator /(Percentage left, Percentage right) {
        if(right.IsZero) throw new DivideByZeroException("Cannot divide by zero percent.");
        return left.Value / right.Value;
    }

    /// <inheritdoc cref="IDivisionOperators{TSelf, TOther, TResult}.op_Division(TSelf, TOther)" />
    public static Percentage operator /(Percentage left, double right) {
        if(right == 0.0) throw new DivideByZeroException();
        return FromDouble(left.Value / right);
    }

    // --- Casts ---

    /// <summary>Implicitly converts a <see cref="Percentage"/> to a <see cref="double"/>.</summary>
    public static implicit operator double(Percentage p) => p.Value;

    /// <summary>Explicitly converts a <see cref="double"/> to a <see cref="Percentage"/>.</summary>
    public static explicit operator Percentage(double value) => FromDouble(value);

    #endregion

    #region Formatting (ISpanFormattable, IUtf8SpanFormattable, IFormattable)

    /// <summary>
    /// Returns a string representation of the percentage using the "P0" format (e.g., "50%").
    /// </summary>
    public override string ToString() => this.Value.ToString("P0", CultureInfo.CurrentCulture);

    /// <summary>
    /// Formats the percentage using the specified format.
    /// </summary>
    public string ToString(string? format) => ToString(format, null);

    /// <summary>
    /// Formats the percentage using the specified format and format provider.
    /// </summary>
    public string ToString(string? format, IFormatProvider? formatProvider) => this.Value.ToString(format ?? "P0", formatProvider ?? CultureInfo.CurrentCulture); 

    /// <summary>
    /// Tries to format the percentage into the destination character span.
    /// </summary>
    public bool TryFormat(Span<char> destination, out int charsWritten) => TryFormat(destination, out charsWritten, default, null);

    /// <summary>
    /// Tries to format the percentage into the destination character span using the specified format.
    /// </summary>
    public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format) => TryFormat(destination, out charsWritten, format, null);

    /// <summary>
    /// Tries to format the percentage into the destination character span using the specified format and provider.
    /// </summary>
    public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
        => this.Value.TryFormat(destination, out charsWritten, format.IsEmpty ? "P0" : format, provider ?? CultureInfo.CurrentCulture);

    /// <summary>
    /// Tries to format the percentage into the destination UTF-8 byte span.
    /// </summary>
    public bool TryFormat(Span<byte> utf8Destination, out int bytesWritten) => TryFormat(utf8Destination, out bytesWritten, default, null);

    /// <summary>
    /// Tries to format the percentage into the destination UTF-8 byte span using the specified format.
    /// </summary>
    public bool TryFormat(Span<byte> utf8Destination, out int bytesWritten, ReadOnlySpan<char> format) => TryFormat(utf8Destination, out bytesWritten, format, null);

    /// <summary>
    /// Tries to format the percentage into the destination UTF-8 byte span using the specified format and provider.
    /// </summary>
    public bool TryFormat(Span<byte> utf8Destination, out int bytesWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
        => this.Value.TryFormat(utf8Destination, out bytesWritten, format.IsEmpty ? "P0" : format, provider ?? CultureInfo.CurrentCulture);

    #endregion

    #region Parsing

    /// <summary>
    /// Parses a string into a <see cref="Percentage"/>.
    /// </summary>
    /// <param name="s">The string to parse.</param>
    /// <returns>The parsed <see cref="Percentage"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="s"/> is null.</exception>
    /// <exception cref="FormatException">Thrown if the input is not a valid number between 0.0 and 1.0.</exception>
    public static Percentage Parse(string s) {
        Preca.ThrowIfNull(s);
        return Parse(s.AsSpan());
    }

    /// <summary>
    /// Parses a character span into a <see cref="Percentage"/>.
    /// </summary>
    /// <param name="s">The span to parse.</param>
    /// <returns>The parsed <see cref="Percentage"/>.</returns>
    /// <exception cref="FormatException">Thrown if the input is not a valid number between 0.0 and 1.0.</exception>
    public static Percentage Parse(ReadOnlySpan<char> s) {
        if(TryParse(s, out Percentage result)) {
            return result;
        }
        throw new FormatException($"Span '{s}' was not valid for Percentage. Value must be a raw number between 0.0 and 1.0.");
    }

    /// <summary>
    /// Parses a UTF-8 byte span into a <see cref="Percentage"/>.
    /// </summary>
    /// <param name="utf8Text">The UTF-8 byte span to parse.</param>
    /// <returns>The parsed <see cref="Percentage"/>.</returns>
    /// <exception cref="FormatException">Thrown if the input is not a valid UTF-8 number between 0.0 and 1.0.</exception>
    public static Percentage Parse(ReadOnlySpan<byte> utf8Text) {
        if(TryParse(utf8Text, out Percentage result)) {
            return result;
        }
        throw new FormatException("Invalid UTF-8 data for Percentage. Expected raw number bytes between 0.0 and 1.0.");
    }

    /// <summary>
    /// Tries to parse a string into a <see cref="Percentage"/>.
    /// </summary>
    public static bool TryParse([NotNullWhen(true)] string? s, out Percentage result) {
        if(s is null) {
            result = default;
            return false;
        }
        return TryParse(s.AsSpan(), out result);
    }

    /// <summary>
    /// Tries to parse a character span into a <see cref="Percentage"/>.
    /// </summary>
    public static bool TryParse(ReadOnlySpan<char> s, out Percentage result) {
        return TryParseInternal(s, CultureInfo.InvariantCulture, out result);
    }

    /// <summary>
    /// Tries to parse a UTF-8 byte span into a <see cref="Percentage"/>.
    /// </summary>
    public static bool TryParse(ReadOnlySpan<byte> utf8Text, out Percentage result) {
        return TryParseUtf8Internal(utf8Text, out result);
    }

    /// <summary>
    /// Internal parsing logic. Expects a raw number string (e.g. "0.5").
    /// Does NOT support strings with '%' symbols.
    /// </summary>
    internal static bool TryParseInternal(ReadOnlySpan<char> s, IFormatProvider? provider, out Percentage result) {
        result = default;
        if(double.TryParse(s, NumberStyles.Float, provider ?? CultureInfo.InvariantCulture, out double value)) {
            if(value >= 0.0 && value <= 1.0) {
                result = new Percentage(value);
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Internal UTF-8 parsing logic. Expects raw ASCII/UTF-8 number bytes (e.g. "0.5").
    /// </summary>
    private static bool TryParseUtf8Internal(ReadOnlySpan<byte> utf8Text, out Percentage result) {
        result = default;
        if(Utf8Parser.TryParse(utf8Text, out double value, out _) && value >= 0.0 && value <= 1.0) {
            result = new Percentage(value);
            return true;
        }
        return false;
    }

    #endregion

    #region Explicit Interface Implementations (IParsable, ISpanParsable, IUtf8SpanParsable)

    static Percentage IParsable<Percentage>.Parse(string s, IFormatProvider? provider) => Parse(s);
    static bool IParsable<Percentage>.TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out Percentage result) {
        if(s is null) { result = default; return false; }
        return TryParseInternal(s.AsSpan(), provider, out result);
    }
    static Percentage ISpanParsable<Percentage>.Parse(ReadOnlySpan<char> s, IFormatProvider? provider) {
        if(TryParseInternal(s, provider, out Percentage result)) return result;
        throw new FormatException($"Span '{s}' was not valid for Percentage.");
    }
    static bool ISpanParsable<Percentage>.TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out Percentage result) => TryParseInternal(s, provider, out result);
    static Percentage IUtf8SpanParsable<Percentage>.Parse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider) => Parse(utf8Text);
    static bool IUtf8SpanParsable<Percentage>.TryParse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider, out Percentage result) => TryParseUtf8Internal(utf8Text, out result);

    #endregion

    #region Alternate Comparers (.NET 10 Alternate Lookup)

    /// <summary>
    /// Gets an equality comparer that performs equality comparisons on <see cref="Percentage"/>
    /// and supports zero-allocation alternate lookups using <see cref="ReadOnlySpan{Char}"/>.
    /// </summary>
    public static IEqualityComparer<Percentage> OrdinalComparer => PercentageOrdinalComparer.Instance;

    private sealed class PercentageOrdinalComparer : IEqualityComparer<Percentage>, IAlternateEqualityComparer<ReadOnlySpan<char>, Percentage> {
        public static PercentageOrdinalComparer Instance { get; } = new();

        public bool Equals(Percentage x, Percentage y) => x.Value.Equals(y.Value);

        public int GetHashCode(Percentage obj) => obj.Value.GetHashCode();

        public bool Equals(ReadOnlySpan<char> alternate, Percentage other) {
            if(Percentage.TryParse(alternate, out Percentage p)) {
                return p.Value.Equals(other.Value);
            }
            return false;
        }

        public int GetHashCode(ReadOnlySpan<char> alternate) {
            if(Percentage.TryParse(alternate, out Percentage p)) {
                return p.Value.GetHashCode();
            }
            return 0;
        }

        public Percentage Create(ReadOnlySpan<char> alternate) => Percentage.Parse(alternate);
    }

    #endregion
}