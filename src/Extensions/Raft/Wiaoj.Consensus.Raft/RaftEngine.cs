//using System.Collections.Concurrent;
//using Microsoft.Extensions.Logging;
//using Wiaoj.Concurrency;
//using Wiaoj.Consensus.Raft.Abstractions;
//using Wiaoj.Consensus.Raft.Roles;
//using Wiaoj.Results;
//using Wiaoj.Threading.Channels;

//namespace Wiaoj.Consensus.Raft;

//public sealed class RaftEngine : IRaftEngine, IAsyncDisposable {
//    private readonly ILogger<RaftEngine> _logger;
//    private readonly IStateManager _stateManager;
//    private readonly IRaftLog _raftLog;
//    private readonly IStateMachine _stateMachine;
//    private readonly RaftNodeOptions _options;
//    private readonly IReadOnlyDictionary<NodeState, IRaftRole> _roles;
//    private readonly AsyncChannel<IRaftCommand> _commandChannel;
//    private readonly CancellationTokenSource _engineCts = new();
//    private readonly AsyncLock _transitionLock = new();
//    private readonly DisposeState _disposeState = new();

//    private readonly ConcurrentDictionary<LogIndex, TaskCompletionSource<ErrorOr<CommandPayload>>> _pendingProposals = new();

//    private Task? _executionTask;
//    private IRaftRole _currentRole;
//    private LogIndex _lastApplied = LogIndex.Zero;
//    private readonly AsyncAutoResetEvent _applySignal = new(false);

//    public LogIndex CommitIndex { get; private set; } = LogIndex.Zero;

//    public RaftEngine(
//      ILogger<RaftEngine> logger,
//      IStateManager stateManager,
//      IRaftLog raftLog,
//      IStateMachine stateMachine,
//      IRaftCluster cluster,
//      RaftNodeOptions options,
//      ILoggerFactory loggerFactory) {
//        _logger = logger;
//        _stateManager = stateManager;
//        _raftLog = raftLog;
//        _stateMachine = stateMachine;
//        _options = options;
//        _commandChannel = AsyncChannel<IRaftCommand>.CreateUnbounded();

//        _roles = new Dictionary<NodeState, IRaftRole> {
//            { NodeState.Follower, new FollowerRole(this, stateManager, raftLog, options, loggerFactory.CreateLogger<FollowerRole>()) },
//            { NodeState.Candidate, new CandidateRole(this, stateManager, raftLog, cluster, options, loggerFactory.CreateLogger<CandidateRole>()) },
//            { NodeState.Leader, new LeaderRole(this, stateManager, raftLog, cluster, options, loggerFactory.CreateLogger<LeaderRole>()) }
//        };
//        _currentRole = _roles[NodeState.Follower];
//    }

//    public Task StartAsync(CancellationToken cancellationToken) {
//        _disposeState.ThrowIfDisposingOrDisposed(nameof(RaftEngine));
//        _logger.LogInformation("Raft Engine başlatılıyor...");

//        CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_engineCts.Token, cancellationToken);

//        _executionTask = Task.Run(() => MainLoopAsync(linkedCts.Token), linkedCts.Token);
//        _ = Task.Run(() => ApplyCommittedEntriesAsync(linkedCts.Token), linkedCts.Token);

//        return Task.CompletedTask;
//    }

//    public object GetStatus() {
//        return new {
//            NodeId = _options.NodeId,  
//            Role = _currentRole.State.ToString(),
//            Term = _stateManager.GetCurrentTerm().Value,
//            CommitIndex = this.CommitIndex.Value,
//            LastApplied = _lastApplied.Value
//        };
//    }

//    private async Task MainLoopAsync(CancellationToken ct) {
//        try {
//            await _currentRole.Enter(ct);

//            await foreach (var command in _commandChannel.Reader.ReadAllAsync(ct)) {
//                switch (command) {
//                    case ProcessTermResponse resp:
//                    // Kural: Gelen herhangi bir RPC yanıtı veya isteği daha yüksek bir Term içeriyorsa,
//                    // derhal Follower ol. Bu, tüm roller için geçerli evrensel bir Raft kuralıdır.
//                    if (await _stateManager.StepDownIfGreaterTermAsync(resp.ResponseTerm)) {
//                        _logger.LogInformation("{PeerId} daha yüksek bir dönemde ({PeerTerm}). Takipçi olunuyor.", resp.SenderId, resp.ResponseTerm);
//                        await TransitionToAsync(NodeState.Follower);
//                    }
//                    break;
//                    case ProcessVoteRequest req:
//                    var voteResult = await _currentRole.HandleRequestVoteAsync(req.Args);
//                    req.Tcs.TrySetResult(voteResult);
//                    break;
//                    case ProcessAppendEntries req:
//                    var appendResult = await _currentRole.HandleAppendEntriesAsync(req.Args);
//                    req.Tcs.TrySetResult(appendResult);
//                    break;
//                    case ProcessClientProposal req:
//                    var proposalResult = await _currentRole.ProposeAsync(req.Command);
//                    req.Tcs.TrySetResult(proposalResult);
//                    break;
//                    case InternalTimeoutElapsed:
//                    if (_currentRole.State == NodeState.Follower) {
//                        _logger.LogInformation("Liderden heartbeat alınamadı, seçim başlatılıyor...");
//                        await TransitionToAsync(NodeState.Candidate);
//                    }
//                    break;
//                }
//            }
//        }
//        catch (OperationCanceledException) {
//            _logger.LogWarning("Raft Engine ana döngüsü iptal edildi.");
//        }
//        catch (Exception ex) {
//            _logger.LogCritical(ex, "Raft Engine ana döngüsünde ölümcül hata!");
//        }
//    }
//    public void HandleInternalTimeout() {
//        _commandChannel.Writer.TryWrite(new InternalTimeoutElapsed("ElectionTimer"));
//    }

//    public void ProcessHigherTerm(NodeId senderId, Term responseTerm) {
//        _commandChannel.Writer.TryWrite(new ProcessTermResponse(senderId, responseTerm));
//    }

//    public async Task TransitionToAsync(NodeState newState) {
//        using (await _transitionLock.LockAsync(_engineCts.Token)) {
//            if (_currentRole.State == newState) return;

//            _logger.LogInformation("Durum değişiyor: {OldState} -> {NewState}", _currentRole.State, newState);

//            await _currentRole.LeaveAsync();
//            _currentRole = _roles[newState];
//            await _currentRole.Enter(_engineCts.Token);
//        }
//    }

//    public Task<RequestVoteResult> HandleRequestVoteAsync(RequestVoteArgs args) {
//        var tcs = new TaskCompletionSource<RequestVoteResult>(TaskCreationOptions.RunContinuationsAsynchronously);
//        var command = new ProcessVoteRequest(args, tcs);
//        if (!_commandChannel.Writer.TryWrite(command)) {
//            tcs.TrySetException(new InvalidOperationException("RaftEngine is shutting down."));
//        }
//        return tcs.Task;
//    }

//    public Task<AppendEntriesResult> HandleAppendEntriesAsync(AppendEntriesArgs args) {
//        var tcs = new TaskCompletionSource<AppendEntriesResult>(TaskCreationOptions.RunContinuationsAsynchronously);
//        var command = new ProcessAppendEntries(args, tcs);
//        if (!_commandChannel.Writer.TryWrite(command)) {
//            tcs.TrySetException(new InvalidOperationException("RaftEngine is shutting down."));
//        }
//        return tcs.Task;
//    }

//    //public Task<ErrorOr<byte[]>> ProposeAsync(byte[] command) {
//    //    var tcs = new TaskCompletionSource<ErrorOr<byte[]>>(TaskCreationOptions.RunContinuationsAsynchronously);
//    //    var proposal = new ProcessClientProposal(command, tcs);
//    //    if (!_commandChannel.Writer.TryWrite(proposal)) {
//    //        tcs.TrySetResult(Error.ServiceUnavailable("Raft.EngineShutdown", "Raft motoru kapanıyor."));
//    //    }
//    //    return tcs.Task;
//    //}

//    public Task<ErrorOr<CommandPayload>> ProposeAsync(CommandPayload command) {
//        var tcs = new TaskCompletionSource<ErrorOr<CommandPayload>>(TaskCreationOptions.RunContinuationsAsynchronously);
//        var proposal = new ProcessClientProposal(command, tcs);
//        if (!_commandChannel.Writer.TryWrite(proposal)) {
//            tcs.TrySetResult(Error.ServiceUnavailable("Raft.EngineShutdown", "Raft motoru kapanıyor."));
//        }
//        return tcs.Task;
//    }

//    public void RegisterProposal(LogIndex index, TaskCompletionSource<ErrorOr<CommandPayload>> tcs) {
//        _pendingProposals[index] = tcs;
//    }

//    public void SetCommitIndex(LogIndex index) {
//        if (index > CommitIndex) {
//            CommitIndex = index;
//            _applySignal.Set();
//        }
//    }

//    private async Task ApplyCommittedEntriesAsync(CancellationToken ct) {
//        while (!ct.IsCancellationRequested) {
//            await _applySignal.WaitAsync(ct);
//            while (_lastApplied < CommitIndex) {
//                var indexToApply = _lastApplied + 1;
//                var logEntry = _raftLog.Get(indexToApply);

//                if (logEntry != null) {
//                    var result = await _stateMachine.ApplyAsync(logEntry.Command);
//                    if (_pendingProposals.TryRemove(indexToApply, out var tcs)) {
//                        tcs.TrySetResult(result);
//                    }
//                }
//                _lastApplied = indexToApply;
//            }
//        }
//    }

//    public async ValueTask DisposeAsync() {
//        if (!_disposeState.TryBeginDispose()) return;
//        _logger.LogInformation("Raft Engine durduruluyor...");
//        _engineCts.Cancel();
//        if (_executionTask is not null) {
//            await _executionTask.ConfigureAwait(false);
//        }
//        await _currentRole.LeaveAsync();
//        _commandChannel.Writer.TryComplete();
//        _engineCts.Dispose();
//        _disposeState.SetDisposed();
//    }
//}


// --- START OF FILE RaftEngine.cs (EKSİKSIZ VE SNAPSHOT DESTEKLİ VERSİYON) ---

using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Wiaoj.Concurrency;
using Wiaoj.Consensus.Raft.Abstractions;
using Wiaoj.Consensus.Raft.Roles;
using Wiaoj.Primitives;
using Wiaoj.Results;
using Wiaoj.Threading.Channels;

namespace Wiaoj.Consensus.Raft;

/// <summary>
/// Raft düğümünün ana orkestrasyon motoru. Durum geçişlerini, RPC çağrılarını,
/// zaman aşımlarını ve log sıkıştırma (snapshotting) işlemlerini yönetir.
/// </summary>
public sealed class RaftEngine : IRaftEngine, IAsyncDisposable {
    private readonly ILogger<RaftEngine> _logger;
    private readonly IStateManager _stateManager;
    private readonly IRaftLog _raftLog;
    private readonly IStateMachine _stateMachine;
    private readonly RaftNodeOptions _options;
    private readonly IReadOnlyDictionary<NodeState, IRaftRole> _roles;
    private readonly Channel<IRaftCommand> _commandChannel;
    private readonly CancellationTokenSource _engineCts = new();
    private readonly AsyncLock _transitionLock = new();
    private readonly DisposeState _disposeState = new();

    private readonly ConcurrentDictionary<LogIndex, TaskCompletionSource<Result<CommandPayload>>> _pendingProposals = new();

    private Task? _executionTask;
    private IRaftRole _currentRole;
    private LogIndex _lastApplied;
    private readonly AsyncAutoResetEvent _applySignal = new(false);
    private LogIndex _lastSnapshotIndex;

    public LogIndex CommitIndex { get; private set; }

    public RaftEngine(
      ILogger<RaftEngine> logger,
      IStateManager stateManager,
      IRaftLog raftLog,
      IStateMachine stateMachine,
      IRaftCluster cluster,
      RaftNodeOptions options,
      ILoggerFactory loggerFactory) {
        _logger = logger;
        _stateManager = stateManager;
        _raftLog = raftLog;
        _stateMachine = stateMachine;
        _options = options;
        _commandChannel = Channel<IRaftCommand>.CreateUnbounded();

        // Başlangıç durumunu kalıcılık katmanından yükle
        _lastSnapshotIndex = _raftLog.LastSnapshotIndex;
        _lastApplied = _raftLog.LastSnapshotIndex;
        CommitIndex = _raftLog.LastSnapshotIndex;

        _roles = new Dictionary<NodeState, IRaftRole> {
            { NodeState.Follower, new FollowerRole(this, stateManager, raftLog, options, loggerFactory.CreateLogger<FollowerRole>()) },
            { NodeState.Candidate, new CandidateRole(this, stateManager, raftLog, cluster, options, loggerFactory.CreateLogger<CandidateRole>()) },
            { NodeState.Leader, new LeaderRole(this, stateManager, raftLog, cluster, options, loggerFactory.CreateLogger<LeaderRole>()) }
        };
        _currentRole = _roles[NodeState.Follower];
    }

    public Task StartAsync(CancellationToken cancellationToken) {
        _disposeState.ThrowIfDisposingOrDisposed(nameof(RaftEngine));
        _logger.LogInformation("Raft Engine başlatılıyor...");

        CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_engineCts.Token, cancellationToken);

        _executionTask = Task.Run(() => MainLoopAsync(linkedCts.Token), linkedCts.Token);
        _ = Task.Run(() => ApplyCommittedEntriesAsync(linkedCts.Token), linkedCts.Token);

        return Task.CompletedTask;
    }

    private async Task MainLoopAsync(CancellationToken ct) {
        try {
            await _currentRole.Enter(ct);

            await foreach (var command in _commandChannel.Reader.ReadAllAsync(ct)) {
                switch (command) {
                    case ProcessTermResponse resp:
                    if (await _stateManager.StepDownIfGreaterTermAsync(resp.ResponseTerm)) {
                        _logger.LogInformation("{PeerId} daha yüksek bir dönemde ({PeerTerm}). Takipçi olunuyor.", resp.SenderId, resp.ResponseTerm);
                        await TransitionToAsync(NodeState.Follower);
                    }
                    break;
                    case ProcessVoteRequest req:
                    var voteResult = await _currentRole.HandleRequestVoteAsync(req.Args);
                    req.Tcs.TrySetResult(voteResult);
                    break;
                    case ProcessAppendEntries req:
                    var appendResult = await _currentRole.HandleAppendEntriesAsync(req.Args);
                    req.Tcs.TrySetResult(appendResult);
                    break;
                    case ProcessInstallSnapshot req:
                    var snapshotResult = await _currentRole.HandleInstallSnapshotAsync(req.Args);
                    req.Tcs.TrySetResult(snapshotResult);
                    break;
                    case ProcessClientProposal req:
                    var proposalResult = await _currentRole.ProposeAsync(req.Command);
                    req.Tcs.TrySetResult(proposalResult);
                    break;
                    case InternalTimeoutElapsed:
                    if (_currentRole.State == NodeState.Follower) {
                        _logger.LogInformation("Liderden heartbeat alınamadı, seçim başlatılıyor...");
                        await TransitionToAsync(NodeState.Candidate);
                    }
                    break;
                }
            }
        }
        catch (OperationCanceledException) {
            _logger.LogWarning("Raft Engine ana döngüsü iptal edildi.");
        }
        catch (Exception ex) {
            _logger.LogCritical(ex, "Raft Engine ana döngüsünde ölümcül hata!");
        }
    }

    private async Task ApplyCommittedEntriesAsync(CancellationToken ct) {
        while (!ct.IsCancellationRequested) {
            try {
                await _applySignal.WaitAsync(ct);
                while (_lastApplied < CommitIndex) {
                    var indexToApply = _lastApplied + 1;
                    var logEntry = _raftLog.Get(indexToApply);

                    if (logEntry != null) {
                        var result = await _stateMachine.ApplyAsync(logEntry.Command);
                        if (_pendingProposals.TryRemove(indexToApply, out var tcs)) {
                            tcs.TrySetResult(result);
                        }
                    }
                    _lastApplied = indexToApply;

                    // Snapshot eşiğini kontrol et
                    if (_options.SnapshotThreshold > 0 && (_lastApplied - _lastSnapshotIndex) >= _options.SnapshotThreshold) {
                        _ = TakeSnapshotAsync(); // Arka planda çalıştır, ana döngüyü bloklama
                    }
                }
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) {
                _logger.LogError(ex, "ApplyCommittedEntries döngüsünde hata oluştu.");
            }
        }
    }

    private async Task TakeSnapshotAsync() {
        // Geçiş kilidi, rol değişikliği gibi işlemlerle çakışmayı önler.
        using (await _transitionLock.LockAsync(_engineCts.Token)) {
            var lastAppliedToSnapshot = _lastApplied;
            if (lastAppliedToSnapshot <= _lastSnapshotIndex) return;

            _logger.LogInformation("Snapshot eşiği aşıldı. Index {Index}'e kadar snapshot alınıyor...", lastAppliedToSnapshot);
            try {
                var snapshotData = await _stateMachine.CreateSnapshotAsync();
                var lastEntry = _raftLog.Get(lastAppliedToSnapshot);
                if (lastEntry is null) {
                    _logger.LogError("Snapshot alınacak index {Index} için log girdisi bulunamadı!", lastAppliedToSnapshot);
                    return;
                }
                var lastTerm = new Term(lastEntry.Term);

                await _raftLog.CompactLogAsync(snapshotData, lastAppliedToSnapshot, lastTerm);
                _lastSnapshotIndex = lastAppliedToSnapshot;
                _logger.LogInformation("Snapshot başarıyla alındı ve log sıkıştırıldı.");
            }
            catch (Exception ex) {
                _logger.LogCritical(ex, "Snapshot alınırken ölümcül hata!");
            }
        }
    }

    public void HandleInternalTimeout() => _commandChannel.Writer.TryWrite(new InternalTimeoutElapsed("ElectionTimer"));
    public void ProcessHigherTerm(NodeId senderId, Term responseTerm) => _commandChannel.Writer.TryWrite(new ProcessTermResponse(senderId, responseTerm));

    public async Task TransitionToAsync(NodeState newState) {
        using (await _transitionLock.LockAsync(_engineCts.Token)) {
            if (_currentRole.State == newState) return;

            _logger.LogInformation("Durum değişiyor: {OldState} -> {NewState}", _currentRole.State, newState);

            await _currentRole.LeaveAsync();
            _currentRole = _roles[newState];
            await _currentRole.Enter(_engineCts.Token);
        }
    }

    public Task<RequestVoteResult> HandleRequestVoteAsync(RequestVoteArgs args) {
        var tcs = new TaskCompletionSource<RequestVoteResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var command = new ProcessVoteRequest(args, tcs);
        if (!_commandChannel.Writer.TryWrite(command)) {
            tcs.TrySetException(new InvalidOperationException("RaftEngine is shutting down."));
        }
        return tcs.Task;
    }

    public Task<AppendEntriesResult> HandleAppendEntriesAsync(AppendEntriesArgs args) {
        var tcs = new TaskCompletionSource<AppendEntriesResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var command = new ProcessAppendEntries(args, tcs);
        if (!_commandChannel.Writer.TryWrite(command)) {
            tcs.TrySetException(new InvalidOperationException("RaftEngine is shutting down."));
        }
        return tcs.Task;
    }

    public Task<InstallSnapshotResult> HandleInstallSnapshotAsync(InstallSnapshotArgs args) {
        var tcs = new TaskCompletionSource<InstallSnapshotResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var command = new ProcessInstallSnapshot(args, tcs);
        if (!_commandChannel.Writer.TryWrite(command)) {
            tcs.TrySetException(new InvalidOperationException("RaftEngine is shutting down."));
        }
        return tcs.Task;
    }

    public Task<Result<CommandPayload>> ProposeAsync(CommandPayload command) {
        var tcs = new TaskCompletionSource<Result<CommandPayload>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var proposal = new ProcessClientProposal(command, tcs);
        if (!_commandChannel.Writer.TryWrite(proposal)) {
            tcs.TrySetResult(Error.Failure("Raft.EngineShutdown", "Raft motoru kapanıyor."));
        }
        return tcs.Task;
    }

    public void RegisterProposal(LogIndex index, TaskCompletionSource<Result<CommandPayload>> tcs) => _pendingProposals[index] = tcs;

    public void SetCommitIndex(LogIndex index) {
        // Commit index asla geriye gitmez.
        if (index > CommitIndex) {
            CommitIndex = index;
            _applySignal.Set();
        }
    }

    // Bu metotlar roller tarafından, durumlarını senkronize etmek için kullanılır.
    public void SetLastApplied(LogIndex index) => _lastApplied = index;
    public async Task RestoreStateMachineFromSnapshotAsync(byte[] snapshotData) => await _stateMachine.RestoreFromSnapshotAsync(snapshotData);

    public object GetStatus() {
        return new {
            NodeId = _options.NodeId,
            Role = _currentRole.State.ToString(),
            Term = _stateManager.GetCurrentTerm().Value,
            CommitIndex = CommitIndex.Value,
            LastApplied = _lastApplied.Value,
            LastSnapshotIndex = _lastSnapshotIndex.Value
        };
    }

    public async ValueTask DisposeAsync() {
        if (!_disposeState.TryBeginDispose()) return;
        _logger.LogInformation("Raft Engine durduruluyor...");
        _engineCts.Cancel();
        if (_executionTask is not null) {
            try {
                await _executionTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException) { /* Beklenen bir durum */ }
        }
        await _currentRole.LeaveAsync();
        _commandChannel.Writer.TryComplete();
        _engineCts.Dispose();
        _disposeState.SetDisposed();
    }
}

// --- END OF FILE RaftEngine.cs ---