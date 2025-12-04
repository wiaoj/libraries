//using Microsoft.Extensions.Logging;
//using Wiaoj.Consensus.Raft.Abstractions;
//using Wiaoj.Extensions;
//using Wiaoj.Results;

//namespace Wiaoj.Consensus.Raft.Roles;

//public sealed class CandidateRole : IRaftRole {
//    private readonly RaftEngine _engine;
//    private readonly IStateManager _stateManager;
//    private readonly IRaftLog _raftLog;
//    private readonly IRaftCluster _cluster;
//    private readonly RaftNodeOptions _options;
//    private readonly ILogger<CandidateRole> _logger;
//    private CancellationTokenSource? _roleCts;

//    public NodeState State => NodeState.Candidate;

//    public CandidateRole(RaftEngine engine, IStateManager stateManager, IRaftLog raftLog, IRaftCluster cluster, RaftNodeOptions options, ILogger<CandidateRole> logger) {
//        _engine = engine;
//        _stateManager = stateManager;
//        _raftLog = raftLog;
//        _cluster = cluster;
//        _options = options;
//        _logger = logger;
//    }

//    public Task Enter(CancellationToken cancellationToken) {
//        _roleCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
//        _logger.LogInformation("Aday rolü aktive edildi.");
//        // Arka planda seçim kampanyasını başlat.
//        _ = RunElectionCampaignAsync(_roleCts.Token);
//        return Task.CompletedTask;
//    }

//    public Task LeaveAsync() {
//        _roleCts?.Cancel();
//        _roleCts?.Dispose();
//        return Task.CompletedTask;
//    }

//    // --- DÜZELTME 1: SONSUZ DÖNGÜYÜ KIR ---
//    private async Task RunElectionCampaignAsync(CancellationToken ct) {
//        // Bu metot artık bir döngü değil. Sadece TEK BİR seçim turu dener.
//        // Eğer bu tur başarısız olursa, takipçi rolüne geri döner.

//        if (ct.IsCancellationRequested) return;

//        _logger.LogInformation("Yeni bir seçim kampanyası başlıyor.");

//        bool wonElection = await HoldElectionAsync(ct);

//        // Eğer bu süreçte rolümüz zaten değiştiyse (örn: başka bir lider bulundu) veya kazandıysak, yapacak bir şey yok.
//        if (ct.IsCancellationRequested || wonElection) {
//            return;
//        }

//        // --- EN KRİTİK DEĞİŞİKLİK ---
//        // Seçimi kazanamadık. İnatla tekrar denemek yerine, TAKİPÇİ oluyoruz.
//        // Bu, "split vote" döngülerini kırar. Başka bir düğümün zaman aşımı daha erken
//        // tetiklenebilir ve o kazanabilir. Eğer kimse kazanmazsa, bizim kendi 
//        // Follower zaman aşımımız tekrar tetiklenecek ve yeni bir dönemde tekrar aday olacağız.
//        // Bu, Raft'ın doğal akışıdır.
//        _logger.LogWarning("Seçim kazanılamadı. Takipçi rolüne geri dönülüyor ve yeni bir zaman aşımı bekleniyor.");
//        await _engine.TransitionToAsync(NodeState.Follower);
//    }

//    private async Task<bool> HoldElectionAsync(CancellationToken ct) {
//        // Seçim için kendi zaman aşımımızı (timeout) belirliyoruz.
//        using var electionTimeoutCts = new CancellationTokenSource(_options.ElectionTimeout);
//        // Hem rolün genel iptal token'ını hem de bu seçimin kendi token'ını birleştiriyoruz.
//        using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, electionTimeoutCts.Token);

//        try {
//            Term newTerm = await IncrementTermAndVoteForSelf();
//            _logger.LogInformation("Dönem {Term} için yeni seçim başlatıldı. Oylar isteniyor...", newTerm);

//            int votesGranted = 1;
//            int quorum = (_cluster.TotalNodes / 2) + 1;

//            var (lastLogTerm, lastLogIndex) = _raftLog.GetLastEntryInfo();
//            RequestVoteArgs args = new() {
//                Term = newTerm,
//                CandidateId = NodeId.From(_options.NodeId),
//                LastLogIndex = lastLogIndex,
//                LastLogTerm = lastLogTerm
//            };

//            List<Task<bool>> tasks = _cluster.Peers
//                .Select(peer => RequestVoteFromPeerAsync(peer, args, combinedCts.Token))
//                .ToList();

//            while (tasks.Count > 0) {
//                var completedTask = await Task.WhenAny(tasks);
//                tasks.Remove(completedTask);

//                if (ct.IsCancellationRequested) return false;

//                if (await completedTask) {
//                    if (Interlocked.Increment(ref votesGranted) >= quorum) {
//                        _logger.LogInformation("Çoğunluk ({Quorum}) sağlandı. Lider olunuyor...", quorum);
//                        if (!ct.IsCancellationRequested) {
//                            await _engine.TransitionToAsync(NodeState.Leader);
//                            return true;
//                        }
//                        return false;
//                    }
//                }
//            }
//            return false;
//        }
//        catch (OperationCanceledException) {
//            if (ct.IsCancellationRequested)
//                _logger.LogInformation("Aday rolü iptal edildiği için seçim turu sonlandırıldı.");
//            else
//                _logger.LogWarning("Seçim turu zaman aşımına uğradı.");
//            return false;
//        }
//        catch (Exception ex) {
//            _logger.LogError(ex, "Seçim turunda beklenmedik hata oluştu.");
//            return false;
//        }
//    }

//    private async Task<Term> IncrementTermAndVoteForSelf() {
//        Term currentTerm = _stateManager.GetCurrentTerm();
//        Term newTerm = ++currentTerm;
//        await _stateManager.SetCurrentTermAsync(newTerm);
//        await _stateManager.SetVotedForAsync(NodeId.From(_options.NodeId));
//        return newTerm;
//    }

//    private async Task<bool> RequestVoteFromPeerAsync(IRaftPeer peer, RequestVoteArgs args, CancellationToken ct) {
//        try {
//            RequestVoteResult result = await peer.RequestVoteAsync(args, ct);

//            if (ct.IsCancellationRequested) return false;

//            if (result.Term > args.Term) {
//                _engine.ProcessHigherTerm(peer.Id, result.Term);
//                return false;
//            }

//            return result.VoteGranted;
//        }
//        catch (OperationCanceledException) {
//            _logger.LogWarning("Oy isteme RPC'si {PeerId} için iptal edildi veya zaman aşımına uğradı.", peer.Id);
//        }
//        catch (Exception ex) {
//            _logger.LogError(ex, "{PeerId} adresinden oy istenirken genel bir hata oluştu.", peer.Id);
//        }
//        return false;
//    }

//    // --- DÜZELTME 2: RPC HANDLER'LARIN DOĞRU MANTIĞI ---

//    public async Task<AppendEntriesResult> HandleAppendEntriesAsync(AppendEntriesArgs args) {
//        // Kural: Bir aday, kendisinden daha yüksek VEYA EŞİT term'e sahip bir liderden
//        // AppendEntries alırsa, o lider meşrudur ve aday takipçi olmalıdır.
//        if (args.Term >= _stateManager.GetCurrentTerm()) {
//            _logger.LogInformation("Geçerli bir lider ({LeaderId}) keşfedildi. Takipçi olunuyor.", args.LeaderId);

//            if (args.Term > _stateManager.GetCurrentTerm()) {
//                // Sadece term gerçekten daha büyükse state manager üzerinden güncelle.
//                await _stateManager.StepDownIfGreaterTermAsync(args.Term);
//            }

//            await _engine.TransitionToAsync(NodeState.Follower);
//        }

//        return new AppendEntriesResult { Term = _stateManager.GetCurrentTerm(), Success = false };
//    }

//    public async Task<RequestVoteResult> HandleRequestVoteAsync(RequestVoteArgs args) {
//        // Kural: Eğer gelen adayın term'i bizimkinden KESİNLİKLE büyükse,
//        // tartışmasız takipçi oluruz.
//        if (await _stateManager.StepDownIfGreaterTermAsync(args.Term)) {
//            _logger.LogInformation("Daha yüksek dönemli bir aday ({CandidateId}) keşfedildi. Takipçi olunuyor.", args.CandidateId);
//            await _engine.TransitionToAsync(NodeState.Follower);
//        }

//        // Gelen adayın term'i bizimkine eşit veya daha küçükse, oy vermeyiz çünkü biz de adayız.
//        return new RequestVoteResult { Term = _stateManager.GetCurrentTerm(), VoteGranted = false };
//    }

//    public Task<ErrorOr<CommandPayload>> ProposeAsync(CommandPayload command) {
//        Error error = Error.Conflict.With("Raft.NotLeader", "Düğüm adaydır ve önerileri işleyemez. Lider seçilmesi bekleniyor.");
//        return Task.FromResult<ErrorOr<CommandPayload>>(error);
//    }
//}

// --- START OF FILE CandidateRole.cs (EKSİKSİZ VE SNAPSHOT DESTEKLİ VERSİYON) ---

using Microsoft.Extensions.Logging;
using Wiaoj.Consensus.Raft.Abstractions;
using Wiaoj.Extensions;
using Wiaoj.Results;

namespace Wiaoj.Consensus.Raft.Roles;

public sealed class CandidateRole : IRaftRole {
    private readonly RaftEngine _engine;
    private readonly IStateManager _stateManager;
    private readonly IRaftLog _raftLog;
    private readonly IRaftCluster _cluster;
    private readonly RaftNodeOptions _options;
    private readonly ILogger<CandidateRole> _logger;
    private CancellationTokenSource? _roleCts;

    public NodeState State => NodeState.Candidate;

    public CandidateRole(RaftEngine engine, IStateManager stateManager, IRaftLog raftLog, IRaftCluster cluster, RaftNodeOptions options, ILogger<CandidateRole> logger) {
        _engine = engine;
        _stateManager = stateManager;
        _raftLog = raftLog;
        _cluster = cluster;
        _options = options;
        _logger = logger;
    }

    public Task Enter(CancellationToken cancellationToken) {
        _roleCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _logger.LogInformation("Aday rolü aktive edildi.");
        _ = RunElectionCampaignAsync(_roleCts.Token);
        return Task.CompletedTask;
    }

    public Task LeaveAsync() {
        _roleCts?.Cancel();
        _roleCts?.Dispose();
        return Task.CompletedTask;
    }

    private async Task RunElectionCampaignAsync(CancellationToken ct) {
        if (ct.IsCancellationRequested) return;

        _logger.LogInformation("Yeni bir seçim kampanyası başlıyor.");

        bool wonElection = await HoldElectionAsync(ct);

        if (ct.IsCancellationRequested || wonElection) {
            return;
        }

        _logger.LogWarning("Seçim kazanılamadı. Takipçi rolüne geri dönülüyor ve yeni bir zaman aşımı bekleniyor.");
        await _engine.TransitionToAsync(NodeState.Follower);
    }

    private async Task<bool> HoldElectionAsync(CancellationToken ct) {
        using var electionTimeoutCts = new CancellationTokenSource(_options.ElectionTimeout);
        using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, electionTimeoutCts.Token);

        try {
            Term newTerm = await IncrementTermAndVoteForSelf();
            _logger.LogInformation("Dönem {Term} için yeni seçim başlatıldı. Oylar isteniyor...", newTerm);

            int votesGranted = 1;
            int quorum = (_cluster.TotalNodes / 2) + 1;

            var (lastLogTerm, lastLogIndex) = _raftLog.GetLastEntryInfo();
            RequestVoteArgs args = new() {
                Term = newTerm,
                CandidateId = NodeId.From(_options.NodeId),
                LastLogIndex = lastLogIndex,
                LastLogTerm = lastLogTerm
            };

            List<Task<bool>> tasks = _cluster.Peers
                .Select(peer => RequestVoteFromPeerAsync(peer, args, combinedCts.Token))
                .ToList();

            while (tasks.Count > 0) {
                var completedTask = await Task.WhenAny(tasks);
                tasks.Remove(completedTask);

                if (ct.IsCancellationRequested) return false;

                if (await completedTask) {
                    if (Interlocked.Increment(ref votesGranted) >= quorum) {
                        _logger.LogInformation("Çoğunluk ({Quorum}) sağlandı. Lider olunuyor...", quorum);
                        if (!ct.IsCancellationRequested) {
                            await _engine.TransitionToAsync(NodeState.Leader);
                            return true;
                        }
                        return false;
                    }
                }
            }
            return false;
        }
        catch (OperationCanceledException) {
            if (ct.IsCancellationRequested)
                _logger.LogInformation("Aday rolü iptal edildiği için seçim turu sonlandırıldı.");
            else
                _logger.LogWarning("Seçim turu zaman aşımına uğradı.");
            return false;
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Seçim turunda beklenmedik hata oluştu.");
            return false;
        }
    }

    private async Task<Term> IncrementTermAndVoteForSelf() {
        Term currentTerm = _stateManager.GetCurrentTerm();
        Term newTerm = ++currentTerm;
        await _stateManager.SetCurrentTermAsync(newTerm);
        await _stateManager.SetVotedForAsync(NodeId.From(_options.NodeId));
        return newTerm;
    }

    private async Task<bool> RequestVoteFromPeerAsync(IRaftPeer peer, RequestVoteArgs args, CancellationToken ct) {
        try {
            RequestVoteResult result = await peer.RequestVoteAsync(args, ct);

            if (ct.IsCancellationRequested) return false;

            if (result.Term > args.Term) {
                _engine.ProcessHigherTerm(peer.Id, result.Term);
                return false;
            }

            return result.VoteGranted;
        }
        catch (OperationCanceledException) {
            _logger.LogWarning("Oy isteme RPC'si {PeerId} için iptal edildi veya zaman aşımına uğradı.", peer.Id);
        }
        catch (Exception ex) {
            _logger.LogError(ex, "{PeerId} adresinden oy istenirken genel bir hata oluştu.", peer.Id);
        }
        return false;
    }

    public async Task<AppendEntriesResult> HandleAppendEntriesAsync(AppendEntriesArgs args) {
        if (args.Term >= _stateManager.GetCurrentTerm()) {
            _logger.LogInformation("Geçerli bir lider ({LeaderId}) keşfedildi. Takipçi olunuyor.", args.LeaderId);

            if (args.Term > _stateManager.GetCurrentTerm()) {
                await _stateManager.StepDownIfGreaterTermAsync(args.Term);
            }

            await _engine.TransitionToAsync(NodeState.Follower);
        }

        return new AppendEntriesResult { Term = _stateManager.GetCurrentTerm(), Success = false };
    }

    public async Task<RequestVoteResult> HandleRequestVoteAsync(RequestVoteArgs args) {
        if (await _stateManager.StepDownIfGreaterTermAsync(args.Term)) {
            _logger.LogInformation("Daha yüksek dönemli bir aday ({CandidateId}) keşfedildi. Takipçi olunuyor.", args.CandidateId);
            await _engine.TransitionToAsync(NodeState.Follower);
        }

        return new RequestVoteResult { Term = _stateManager.GetCurrentTerm(), VoteGranted = false };
    }

    public Task<ErrorOr<CommandPayload>> ProposeAsync(CommandPayload command) {
        Error error = Error.Conflict.With("Raft.NotLeader", "Düğüm adaydır ve önerileri işleyemez. Lider seçilmesi bekleniyor.");
        return Task.FromResult<ErrorOr<CommandPayload>>(error);
    }

    public async Task<InstallSnapshotResult> HandleInstallSnapshotAsync(InstallSnapshotArgs args) {
        var currentTerm = _stateManager.GetCurrentTerm();
        if (args.Term < currentTerm) {
            return new InstallSnapshotResult { Term = currentTerm };
        }

        if (args.Term >= currentTerm) {
            _logger.LogInformation("Snapshot gönderen geçerli bir lider ({LeaderId}) keşfedildi. Takipçi olunuyor.", args.LeaderId);
            await _stateManager.StepDownIfGreaterTermAsync(args.Term);
            await _engine.TransitionToAsync(NodeState.Follower);
        }

        return new InstallSnapshotResult { Term = _stateManager.GetCurrentTerm() };
    }
}