namespace Wiaoj.Consensus.Raft.Abstractions;

public enum NodeState {
    Follower,
    Candidate,
    Leader
}