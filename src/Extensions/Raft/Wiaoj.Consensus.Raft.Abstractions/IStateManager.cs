namespace Wiaoj.Consensus.Raft.Abstractions;

/// <summary>
/// Raft'ın çekirdek durum değişkenlerini (CurrentTerm, VotedFor)
/// thread-safe ve kalıcı bir şekilde yönetir.
/// </summary>
public interface IStateManager {
    Term GetCurrentTerm();
    ValueTask SetCurrentTermAsync(Term term);
    NodeId? GetVotedFor();
    ValueTask SetVotedForAsync(NodeId? candidateId);
    ValueTask<bool> StepDownIfGreaterTermAsync(Term rpcTerm);
}