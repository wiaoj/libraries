namespace Wiaoj.Consensus.Raft.Abstractions;

/// <summary>
/// Kümedeki tek bir uzak düğümü (peer) temsil eder ve ona RPC gönderme yeteneği sağlar.
/// </summary>
public interface IRaftPeer {
    /// <summary>
    /// Düğümün benzersiz kimliği.
    /// </summary>
    NodeId Id { get; }

    /// <summary>
    /// Bu düğüme bir RequestVote RPC'si gönderir.
    /// </summary>
    Task<RequestVoteResult> RequestVoteAsync(RequestVoteArgs args, CancellationToken cancellationToken);

    /// <summary>
    /// Bu düğüme bir AppendEntries RPC'si gönderir.
    /// </summary>
    Task<AppendEntriesResult> AppendEntriesAsync(AppendEntriesArgs args, CancellationToken cancellationToken);

    Task<InstallSnapshotResult> InstallSnapshotAsync(InstallSnapshotArgs args, CancellationToken cancellationToken);
}