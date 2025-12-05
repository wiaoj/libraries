using Wiaoj.Results;

namespace Wiaoj.Consensus.Raft.Abstractions;

/// <summary>
/// Raft düğümünün ana orkestrasyon motoru. Dış dünyadan gelen
/// RPC çağrılarını ve istemci isteklerini yönetir.
/// </summary>
public interface IRaftEngine {
    /// <summary>
    /// Raft motorunu başlatır.
    /// </summary>
    Task StartAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Gelen bir RequestVote RPC'sini işler.
    /// </summary>
    Task<RequestVoteResult> HandleRequestVoteAsync(RequestVoteArgs args);

    /// <summary>
    /// Gelen bir AppendEntries RPC'sini işler.
    /// </summary>
    Task<AppendEntriesResult> HandleAppendEntriesAsync(AppendEntriesArgs args);

    /// <summary>
    /// İstemciden gelen yeni bir komut önerisini işleme alır.
    /// </summary>
    Task<Result<CommandPayload>> ProposeAsync(CommandPayload command);

    Task<InstallSnapshotResult> HandleInstallSnapshotAsync(InstallSnapshotArgs args);
} 