using MemoryPack;

namespace Wiaoj.Consensus.Raft.Abstractions;

/// <summary>
/// Raft log'undaki tek bir girdiyi temsil eder. Bu yapı, FASTER'da saklanacak ve
/// ağ üzerinden gönderilecektir, bu nedenle MemoryPack ile serileştirilebilir olmalıdır.
/// </summary>
[MemoryPackable]
public partial record LogEntry {
    /// <summary>
    /// Bu log girdisinin lider tarafından alındığı dönem (term).
    /// </summary>
    public required long Term { get; init; }

    /// <summary>
    /// Durum makinesine uygulanacak olan komut.
    /// </summary>
    public required byte[] Command { get; init; }
}