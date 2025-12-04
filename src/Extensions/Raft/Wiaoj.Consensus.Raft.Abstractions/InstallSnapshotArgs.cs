namespace Wiaoj.Consensus.Raft.Abstractions;

public record InstallSnapshotArgs {
    public required Term Term { get; init; }
    public required NodeId LeaderId { get; init; }
    public required LogIndex LastIncludedIndex { get; init; }
    public required Term LastIncludedTerm { get; init; }
    public required byte[] Data { get; init; }
}

public record InstallSnapshotResult {
    public required Term Term { get; init; }
}