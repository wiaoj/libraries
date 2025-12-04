namespace Wiaoj.Consensus.Raft.Abstractions;

// AppendEntries RPC için argümanlar
public record AppendEntriesArgs {
    public required Term Term { get; init; }
    public required NodeId LeaderId { get; init; }
    public required LogIndex PrevLogIndex { get; init; }
    public required Term PrevLogTerm { get; init; }
    public required IReadOnlyList<LogEntry> Entries { get; init; }
    public required LogIndex LeaderCommitIndex { get; init; }
}
