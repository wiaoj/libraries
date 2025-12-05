using Wiaoj.Consensus.Raft.Abstractions;
using Wiaoj.Results;

namespace Wiaoj.Consensus.Raft;

public interface IRaftCommand;

public record ProcessVoteRequest(
    RequestVoteArgs Args,
    TaskCompletionSource<RequestVoteResult> Tcs) : IRaftCommand;

public record ProcessAppendEntries(
    AppendEntriesArgs Args,
    TaskCompletionSource<AppendEntriesResult> Tcs) : IRaftCommand;
   
public record ProcessInstallSnapshot(
    InstallSnapshotArgs Args,
    TaskCompletionSource<InstallSnapshotResult> Tcs) : IRaftCommand;

public record ProcessClientProposal(
    CommandPayload Command,
    TaskCompletionSource<Result<CommandPayload>> Tcs) : IRaftCommand;

public record InternalTimeoutElapsed(string TimerName) : IRaftCommand;

public record ProcessTermResponse(
    NodeId SenderId, // Yanıtı kimin gönderdiği
    Term ResponseTerm // Yanıttaki Term değeri
) : IRaftCommand;