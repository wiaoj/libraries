namespace Wiaoj.Consensus.Raft.Abstractions;

// AppendEntries RPC için sonuç
public record AppendEntriesResult {
    public required Term Term { get; init; }
    public required bool Success { get; init; }
}
