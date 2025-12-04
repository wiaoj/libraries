using System.Buffers.Text;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Numerics;

namespace Wiaoj.Primitives;
/// <summary>
/// Represents a percentage value as a double-precision floating-point number between 0.0 and 1.0.
/// This is an immutable, allocation-free value object that wraps a <see cref="double"/> value
/// and integrates with modern .NET numeric and parsing interfaces.
/// </summary>
[DebuggerDisplay("{ToString(),nq}")]
public readonly record struct Percentage :
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

    /// <summary>Gets the underlying value of the percentage, ranging from 0.0 to 1.0.</summary>
    public double Value { get; }

    /// <summary>Gets a value indicating whether this percentage is zero (0%).</summary>
    public bool IsZero => this.Value is 0.0D;

    /// <summary>Gets a value indicating whether this percentage represents the whole (100%).</summary>
    public bool IsOne => this.Value is 1.0D;

    private Percentage(double value) { this.Value = value; }
    #endregion

    #region Factory Methods
    /// <summary>Creates a <see cref="Percentage"/> from a value between 0.0 (0%) and 1.0 (100%).</summary>
    public static Percentage FromDouble(double value) {
        Preca.ThrowIfOutOfRange(value, 0.0, 1.0);
        return new Percentage(value);
    }

    /// <summary>Creates a <see cref="Percentage"/> from a value between 0 (0%) and 100 (100%).</summary>
    public static Percentage FromInt(int value) {
        Preca.ThrowIfOutOfRange(value, 0, 100);
        return new Percentage(value / 100.0);
    } 
    #endregion

    /// <inheritdoc/>
    public int CompareTo(Percentage other) {
        return this.Value.CompareTo(other.Value);
    }

    #region Operators
    /// <inheritdoc cref="IComparisonOperators{TSelf, TOther, TResult}.op_GreaterThan(TSelf, TOther)" />
    public static bool operator >(Percentage left, Percentage right) {
        return left.Value > right.Value;
    }

    /// <inheritdoc cref="IComparisonOperators{TSelf, TOther, TResult}.op_LessThan(TSelf, TOther)" />
    public static bool operator <(Percentage left, Percentage right) {
        return left.Value < right.Value;
    }

    /// <inheritdoc cref="IComparisonOperators{TSelf, TOther, TResult}.op_GreaterThanOrEqual(TSelf, TOther)" />
    public static bool operator >=(Percentage left, Percentage right) {
        return left.Value >= right.Value;
    }

    /// <inheritdoc cref="IComparisonOperators{TSelf, TOther, TResult}.op_LessThanOrEqual(TSelf, TOther)" />
    public static bool operator <=(Percentage left, Percentage right) {
        return left.Value <= right.Value;
    }

    /// <inheritdoc cref="IComparisonOperators{TSelf, TOther, TResult}.op_GreaterThan(TSelf, TOther)" />
    public static bool operator >(Percentage left, double right) {
        return left.Value > right;
    }

    /// <inheritdoc cref="IComparisonOperators{TSelf, TOther, TResult}.op_LessThan(TSelf, TOther)" />
    public static bool operator <(Percentage left, double right) {
        return left.Value < right;
    }

    /// <inheritdoc cref="IComparisonOperators{TSelf, TOther, TResult}.op_GreaterThanOrEqual(TSelf, TOther)" />
    public static bool operator >=(Percentage left, double right) {
        return left.Value >= right;
    }

    /// <inheritdoc cref="IComparisonOperators{TSelf, TOther, TResult}.op_LessThanOrEqual(TSelf, TOther)" />
    public static bool operator <=(Percentage left, double right) {
        return left.Value <= right;
    }

    /// <inheritdoc cref="IEqualityOperators{TSelf, TOther, TResult}.op_Equality(TSelf, TOther)" />
    public static bool operator ==(Percentage left, double right) {
        return left.Value == right;
    }

    /// <inheritdoc cref="IEqualityOperators{TSelf, TOther, TResult}.op_Inequality(TSelf, TOther)" />
    public static bool operator !=(Percentage left, double right) {
        return left.Value != right;
    }

    /// <inheritdoc cref="IEqualityOperators{TSelf, TOther, TResult}.op_Equality(TSelf, TOther)" />
    public static bool operator ==(double left, Percentage right) {
        return left == right.Value;
    }

    /// <inheritdoc cref="IEqualityOperators{TSelf, TOther, TResult}.op_Inequality(TSelf, TOther)" />
    public static bool operator !=(double left, Percentage right) {
        return left != right.Value;
    }

    /// <inheritdoc cref="IComparisonOperators{TSelf, TOther, TResult}.op_GreaterThan(TSelf, TOther)" />
    public static bool operator >(Percentage left, int right) {
        return left.Value > right;
    }

    /// <inheritdoc cref="IComparisonOperators{TSelf, TOther, TResult}.op_LessThan(TSelf, TOther)" />
    public static bool operator <(Percentage left, int right) {
        return left.Value < right;
    }

    /// <inheritdoc cref="IComparisonOperators{TSelf, TOther, TResult}.op_GreaterThanOrEqual(TSelf, TOther)" />
    public static bool operator >=(Percentage left, int right) {
        return left.Value >= right;
    }

    /// <inheritdoc cref="IComparisonOperators{TSelf, TOther, TResult}.op_LessThanOrEqual(TSelf, TOther)" />
    public static bool operator <=(Percentage left, int right) {
        return left.Value <= right;
    }

    /// <inheritdoc cref="IEqualityOperators{TSelf, TOther, TResult}.op_Equality(TSelf, TOther)" />
    public static bool operator ==(Percentage left, int right) {
        return left.Value == right;
    }

    /// <inheritdoc cref="IEqualityOperators{TSelf, TOther, TResult}.op_Inequality(TSelf, TOther)" />
    public static bool operator !=(Percentage left, int right) {
        return left.Value != right;
    }

    /// <inheritdoc cref="IEqualityOperators{TSelf, TOther, TResult}.op_Equality(TSelf, TOther)" />
    public static bool operator ==(int left, Percentage right) {
        return left == right.Value;
    }

    /// <inheritdoc cref="IEqualityOperators{TSelf, TOther, TResult}.op_Inequality(TSelf, TOther)" />
    public static bool operator !=(int left, Percentage right) {
        return left != right.Value;
    }


    /// <inheritdoc cref="IMultiplyOperators{TSelf, TOther, TResult}.op_Multiply(TSelf, TOther)" />
    public static double operator *(Percentage p, double baseValue) {
        return p.Value * baseValue;
    }

    /// <summary>Multiplies a <see cref="double"/> value by a <see cref="Percentage"/>.</summary>
    public static double operator *(double baseValue, Percentage p) {
        return baseValue * p.Value;
    }

    /// <inheritdoc cref="IMultiplyOperators{TSelf, TOther, TResult}.op_Multiply(TSelf, TOther)" />
    /// <summary>Multiplies a <see cref="Percentage"/> by a <see cref="TimeSpan"/> value.</summary>
    public static TimeSpan operator *(Percentage p, TimeSpan timeSpan) {
        return timeSpan * p.Value;
    }

    /// <summary>Multiplies a <see cref="TimeSpan"/> value by a <see cref="Percentage"/>.</summary>
    public static TimeSpan operator *(TimeSpan timeSpan, Percentage p) {
        return timeSpan * p.Value;
    }

    /// <inheritdoc cref="IMultiplyOperators{TSelf, TOther, TResult}.op_Multiply(TSelf, TOther)" />
    /// <summary>Calculates the product of two percentage values (e.g., 50% of 50% is 25%).</summary>
    public static Percentage operator *(Percentage left, Percentage right) {
        return new(left.Value * right.Value);
    }


    /// <inheritdoc cref="IDivisionOperators{TSelf, TOther, TResult}.op_Division(TSelf, TOther)" />
    /// <summary>Divides a percentage by another, returning their ratio as a double (e.g., 50% / 25% = 2.0).</summary>
    public static double operator /(Percentage left, Percentage right) {
        Preca.ThrowIfTrue(right.IsZero, static () => new DivideByZeroException("Cannot divide by zero percent."));
        return left.Value / right.Value;
    }

    /// <inheritdoc cref="IDivisionOperators{TSelf, TOther, TResult}.op_Division(TSelf, TOther)" />
    /// <summary>Divides a percentage by a scalar value, resulting in a new, smaller percentage (e.g., 50% / 2.0 = 25%).</summary>
    public static Percentage operator /(Percentage left, double right) {
        Preca.ThrowIfZero(right, static () => new DivideByZeroException());
        return FromDouble(left.Value / right);
    }


    /// <summary>Implicitly converts a <see cref="Percentage"/> to its underlying <see cref="double"/> value.</summary>
    public static implicit operator double(Percentage p) {
        return p.Value;
    }

    /// <summary>Explicitly converts a <see cref="double"/> to a <see cref="Percentage"/>.</summary>
    /// <exception cref="PrecaArgumentOutOfRangeException">Thrown if the value is not between 0.0 and 1.0.</exception>
    public static explicit operator Percentage(double value) {
        return FromDouble(value);
    }
    #endregion

    #region Formatting & Parsing
    /// <inheritdoc/>
    public override string ToString() {
        return this.Value.ToString("P0", CultureInfo.CurrentCulture);
    }

    /// <inheritdoc/>
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

    /// <inheritdoc/>
    public static Percentage Parse(string s, IFormatProvider? provider) {
        return FromDouble(ParseToDouble(s, provider));
    }

    /// <inheritdoc/>
    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out Percentage result) {
        if (TryParseToDouble(s, provider, out double value) && value is >= 0.0 and <= 1.0) {
            result = new Percentage(value);
            return true;
        }
        result = default;
        return false;
    }

    /// <inheritdoc/>
    public static Percentage Parse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider) {
        if (!Utf8Parser.TryParse(utf8Text, out double value, out _))
            throw new FormatException("Invalid UTF-8 string for Percentage.");
        return FromDouble(value);
    }

    /// <inheritdoc/>
    public static bool TryParse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider, [MaybeNullWhen(false)] out Percentage result) {
        if (Utf8Parser.TryParse(utf8Text, out double value, out _) && value is >= 0.0 and <= 1.0) {
            result = new Percentage(value);
            return true;
        }
        result = default;
        return false;
    }

    private static double ParseToDouble(string? s, IFormatProvider? provider) {
        if (string.IsNullOrWhiteSpace(s)) throw new ArgumentNullException(nameof(s));
        s = s.Trim();
        bool hasPercentSign = s.EndsWith('%');
        ReadOnlySpan<char> numberPart = hasPercentSign ? s.AsSpan(0, s.Length - 1) : s.AsSpan();
        double parsedValue = double.Parse(numberPart, NumberStyles.Float, provider ?? CultureInfo.CurrentCulture);
        return hasPercentSign ? parsedValue / 100.0 : parsedValue;
    }

    private static bool TryParseToDouble(string? s, IFormatProvider? provider, out double parsedValue) {
        parsedValue = 0;
        if (string.IsNullOrWhiteSpace(s)) return false;
        s = s.Trim();
        bool hasPercentSign = s.EndsWith('%');
        ReadOnlySpan<char> numberPart = hasPercentSign ? s.AsSpan(0, s.Length - 1) : s.AsSpan();
        if (double.TryParse(numberPart, NumberStyles.Float, provider ?? CultureInfo.CurrentCulture, out double value)) {
            parsedValue = hasPercentSign ? value / 100.0 : value;
            return true;
        }
        return false;
    }
    #endregion
}