using Wiaoj.Results;

namespace Wiaoj.Consensus.Raft.Abstractions;

/// <summary>
/// Bir Raft düğümünün Follower, Candidate veya Leader gibi bir rolünün davranışını tanımlar.
/// Bu, State Tasarım Deseninin bir parçasıdır.
/// </summary>
public interface IRaftRole {
    /// <summary>
    /// Bu rolün temsil ettiği durumu alır.
    /// </summary>
    NodeState State { get; }

    /// <summary>
    /// Düğüm bu role geçtiğinde çağrılır. Zamanlayıcıları başlatmak gibi kurulum işlemleri için kullanılır.
    /// </summary>
    Task Enter(CancellationToken cancellationToken);

    /// <summary>
    /// Düğüm bu rolden ayrıldığında çağrılır. Zamanlayıcıları durdurmak gibi temizlik işlemleri için kullanılır.
    /// </summary>
    Task LeaveAsync();

    /// <summary>
    /// Gelen bir RequestVote RPC'sini bu rolün mantığına göre işler.
    /// </summary>
    Task<RequestVoteResult> HandleRequestVoteAsync(RequestVoteArgs args);

    /// <summary>
    /// Gelen bir AppendEntries RPC'sini bu rolün mantığına göre işler.
    /// </summary>
    Task<AppendEntriesResult> HandleAppendEntriesAsync(AppendEntriesArgs args);

    /// <summary>
    /// İstemciden gelen bir komut önerisini işler.
    /// </summary>
    Task<ErrorOr<CommandPayload>> ProposeAsync(CommandPayload command);

    Task<InstallSnapshotResult> HandleInstallSnapshotAsync(InstallSnapshotArgs args);
} 