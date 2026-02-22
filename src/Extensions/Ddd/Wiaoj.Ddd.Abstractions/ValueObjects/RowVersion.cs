namespace Wiaoj.Ddd.ValueObjects;
/// <summary>
/// Represents a database concurrency token (Optimistic Concurrency Control).
/// Replaces raw byte[] to prevent primitive obsession.
/// </summary>
public readonly struct RowVersion : IEquatable<RowVersion> {
    private readonly byte[] _value;

    public static RowVersion Empty { get; } = new([]);

    public byte[] Value => this._value ?? [];

    private RowVersion(byte[] value) {
        this._value = value;
    }

    public static RowVersion From(byte[] bytes) {
        if(bytes is null || bytes.Length == 0) {
            return Empty;
        }

        return new RowVersion(bytes);
    }

    // --- Equality ---
    public bool Equals(RowVersion other) {
        return this.Value.AsSpan().SequenceEqual(other.Value.AsSpan());
    }

    public override bool Equals(object? obj) {
        return obj is RowVersion other && Equals(other);
    }

    public override int GetHashCode() {
        HashCode hash = new(); 
        hash.AddBytes(this.Value.AsSpan());
        return hash.ToHashCode();
    }

    public static bool operator ==(RowVersion left, RowVersion right) {
        return left.Equals(right);
    }

    public static bool operator !=(RowVersion left, RowVersion right) {
        return !left.Equals(right);
    }

    public override string ToString() {
        return Convert.ToBase64String(this.Value);
    }
}