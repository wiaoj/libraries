using System.Diagnostics;
using System.Numerics;

namespace Wiaoj.DistributedCounter;

/// <summary>
/// Represents the value of a distributed counter.
/// Encapsulates the long primitive to provide type safety and potential future validation logic.
/// </summary>
/// <remarks>
/// <para>
/// Arithmetic operators (<c>+</c>, <c>-</c>) use <see langword="checked"/> semantics. If an
/// operation would overflow or underflow <see cref="long"/>'s range, an
/// <see cref="OverflowException"/> is thrown instead of silently wrapping to an incorrect
/// value. Callers that increment/decrement counters based on external or attacker-influenced
/// amounts should be prepared to handle <see cref="OverflowException"/> explicitly (e.g.
/// translating it into a rejected operation), rather than letting it propagate as an
/// unhandled exception.
/// </para>
/// <para>
/// Conversion from <see cref="CounterValue"/> to <see cref="long"/> is intentionally
/// <see langword="explicit"/> (via a cast or <see cref="Value"/>) rather than implicit. This
/// is a deliberate API choice: an implicit conversion made it too easy to accidentally pass a
/// <see cref="CounterValue"/> anywhere a <see cref="long"/> was expected, silently discarding
/// the type-safety this struct exists to provide. Conversion from <see cref="long"/> to
/// <see cref="CounterValue"/> remains implicit, since wrapping a raw count into this type is
/// always safe and lossless.
/// </para>
/// </remarks>
[DebuggerDisplay("{Value}")]
public readonly record struct CounterValue :
    IComparisonOperators<CounterValue, CounterValue, bool>,
    IAdditionOperators<CounterValue, long, CounterValue>,
    ISubtractionOperators<CounterValue, long, CounterValue>,
    IAdditionOperators<CounterValue, CounterValue, CounterValue>,
    ISubtractionOperators<CounterValue, CounterValue, CounterValue> {

    /// <summary>
    /// Gets the raw long value of the counter.
    /// </summary>
    public long Value { get; }

    /// <summary>
    /// Represents a counter value of zero.
    /// </summary>
    public static CounterValue Zero { get; } = new(0);

    /// <summary>
    /// Initializes a new instance of the <see cref="CounterValue"/> struct.
    /// </summary>
    /// <param name="value">The initial raw counter value.</param>
    public CounterValue(long value) {
        this.Value = value;
    }

    /// <inheritdoc cref="IAdditionOperators{TSelf, TOther, TResult}.op_Addition(TSelf, TOther)" />
    public static CounterValue operator +(CounterValue left, long right) {
        checked {
            return new(left.Value + right);
        }
    }

    /// <inheritdoc cref="ISubtractionOperators{TSelf, TOther, TResult}.op_Subtraction(TSelf, TOther)" />
    public static CounterValue operator -(CounterValue left, long right) {
        checked {
            return new(left.Value - right);
        }
    }

    /// <inheritdoc cref="IAdditionOperators{TSelf, TOther, TResult}.op_Addition(TSelf, TOther)" />
    public static CounterValue operator +(CounterValue left, CounterValue right) {
        checked {
            return new(left.Value + right.Value);
        }
    }

    /// <inheritdoc cref="ISubtractionOperators{TSelf, TOther, TResult}.op_Subtraction(TSelf, TOther)" />
    public static CounterValue operator -(CounterValue left, CounterValue right) {
        checked {
            return new(left.Value - right.Value);
        }
    }

    /// <inheritdoc cref="IComparisonOperators{TSelf, TOther, TResult}.op_GreaterThan(TSelf, TOther)" />
    public static bool operator >(CounterValue left, CounterValue right) {
        return left.Value > right.Value;
    }

    /// <inheritdoc cref="IComparisonOperators{TSelf, TOther, TResult}.op_LessThan(TSelf, TOther)" />
    public static bool operator <(CounterValue left, CounterValue right) {
        return left.Value < right.Value;
    }

    /// <inheritdoc cref="IComparisonOperators{TSelf, TOther, TResult}.op_GreaterThanOrEqual(TSelf, TOther)" />
    public static bool operator >=(CounterValue left, CounterValue right) {
        return left.Value >= right.Value;
    }

    /// <inheritdoc cref="IComparisonOperators{TSelf, TOther, TResult}.op_LessThanOrEqual(TSelf, TOther)" />
    public static bool operator <=(CounterValue left, CounterValue right) {
        return left.Value <= right.Value;
    }

    /// <summary>
    /// Explicitly converts this <see cref="CounterValue"/> to its raw <see cref="long"/>.
    /// </summary>
    /// <param name="v">The counter value to cast.</param>
    public static explicit operator long(CounterValue v) {
        return v.Value;
    }

    /// <summary>
    /// Implicitly wraps a raw <see cref="long"/> into a <see cref="CounterValue"/>.
    /// </summary>
    /// <param name="v">The raw long value to wrap.</param>
    public static implicit operator CounterValue(long v) {
        return new(v);
    }

    /// <inheritdoc/>
    public override string ToString() {
        return this.Value.ToString();
    }
}