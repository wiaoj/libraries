using System.Diagnostics;
using System.Numerics;

namespace Wiaoj.DistributedCounter;
/// <summary>
/// Represents the value of a distributed counter.
/// Encapsulates the long primitive to provide type safety and potential future validation logic.
/// </summary>
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
    public CounterValue(long value) {
        this.Value = value;
    }

    // Operators to make math natural: var newVal = currentVal + 5;
    public static CounterValue operator +(CounterValue left, long right) {
        return new(left.Value + right);
    }

    public static CounterValue operator -(CounterValue left, long right) {
        return new(left.Value - right);
    }

    public static CounterValue operator +(CounterValue left, CounterValue right) {
        return new(left.Value + right.Value);
    }

    public static CounterValue operator -(CounterValue left, CounterValue right) {
        return new(left.Value - right.Value);
    }

    public static bool operator >(CounterValue left, CounterValue right) {
        return left.Value > right.Value;
    }

    public static bool operator <(CounterValue left, CounterValue right) {
        return left.Value < right.Value;
    }

    public static bool operator >=(CounterValue left, CounterValue right) {
        return left.Value >= right.Value;
    }

    public static bool operator <=(CounterValue left, CounterValue right) {
        return left.Value <= right.Value;
    }

    public static implicit operator long(CounterValue v) {
        return v.Value;
    }

    public static implicit operator CounterValue(long v) {
        return new(v);
    }

    public override string ToString() {
        return this.Value.ToString();
    }
}