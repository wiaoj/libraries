using System.Collections;

namespace Wiaoj.Ddd.Abstractions.ValueObjects;
/// <summary>
/// Represents a database concurrency token (Optimistic Concurrency Control).
/// Replaces raw byte[] to prevent primitive obsession.
/// </summary>
public readonly struct RowVersion : IEquatable<RowVersion>, IStructuralEquatable {
    private readonly byte[] _value;

    public static RowVersion Empty { get; } = new([]);

    public byte[] Value => this._value ?? [];

    private RowVersion(byte[] value) {
        this._value = value;
    }

    public static RowVersion From(byte[] bytes) {
        if (bytes is null || bytes.Length == 0) {
            return Empty;
        }

        return new RowVersion(bytes);
    }

    // --- Equality ---
    public bool Equals(RowVersion other) {
        return StructuralComparisons.StructuralEqualityComparer.Equals(this.Value, other.Value);
    }

    public override bool Equals(object? obj) {
        return obj is RowVersion other && Equals(other);
    }

    public override int GetHashCode() {
        return StructuralComparisons.StructuralEqualityComparer.GetHashCode(this.Value);
    }

    public static bool operator ==(RowVersion left, RowVersion right) {
        return left.Equals(right);
    }

    public static bool operator !=(RowVersion left, RowVersion right) {
        return !left.Equals(right);
    }

    // --- Interface for Structural Equality (Array comparison) ---
    bool IStructuralEquatable.Equals(object? other, IEqualityComparer comparer) {
        if (other is RowVersion rv) {
            return comparer.Equals(this.Value, rv.Value);
        }
        return false;
    }

    int IStructuralEquatable.GetHashCode(IEqualityComparer comparer) {
        return comparer.GetHashCode(this.Value);
    }

    public override string ToString() {
        return Convert.ToBase64String(this.Value);
    }
}