using System.Buffers.Text;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Numerics;
using System.Text.Json.Serialization;
using Wiaoj.Primitives.JsonConverters;

namespace Wiaoj.Primitives;
/// <summary>
/// Represents a percentage value as a double-precision floating-point number between 0.0 and 1.0.
/// This is an immutable, allocation-free value object that wraps a <see cref="double"/> value.
/// </summary>
/// <remarks>
/// This struct implements modern .NET interfaces for math, parsing, and formatting.
/// It uses explicit implementation for parsing interfaces to keep the API surface clean.
/// </remarks>
[DebuggerDisplay("{ToString(),nq}")]
[JsonConverter(typeof(PercentageJsonConverter))]
public readonly record struct Percentage :
    IEquatable<Percentage>,
    IComparable,
    IComparable<Percentage>,
    IFormattable,
    IParsable<Percentage>,
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

    /// <summary>Represents a percentage of 0% (0.0).</summary>
    public static readonly Percentage Zero = new(0.0);

    /// <summary>Represents a percentage of 50% (0.5).</summary>
    public static readonly Percentage Half = new(0.5);

    /// <summary>Represents a percentage of 100% (1.0).</summary>
    public static readonly Percentage Full = new(1.0);

    /// <inheritdoc/>
    static Percentage IMinMaxValue<Percentage>.MinValue => Zero;

    /// <inheritdoc/>
    static Percentage IMinMaxValue<Percentage>.MaxValue => Full;

    /// <summary>
    /// Gets the underlying value of the percentage, ranging from 0.0 to 1.0.
    /// </summary>
    /// <value>A double between 0.0 and 1.0.</value>
    public double Value { get; }

    /// <summary>
    /// Gets a value indicating whether this percentage is zero (0%).
    /// </summary>
    public bool IsZero => this.Value is 0.0D;

    /// <summary>
    /// Gets a value indicating whether this percentage represents the whole (100%).
    /// </summary>
    public bool IsOne => this.Value is 1.0D;

    // Private constructor to enforce factory usage
    private Percentage(double value) { this.Value = value; }

    #endregion

    #region Factory Methods

    /// <summary>
    /// Creates a <see cref="Percentage"/> from a raw double value between 0.0 (0%) and 1.0 (100%).
    /// </summary>
    /// <param name="value">The value representing the fraction (e.g., 0.25 for 25%).</param>
    /// <returns>A new <see cref="Percentage"/> instance.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="value"/> is less than 0.0 or greater than 1.0.</exception>
    public static Percentage FromDouble(double value) {
        if(value < 0.0 || value > 1.0)
            throw new ArgumentOutOfRangeException(nameof(value), value, "Value must be between 0.0 and 1.0.");

        return new Percentage(value);
    }

    /// <summary>
    /// Creates a <see cref="Percentage"/> from an integer value between 0 (0%) and 100 (100%).
    /// </summary>
    /// <param name="value">The integer value representing the percentage (e.g., 25 for 25%).</param>
    /// <returns>A new <see cref="Percentage"/> instance.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="value"/> is less than 0 or greater than 100.</exception>
    public static Percentage FromInt(int value) {
        if(value < 0 || value > 100)
            throw new ArgumentOutOfRangeException(nameof(value), value, "Value must be between 0 and 100.");

        return new Percentage(value / 100.0D);
    }

    #endregion

    #region Math Operations

    /// <summary>
    /// Adds another percentage to the current one, clamping the result between 0.0 and 1.0 (0% - 100%).
    /// </summary>
    /// <param name="other">The percentage value to add.</param>
    /// <returns>A new <see cref="Percentage"/> representing the sum, capped at <see cref="Full"/> (100%).</returns>
    public Percentage AddClamped(Percentage other) {
        double sum = this.Value + other.Value;
        return new Percentage(Math.Min(sum, 1.0));
    }

    /// <summary>
    /// Subtracts another percentage from the current one, clamping the result between 0.0 and 1.0 (0% - 100%).
    /// </summary>
    /// <param name="other">The percentage value to subtract.</param>
    /// <returns>A new <see cref="Percentage"/> representing the difference, floored at <see cref="Zero"/> (0%).</returns>
    public Percentage SubtractClamped(Percentage other) {
        double diff = this.Value - other.Value;
        return new Percentage(Math.Max(diff, 0.0));
    }

    /// <summary>
    /// Returns the remainder to reach 100% (e.g., if 20%, returns 80%).
    /// </summary>
    public Percentage Remaining => new(1.0 - this.Value);

    /// <summary>
    /// Scales a value by this percentage. Equivalent to (value * percentage).
    /// </summary>
    /// <param name="value">The base value to scale.</param>
    /// <returns>The calculated result.</returns>
    public double ApplyTo(double value) => value * this.Value;

    #endregion

    #region Equality & Comparison

    /// <summary>
    /// Indicates whether the current <see cref="Percentage"/> is equal to another <see cref="Percentage"/>.
    /// </summary>
    /// <param name="other">A percentage to compare with this instance.</param>
    /// <returns><see langword="true"/> if the current percentage is equal to the <paramref name="other"/> parameter; otherwise, <see langword="false"/>.</returns>
    public bool Equals(Percentage other) {
        return this.Value.Equals(other.Value);
    }

    /// <inheritdoc/>
    public override int GetHashCode() {
        return this.Value.GetHashCode();
    }

    /// <summary>
    /// Compares the current instance with another object of the same type and returns an integer that indicates whether the current instance precedes, follows, or occurs in the same position in the sort order as the other object.
    /// </summary>
    /// <param name="other">An object to compare with this instance.</param>
    /// <returns>A value that indicates the relative order of the objects being compared.</returns>
    public int CompareTo(Percentage other) {
        return this.Value.CompareTo(other.Value);
    }

    /// <summary>
    /// Compares the current instance with another object.
    /// </summary>
    /// <param name="obj">The object to compare with this instance.</param>
    /// <returns>A value that indicates the relative order of the objects being compared.</returns>
    /// <exception cref="ArgumentException">Thrown if <paramref name="obj"/> is not a <see cref="Percentage"/>.</exception>
    int IComparable.CompareTo(object? obj) {
        if(obj is null) return 1;
        if(obj is Percentage other) return CompareTo(other);
        throw new ArgumentException($"Object must be of type {nameof(Percentage)}", nameof(obj));
    }

    #endregion

    #region Operators - Comparison (Percentage vs Percentage)

    /// <inheritdoc cref="IComparisonOperators{TSelf, TOther, TResult}.op_GreaterThan(TSelf, TOther)" />
    public static bool operator >(Percentage left, Percentage right) => left.Value > right.Value;

    /// <inheritdoc cref="IComparisonOperators{TSelf, TOther, TResult}.op_LessThan(TSelf, TOther)" />
    public static bool operator <(Percentage left, Percentage right) => left.Value < right.Value;

    /// <inheritdoc cref="IComparisonOperators{TSelf, TOther, TResult}.op_GreaterThanOrEqual(TSelf, TOther)" />
    public static bool operator >=(Percentage left, Percentage right) => left.Value >= right.Value;

    /// <inheritdoc cref="IComparisonOperators{TSelf, TOther, TResult}.op_LessThanOrEqual(TSelf, TOther)" />
    public static bool operator <=(Percentage left, Percentage right) => left.Value <= right.Value;

    #endregion

    #region Operators - Comparison (Percentage vs Double)

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

    #endregion

    #region Operators - Comparison (Percentage vs Int)

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

    #endregion

    #region Operators - Arithmetic

    /// <inheritdoc cref="IMultiplyOperators{TSelf, TOther, TResult}.op_Multiply(TSelf, TOther)" />
    public static double operator *(Percentage p, double baseValue) => p.Value * baseValue;

    /// <inheritdoc cref="IMultiplyOperators{TSelf, TOther, TResult}.op_Multiply(TSelf, TOther)" />
    public static double operator *(double baseValue, Percentage p) => baseValue * p.Value;

    /// <inheritdoc cref="IMultiplyOperators{TSelf, TOther, TResult}.op_Multiply(TSelf, TOther)" />
    public static TimeSpan operator *(Percentage p, TimeSpan timeSpan) => timeSpan * p.Value;

    /// <inheritdoc cref="IMultiplyOperators{TSelf, TOther, TResult}.op_Multiply(TSelf, TOther)" />
    public static TimeSpan operator *(TimeSpan timeSpan, Percentage p) => timeSpan * p.Value;

    /// <inheritdoc cref="IMultiplyOperators{TSelf, TOther, TResult}.op_Multiply(TSelf, TOther)" />
    /// <summary>Calculates the product of two percentage values (e.g., 50% of 50% is 25%).</summary>
    public static Percentage operator *(Percentage left, Percentage right) => new(left.Value * right.Value);

    /// <inheritdoc cref="IDivisionOperators{TSelf, TOther, TResult}.op_Division(TSelf, TOther)" />
    /// <summary>Divides a percentage by another, returning their ratio as a double (e.g., 50% / 25% = 2.0).</summary>
    public static double operator /(Percentage left, Percentage right) {
        if(right.IsZero) throw new DivideByZeroException("Cannot divide by zero percent.");
        return left.Value / right.Value;
    }

    /// <inheritdoc cref="IDivisionOperators{TSelf, TOther, TResult}.op_Division(TSelf, TOther)" />
    /// <summary>Divides a percentage by a scalar value, resulting in a new percentage.</summary>
    public static Percentage operator /(Percentage left, double right) {
        if(right == 0.0) throw new DivideByZeroException();
        return FromDouble(left.Value / right);
    }

    // --- Implicit/Explicit Casts ---

    /// <summary>Implicitly converts a <see cref="Percentage"/> to its underlying <see cref="double"/> value.</summary>
    public static implicit operator double(Percentage p) => p.Value;

    /// <summary>Explicitly converts a <see cref="double"/> to a <see cref="Percentage"/>.</summary>
    public static explicit operator Percentage(double value) => FromDouble(value);

    #endregion

    #region Formatting

    /// <summary>
    /// Returns a string representation of the percentage value using the current culture (e.g., "50%").
    /// </summary>
    public override string ToString() {
        return this.Value.ToString("P0", CultureInfo.CurrentCulture);
    }

    /// <summary>
    /// Formats the value of the current instance using the specified format.
    /// </summary>
    /// <param name="format">The format to use, or null to use the default format.</param>
    /// <param name="formatProvider">The provider to use to specify the format, or null to obtain the numeric format information from the current operating system setting.</param>
    public string ToString(string? format, IFormatProvider? formatProvider) {
        return this.Value.ToString(format, formatProvider);
    }

    /// <inheritdoc/>
    public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider) {
        return this.Value.TryFormat(destination, out charsWritten, format, provider);
    }

    /// <inheritdoc/>
    public bool TryFormat(Span<byte> utf8Destination, out int bytesWritten, ReadOnlySpan<char> format, IFormatProvider? provider) {
        return this.Value.TryFormat(utf8Destination, out bytesWritten, format, provider);
    }

    #endregion

    #region Parsing (Explicit Implementation)

    /// <summary>
    /// Internal helper method to handle the parsing logic for both string and span inputs.
    /// Access modified to 'internal' so the JsonConverter can use it without code duplication.
    /// </summary>
    internal static bool TryParseInternal(ReadOnlySpan<char> s, IFormatProvider? provider, out Percentage result) {
        result = default;
        if(s.IsEmpty || s.IsWhiteSpace()) return false;

        // Trim whitespace
        s = s.Trim();

        // Check for percentage sign
        bool hasPercentSign = s.EndsWith("%");
        ReadOnlySpan<char> numberPart = hasPercentSign ? s[..^1] : s;

        // Try parse double
        if(double.TryParse(numberPart, NumberStyles.Float, provider ?? CultureInfo.CurrentCulture, out double value)) {
            // Adjust value if percentage sign was present (e.g. "50%" -> 50.0 -> 0.5)
            // If raw number "0.5" -> 0.5
            double finalValue = hasPercentSign ? value / 100.0 : value;

            // Validate range
            if(finalValue >= 0.0 && finalValue <= 1.0) {
                result = new Percentage(finalValue);
                return true;
            }
        }

        return false;
    }

    // --- IParsable<Percentage> Explicit Implementation ---

    /// <inheritdoc/>
    static Percentage IParsable<Percentage>.Parse(string s, IFormatProvider? provider) {
        Preca.ThrowIfNull(s);
        if(TryParseInternal(s.AsSpan(), provider, out Percentage result)) {
            return result;
        }
        throw new FormatException($"The string '{s}' was not in a correct format for a Percentage. Expected a value between 0.0 and 1.0, or a percentage string like '50%'.");
    }

    /// <inheritdoc/>
    static bool IParsable<Percentage>.TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out Percentage result) {
        if(s is null) {
            result = default;
            return false;
        }
        return TryParseInternal(s.AsSpan(), provider, out result);
    }

    // --- IUtf8SpanParsable<Percentage> Explicit Implementation ---

    /// <inheritdoc/>
    static Percentage IUtf8SpanParsable<Percentage>.Parse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider) {
        if(Utf8Parser.TryParse(utf8Text, out double value, out _) && value is >= 0.0 and <= 1.0) {
            return new Percentage(value);
        }
        throw new FormatException("Invalid UTF-8 string for Percentage.");
    }

    /// <inheritdoc/>
    static bool IUtf8SpanParsable<Percentage>.TryParse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider, out Percentage result) {
        // Note: Utf8Parser handles raw numbers optimally. It usually does not handle '%' suffix directly.
        // Assuming the UTF8 input is a raw double value (0.0 - 1.0).
        if(Utf8Parser.TryParse(utf8Text, out double value, out _) && value is >= 0.0 and <= 1.0) {
            result = new Percentage(value);
            return true;
        }

        result = default;
        return false;
    }

    #endregion
}