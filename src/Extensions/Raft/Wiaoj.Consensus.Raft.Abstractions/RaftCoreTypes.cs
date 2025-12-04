namespace Wiaoj.Consensus.Raft.Abstractions;

/// <summary>
/// Raft dönemini (Term) temsil eden, değişmez ve karşılaştırılabilir bir yapı.
/// </summary>
public readonly record struct Term(long Value) : IComparable<Term> {
    public static readonly Term Zero = new(0);
    public int CompareTo(Term other) {
        return this.Value.CompareTo(other.Value);
    }

    public static Term operator ++(Term term) {
        return new(term.Value + 1);
    }

    public override string ToString() {
        return this.Value.ToString();
    }

    public static bool operator >(Term left, Term right) {
        return left.Value > right.Value;
    }

    public static bool operator <(Term left, Term right) {
        return left.Value < right.Value;
    }

    public static bool operator >=(Term left, Term right) {
        return left.Value >= right.Value;
    }

    public static bool operator <=(Term left, Term right) {
        return left.Value <= right.Value;
    }
}

/// <summary>
/// Raft log'undaki bir indeksi temsil eden, değişmez ve karşılaştırılabilir bir yapı.
/// </summary>
public readonly record struct LogIndex(long Value) : IComparable<LogIndex> {
    public static readonly LogIndex Zero = new(0);
    public int CompareTo(LogIndex other) {
        return this.Value.CompareTo(other.Value);
    }

    public static LogIndex operator ++(LogIndex index) {
        return new(index.Value + 1);
    }

    public static LogIndex operator --(LogIndex index) {
        return new(index.Value - 1);
    }

    public static long operator -(LogIndex left, LogIndex right) {
        return left.Value - right.Value;
    }

    public static LogIndex operator +(LogIndex index, long value) {
        return new(index.Value + value);
    }

    // --- YENİ EKLENEN OPERATÖRLER ---
    public static LogIndex operator -(LogIndex index, int value) {
        return new(index.Value - value);
    }

    public static LogIndex operator -(LogIndex index, long value) {
        return new(index.Value - value);
    }

    public static LogIndex Max(LogIndex a, LogIndex b) {
        return a.Value > b.Value ? a : b;
    }

    public static LogIndex Min(LogIndex a, LogIndex b) {
        return a.Value < b.Value ? a : b;
    }

    public override string ToString() {
        return this.Value.ToString();
    }

    public static bool operator >(LogIndex left, LogIndex right) {
        return left.Value > right.Value;
    }

    public static bool operator <(LogIndex left, LogIndex right) {
        return left.Value < right.Value;
    }

    public static bool operator >=(LogIndex left, LogIndex right) {
        return left.Value >= right.Value;
    }

    public static bool operator <=(LogIndex left, LogIndex right) {
        return left.Value <= right.Value;
    }
}

/// <summary>
/// Kümedeki bir düğümün kimliğini temsil eden, değişmez bir yapı.
/// </summary>
public readonly record struct NodeId(string Value) {
    public static NodeId From(string value) {
        Preca.ThrowIfNullOrWhiteSpace(value);
        return new NodeId(value);
    }
    public override string ToString() {
        return this.Value;
    }
}