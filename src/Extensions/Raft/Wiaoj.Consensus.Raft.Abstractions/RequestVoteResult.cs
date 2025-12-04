namespace Wiaoj.Consensus.Raft.Abstractions;

// RequestVote RPC için sonuç
public record RequestVoteResult {
    public required Term Term { get; init; }
    public required bool VoteGranted { get; init; }
}
