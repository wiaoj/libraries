//using Microsoft.Extensions.Logging;
//using Wiaoj.Consensus.Raft.Abstractions;
//using Wiaoj.Extensions;
//using Wiaoj.Results;

//namespace Wiaoj.Consensus.Raft.Roles;

//public sealed class FollowerRole : IRaftRole {
//    private readonly RaftEngine _engine;
//    private readonly IStateManager _stateManager;
//    private readonly IRaftLog _raftLog;
//    private readonly RaftNodeOptions _options;
//    private readonly ILogger<FollowerRole> _logger;
//    private Timer? _electionTimer;
//    private CancellationToken _cancellationToken;
//    private NodeId? _currentLeader;

//    public NodeState State => NodeState.Follower;

//    public FollowerRole(RaftEngine engine, IStateManager stateManager, IRaftLog raftLog, RaftNodeOptions options, ILogger<FollowerRole> logger) {
//        this._engine = engine;
//        this._stateManager = stateManager;
//        this._raftLog = raftLog;
//        this._options = options;
//        this._logger = logger;
//    }

//    public Task Enter(CancellationToken cancellationToken) {
//        this._cancellationToken = cancellationToken;
//        this._logger.LogInformation("Takipçi rolü aktive edildi. Dönem: {Term}", this._stateManager.GetCurrentTerm());
//        this._currentLeader = null;
//        ResetElectionTimer();
//        return Task.CompletedTask;
//    }

//    public Task LeaveAsync() {
//        this._electionTimer?.Dispose();
//        this._electionTimer = null;
//        return Task.CompletedTask;
//    }

//    public async Task<AppendEntriesResult> HandleAppendEntriesAsync(AppendEntriesArgs args) {
//        Term currentTerm = this._stateManager.GetCurrentTerm();
//        if (args.Term < currentTerm) {
//            return new AppendEntriesResult { Term = currentTerm, Success = false };
//        }

//        ResetElectionTimer();
//        await this._stateManager.StepDownIfGreaterTermAsync(args.Term);
//        this._currentLeader = args.LeaderId;

//        LogEntry? localEntryAtPrevIndex = this._raftLog.Get(args.PrevLogIndex);
//        if (args.PrevLogIndex > LogIndex.Zero && (localEntryAtPrevIndex == null || localEntryAtPrevIndex.Term != args.PrevLogTerm.Value)) {
//            this._logger.LogWarning("AppendEntries reddedildi. Uyuşmazlık: PrevLogIndex={index}, PrevLogTerm={term}", args.PrevLogIndex, args.PrevLogTerm);
//            return new AppendEntriesResult { Term = this._stateManager.GetCurrentTerm(), Success = false };
//        }

//        LogIndex firstNewEntryIndex = args.PrevLogIndex + 1;
//        LogEntry? existingEntry = this._raftLog.Get(firstNewEntryIndex);
//        if (existingEntry != null && args.Entries.Count > 0 && existingEntry.Term != args.Entries[0].Term) {
//            this._logger.LogInformation("Log'da çakışma tespit edildi. Index {index}'den sonrası siliniyor.", firstNewEntryIndex);
//            await this._raftLog.TruncateAsync(firstNewEntryIndex);
//        }

//        // Kural 4: Log'da olmayan yeni girdileri ekle.
//        // DİKKAT: Bu basit döngü, gelen girdilerin zaten doğru sırada olduğunu varsayar.
//        // Lider, her zaman bir seferde sıralı bir blok gönderir.
//        if (args.Entries.Count > 0) {
//            foreach (LogEntry entry in args.Entries) {
//                await this._raftLog.AppendAsync(entry);
//            }
//        }

//        if (args.LeaderCommitIndex > this._engine.CommitIndex) {
//            LogIndex newCommitIndex = LogIndex.Min(args.LeaderCommitIndex, this._raftLog.LastIndex);
//            this._engine.SetCommitIndex(newCommitIndex);
//        }

//        return new AppendEntriesResult { Term = this._stateManager.GetCurrentTerm(), Success = true };
//    }

//    public async Task<RequestVoteResult> HandleRequestVoteAsync(RequestVoteArgs args) {
//        Term currentTerm = this._stateManager.GetCurrentTerm();

//        // Eğer gelen RPC'nin term'i daha yüksekse, durumumuzu güncelleyelim.
//        // Bu metot zaten VotedFor'u null yapar.
//        if (await this._stateManager.StepDownIfGreaterTermAsync(args.Term)) {
//            // Metot term'i güncellediği için, yerel değişkenimizi de güncelleyelim.
//            currentTerm = args.Term;
//        }

//        NodeId? votedFor = this._stateManager.GetVotedFor();
//        (Term lastLogTerm, LogIndex lastLogIndex) = this._raftLog.GetLastEntryInfo();
//        bool logIsUpToDate = args.LastLogTerm > lastLogTerm || (args.LastLogTerm == lastLogTerm && args.LastLogIndex >= lastLogIndex);
//        bool voteGranted = false;

//        // Sadece şu koşulların HEPSİ doğruysa oy ver:
//        // 1. Adayın term'i bizimkinden küçük DEĞİLSE.
//        // 2. Biz bu term'de ya hiç oy kullanmadıysak YA DA zaten bu adaya oy verdiysek.
//        // 3. Adayın logu en az bizimki kadar güncelse.
//        if (args.Term >= currentTerm &&
//            (votedFor == null || votedFor.Value == args.CandidateId) &&
//            logIsUpToDate) {
//            await this._stateManager.SetVotedForAsync(args.CandidateId);
//            voteGranted = true;
//            this._logger.LogInformation("Dönem {Term} için {CandidateId} adayına oy verildi.", args.Term, args.CandidateId);
//            ResetElectionTimer();
//        }

//        return new RequestVoteResult { Term = this._stateManager.GetCurrentTerm(), VoteGranted = voteGranted };
//    }

//    public Task<ErrorOr<CommandPayload>> ProposeAsync(CommandPayload command) {
//        var leaderHint = this._currentLeader?.Value ?? "Bilinmiyor";
//        Dictionary<string, object> metadata = new() { { "LeaderHint", leaderHint } };
//        Error error = Error.Conflict.With("Raft.NotLeader", "İşlem sadece lider tarafından gerçekleştirilebilir.", metadata);
//        return Task.FromResult<ErrorOr<CommandPayload>>(error);
//    }

//    private void OnElectionTimeout() {
//        if (this._cancellationToken.IsCancellationRequested) return;
//        this._logger.LogInformation("Seçim zaman aşımı tetiklendi. Motora 'OperationTimeout' komutu gönderiliyor.");
//        this._engine.HandleInternalTimeout();
//    }

//    private void ResetElectionTimer() {
//        this._electionTimer?.Dispose();
//        TimeSpan randomizedTimeout = this._options.ElectionTimeout.WithJitter(Jitter.Medium);
//        this._electionTimer = new Timer(_ => OnElectionTimeout(), null, randomizedTimeout, System.Threading.OperationTimeout.InfiniteTimeSpan);
//    }
//}

// --- START OF FILE FollowerRole.cs (EKSİKSİZ VE SNAPSHOT DESTEKLİ VERSİYON) ---

using Microsoft.Extensions.Logging;
using Wiaoj.Consensus.Raft.Abstractions;
using Wiaoj.Extensions;
using Wiaoj.Results;

namespace Wiaoj.Consensus.Raft.Roles;

public sealed class FollowerRole : IRaftRole {
    private readonly RaftEngine _engine;
    private readonly IStateManager _stateManager;
    private readonly IRaftLog _raftLog;
    private readonly RaftNodeOptions _options;
    private readonly ILogger<FollowerRole> _logger;
    private Timer? _electionTimer;
    private CancellationToken _cancellationToken;
    private NodeId? _currentLeader;

    public NodeState State => NodeState.Follower;

    public FollowerRole(RaftEngine engine, IStateManager stateManager, IRaftLog raftLog, RaftNodeOptions options, ILogger<FollowerRole> logger) {
        _engine = engine;
        _stateManager = stateManager;
        _raftLog = raftLog;
        _options = options;
        _logger = logger;
    }

    public Task Enter(CancellationToken cancellationToken) {
        _cancellationToken = cancellationToken;
        _logger.LogInformation("Takipçi rolü aktive edildi. Dönem: {Term}", _stateManager.GetCurrentTerm());
        _currentLeader = null;
        ResetElectionTimer();
        return Task.CompletedTask;
    }

    public Task LeaveAsync() {
        _electionTimer?.Dispose();
        _electionTimer = null;
        return Task.CompletedTask;
    }

    public async Task<AppendEntriesResult> HandleAppendEntriesAsync(AppendEntriesArgs args) {
        Term currentTerm = _stateManager.GetCurrentTerm();
        if (args.Term < currentTerm) {
            return new AppendEntriesResult { Term = currentTerm, Success = false };
        }

        ResetElectionTimer();
        await _stateManager.StepDownIfGreaterTermAsync(args.Term);
        _currentLeader = args.LeaderId;

        if (args.PrevLogIndex < _raftLog.LastSnapshotIndex) {
            _logger.LogWarning("AppendEntries reddedildi. PrevLogIndex ({pIndex}) mevcut snapshot'tan ({sIndex}) daha eski.", args.PrevLogIndex, _raftLog.LastSnapshotIndex);
            return new AppendEntriesResult { Term = _stateManager.GetCurrentTerm(), Success = false };
        }

        LogEntry? localEntryAtPrevIndex = _raftLog.Get(args.PrevLogIndex);
        if (args.PrevLogIndex > _raftLog.LastSnapshotIndex && (localEntryAtPrevIndex == null || localEntryAtPrevIndex.Term != args.PrevLogTerm.Value)) {
            _logger.LogWarning("AppendEntries reddedildi. Uyuşmazlık: PrevLogIndex={index}, PrevLogTerm={term}", args.PrevLogIndex, args.PrevLogTerm);
            return new AppendEntriesResult { Term = _stateManager.GetCurrentTerm(), Success = false };
        }

        if (args.Entries.Count > 0) {
            LogIndex firstNewEntryIndex = args.PrevLogIndex + 1;
            LogEntry? existingEntry = _raftLog.Get(firstNewEntryIndex);
            if (existingEntry != null && existingEntry.Term != args.Entries[0].Term) {
                _logger.LogInformation("Log'da çakışma tespit edildi. Index {index}'den sonrası siliniyor.", firstNewEntryIndex);
                await _raftLog.TruncateAsync(firstNewEntryIndex);
            }
            await _raftLog.AppendEntriesAsync(args.Entries);
        }

        if (args.LeaderCommitIndex > _engine.CommitIndex) {
            LogIndex newCommitIndex = LogIndex.Min(args.LeaderCommitIndex, _raftLog.LastIndex);
            _engine.SetCommitIndex(newCommitIndex);
        }

        return new AppendEntriesResult { Term = _stateManager.GetCurrentTerm(), Success = true };
    }

    public async Task<RequestVoteResult> HandleRequestVoteAsync(RequestVoteArgs args) {
        Term currentTerm = _stateManager.GetCurrentTerm();

        if (await _stateManager.StepDownIfGreaterTermAsync(args.Term)) {
            currentTerm = args.Term;
        }

        NodeId? votedFor = _stateManager.GetVotedFor();
        (Term lastLogTerm, LogIndex lastLogIndex) = _raftLog.GetLastEntryInfo();
        bool logIsUpToDate = args.LastLogTerm > lastLogTerm || (args.LastLogTerm == lastLogTerm && args.LastLogIndex >= lastLogIndex);
        bool voteGranted = false;

        if (args.Term >= currentTerm &&
            (votedFor == null || votedFor.Value == args.CandidateId) &&
            logIsUpToDate) {
            await _stateManager.SetVotedForAsync(args.CandidateId);
            voteGranted = true;
            _logger.LogInformation("Dönem {Term} için {CandidateId} adayına oy verildi.", args.Term, args.CandidateId);
            ResetElectionTimer();
        }

        return new RequestVoteResult { Term = _stateManager.GetCurrentTerm(), VoteGranted = voteGranted };
    }

    public Task<Result<CommandPayload>> ProposeAsync(CommandPayload command) {
        var leaderHint = _currentLeader?.Value ?? "Bilinmiyor";
        Dictionary<string, object> metadata = new() { { "LeaderHint", leaderHint } };
        Error error = Error.Conflict.With("Raft.NotLeader", "İşlem sadece lider tarafından gerçekleştirilebilir.", metadata);
        return Task.FromResult<Result<CommandPayload>>(error);
    }

    public async Task<InstallSnapshotResult> HandleInstallSnapshotAsync(InstallSnapshotArgs args) {
        var currentTerm = _stateManager.GetCurrentTerm();
        var result = new InstallSnapshotResult { Term = currentTerm };

        if (args.Term < currentTerm) {
            return result;
        }

        ResetElectionTimer();
        _currentLeader = args.LeaderId;
        await _stateManager.StepDownIfGreaterTermAsync(args.Term);
        result = new InstallSnapshotResult { Term = _stateManager.GetCurrentTerm() };

        if (_engine.CommitIndex >= args.LastIncludedIndex) {
            _logger.LogWarning("Gelen snapshot (index {new}) mevcut veya eski (commit {current}), yoksayılıyor.", args.LastIncludedIndex, _engine.CommitIndex);
            return result;
        }

        _logger.LogInformation("Liderden snapshot alınıyor. Index={Index}, Term={Term}", args.LastIncludedIndex, args.LastIncludedTerm);

        await _raftLog.CompactLogAsync(args.Data, args.LastIncludedIndex, args.LastIncludedTerm);
        await _engine.RestoreStateMachineFromSnapshotAsync(args.Data);

        _engine.SetCommitIndex(args.LastIncludedIndex);
        _engine.SetLastApplied(args.LastIncludedIndex);

        _logger.LogInformation("Snapshot başarıyla uygulandı.");
        return result;
    }

    private void OnElectionTimeout() {
        if (_cancellationToken.IsCancellationRequested) return;
        _logger.LogInformation("Seçim zaman aşımı tetiklendi. Motora 'OperationTimeout' komutu gönderiliyor.");
        _engine.HandleInternalTimeout();
    }

    private void ResetElectionTimer() {
        _electionTimer?.Dispose();
        TimeSpan randomizedTimeout = _options.ElectionTimeout.WithJitter(Jitter.Medium);
        _electionTimer = new Timer(_ => OnElectionTimeout(), null, randomizedTimeout, System.Threading.Timeout.InfiniteTimeSpan);
    }
}