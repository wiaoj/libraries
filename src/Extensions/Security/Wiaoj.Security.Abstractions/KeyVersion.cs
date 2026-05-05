namespace Wiaoj.Security;
/// <summary>
/// A strongly-typed, validated wrapper for an encryption key version number.
/// Prevents a raw <see cref="int"/> from being accidentally used as a key version
/// and provides meaningful comparison semantics for rotation checks.
/// </summary>
/// <remarks>
/// <para>
/// <b>Conversion policy:</b><br/>
/// Implicit conversion <c>KeyVersion → int</c> is allowed so that dictionary lookups
/// and arithmetic remain ergonomic.<br/>
/// Conversion <c>int → KeyVersion</c> is <b>explicit</b> to prevent accidental coercion
/// from arbitrary integer expressions.
/// </para>
/// <para>
/// <b>Comparison semantics:</b><br/>
/// <c>v1 &lt; v2</c> means v1 is older than v2.
/// A stored secret whose <see cref="KeyVersion"/> is less than the ring's
/// <c>CurrentVersion</c> should be rotated.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// KeyVersion current  = KeyVersion.Of(3);
/// KeyVersion onSecret = KeyVersion.Of(1);
///
/// bool needsRotation = onSecret &lt; current;  // true
///
/// // Explicit cast needed — prevents int leaking in accidentally:
/// KeyVersion fromInt = (KeyVersion)someInt;
/// </code>
/// </example>
public readonly record struct KeyVersion : IComparable<KeyVersion> {

    /// <summary>The underlying integer value.</summary>
    public int Value { get; }

    private KeyVersion(int value) {
        Value = value;
    }

    // ── Factory ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a <see cref="KeyVersion"/> from a non-negative integer.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="value"/> is negative.
    /// </exception>
    public static KeyVersion Of(int value) {
        if(value < 0)
            throw new ArgumentOutOfRangeException(
                nameof(value), value,
                "Key version must be non-negative.");
        return new(value);
    }

    // ── Conversions ───────────────────────────────────────────────────────────

    /// <summary>
    /// Implicit <see cref="KeyVersion"/> → <see cref="int"/>.
    /// Allows ergonomic use in dictionary lookups and arithmetic without losing type safety.
    /// </summary>
    public static implicit operator int(KeyVersion v) => v.Value;

    /// <summary>
    /// Explicit <see cref="int"/> → <see cref="KeyVersion"/>.
    /// Requires a deliberate cast to prevent raw integers from silently flowing into version slots.
    /// </summary>
    public static explicit operator KeyVersion(int v) => Of(v);

    // ── Comparison operators ──────────────────────────────────────────────────

    /// <inheritdoc/>
    public int CompareTo(KeyVersion other) => Value.CompareTo(other.Value);

    /// <summary>Returns <see langword="true"/> if <paramref name="left"/> is an older version than <paramref name="right"/>.</summary>
    public static bool operator <(KeyVersion left, KeyVersion right) => left.Value < right.Value;

    /// <summary>Returns <see langword="true"/> if <paramref name="left"/> is a newer version than <paramref name="right"/>.</summary>
    public static bool operator >(KeyVersion left, KeyVersion right) => left.Value > right.Value;

    /// <inheritdoc cref="op_LessThan"/>
    public static bool operator <=(KeyVersion left, KeyVersion right) => left.Value <= right.Value;

    /// <inheritdoc cref="op_GreaterThan"/>
    public static bool operator >=(KeyVersion left, KeyVersion right) => left.Value >= right.Value;

    // ── Display ───────────────────────────────────────────────────────────────

    /// <summary>Returns a concise, log-friendly representation such as <c>v3</c>.</summary>
    public override string ToString() => $"v{Value}";
}