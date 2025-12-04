namespace Wiaoj.Consensus.Raft.Abstractions;

// RequestVote RPC için argümanlar
public record RequestVoteArgs {
    public required Term Term { get; init; }
    public required NodeId CandidateId { get; init; }
    public required LogIndex LastLogIndex { get; init; }
    public required Term LastLogTerm { get; init; }
}
