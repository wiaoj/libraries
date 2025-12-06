namespace Wiaoj.Primitives.Snowflake;
/// <summary>
/// Represents the number of bits allocated for the sequence part of a Snowflake ID.
/// Encapsulates validation logic to ensure the bit count is within the safe range (1-22).
/// </summary>
public readonly record struct SequenceBits : IComparable<SequenceBits> {
    /// <summary>Default sequence bits (12 bits = 4096 IDs per ms).</summary>
    public static readonly SequenceBits Default = new(12);

    /// <summary>Minimum allowed bits (1 bit = 2 IDs per ms).</summary>
    public const byte MinValue = 1;

    /// <summary>
    /// Maximum allowed bits.
    /// <para>Calculation: 64 Total - 1 Sign - 41 Timestamp = 22 Remaining.</para>
    /// </summary>
    public const byte MaxValue = 22;

    /// <summary>
    /// Gets the underlying byte value.
    /// </summary>
    public byte Value { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SequenceBits"/> struct.
    /// </summary>
    /// <param name="value">The number of bits (1-22).</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if value is out of range.</exception>
    public SequenceBits(byte value) {
        if (value is < MinValue or > MaxValue) {
            throw new ArgumentOutOfRangeException(nameof(value), value,
                $"SequenceBits must be between {MinValue} and {MaxValue}.");
        }
        this.Value = value;
    }

    /// <summary>
    /// Implicit conversion from byte to SequenceBits.
    /// Allows: <c>options.SequenceBits = 12;</c>
    /// </summary>
    public static implicit operator SequenceBits(byte value) {
        return new(value);
    }

    /// <summary>
    /// Implicit conversion from int (for convenience) to SequenceBits.
    /// Allows: <c>options.SequenceBits = 12;</c> (int literal)
    /// </summary>
    public static implicit operator SequenceBits(int value) {
        return new((byte)value);
    }

    /// <summary>
    /// Implicit conversion from SequenceBits to byte.
    /// Allows: <c>byte b = options.SequenceBits;</c>
    /// </summary>
    public static implicit operator byte(SequenceBits bits) {
        return bits.Value;
    }

    /// <summary>
    /// Implicit conversion from SequenceBits to int.
    /// </summary>
    public static implicit operator int(SequenceBits bits) {
        return bits.Value;
    }

    public override string ToString() {
        return this.Value.ToString();
    }

    public int CompareTo(SequenceBits other) {
        return this.Value.CompareTo(other.Value);
    }
}