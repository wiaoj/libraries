// --- START OF FILE LeaderRole.cs (EKSİKSİZ VE SNAPSHOT DESTEKLİ VERSİYON) ---

using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Wiaoj.Concurrency;
using Wiaoj.Consensus.Raft.Abstractions;
using Wiaoj.Results;

namespace Wiaoj.Consensus.Raft.Roles;

public record Proposal(LogEntry Entry, TaskCompletionSource<Result<CommandPayload>> Tcs);

public sealed class LeaderRole : IRaftRole {
    private readonly RaftEngine _engine;
    private readonly IStateManager _stateManager;
    private readonly IRaftLog _raftLog;
    private readonly IRaftCluster _cluster;
    private readonly RaftNodeOptions _options;
    private readonly ILogger<LeaderRole> _logger;
    private readonly Channel<Proposal> _proposalChannel;
    private CancellationTokenSource? _roleCts;
    private readonly ConcurrentDictionary<NodeId, PeerState> _peerStates = new();

    public NodeState State => NodeState.Leader;

    public LeaderRole(RaftEngine engine, IStateManager stateManager, IRaftLog raftLog, IRaftCluster cluster, RaftNodeOptions options, ILogger<LeaderRole> logger) {
        _engine = engine;
        _stateManager = stateManager;
        _raftLog = raftLog;
        _cluster = cluster;
        _options = options;
        _logger = logger;
        _proposalChannel = Channel.CreateUnbounded<Proposal>();
    }

    public Task Enter(CancellationToken cancellationToken) {
        _roleCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _logger.LogInformation("***** LİDER OLUNDU! Dönem: {Term} *****", _stateManager.GetCurrentTerm());

        LogIndex lastLogIndex = _raftLog.GetLastEntryInfo().Index;
        foreach (IRaftPeer peer in _cluster.Peers) {
            _peerStates[peer.Id] = new PeerState(lastLogIndex + 1, _logger);
        }

        _ = Task.Run(() => ProcessProposalsAsync(_roleCts.Token), _roleCts.Token);
        foreach (IRaftPeer peer in _cluster.Peers) {
            _ = Task.Run(() => ReplicationLoopAsync(peer, _roleCts.Token), _roleCts.Token);
        }

        foreach (PeerState state in _peerStates.Values) state.Notify();

        return Task.CompletedTask;
    }

    public Task LeaveAsync() {
        _roleCts?.Cancel();
        _roleCts?.Dispose();
        _proposalChannel.Writer.TryComplete();

        while (_proposalChannel.Reader.TryRead(out Proposal? proposal)) {
            proposal.Tcs.TrySetResult(Error.Unexpected("Raft.LeaderStepDown", "Liderlikten çekildi."));
        }

        return Task.CompletedTask;
    }

    public Task<Result<CommandPayload>> ProposeAsync(CommandPayload command) {
        var tcs = new TaskCompletionSource<Result<CommandPayload>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var entry = new LogEntry { Term = _stateManager.GetCurrentTerm().Value, Command = command.Value.ToArray() };
        if (!_proposalChannel.Writer.TryWrite(new Proposal(entry, tcs))) {
            tcs.TrySetResult(Error.ServiceUnavailable("Raft.QueueFull", "Öneri kuyruğu dolu."));
        }
        return tcs.Task;
    }

    private async Task ProcessProposalsAsync(CancellationToken ct) {
        var proposals = new List<Proposal>();
        while (!ct.IsCancellationRequested) {
            try {
                // İlk proposal'ı bekle
                proposals.Add(await _proposalChannel.Reader.ReadAsync(ct));

                // Kanalda birikmiş diğer tüm proposal'ları da al (batching).
                while (_proposalChannel.Reader.TryRead(out var proposal)) {
                    proposals.Add(proposal);
                }

                if (proposals.Count > 0) {
                    var logEntries = proposals.Select(p => p.Entry).ToList();

                    // YENİ METODU KULLAN: Birden çok girdiyi tek seferde yaz.
                    var lastIndex = await _raftLog.AppendEntriesAsync(logEntries);
                    var firstIndex = lastIndex - (logEntries.Count - 1);

                    // TCS'leri doğru index'lerle kaydet.
                    for (int i = 0; i < proposals.Count; i++) {
                        _engine.RegisterProposal(firstIndex + i, proposals[i].Tcs);
                    }

                    proposals.Clear();
                    foreach (PeerState state in _peerStates.Values) state.Notify();
                }
                //try {
                //    Proposal proposal = await this._proposalChannel.Reader.ReadAsync(ct);
                //    LogIndex newIndex = await this._raftLog.AppendAsync(proposal.Entry);
                //    this._engine.RegisterProposal(newIndex, proposal.Tcs);
                //    // Yeni bir girdi eklendiğinde tüm peer'leri uyar.
                //    foreach (PeerState state in this._peerStates.Values) state.Notify();
            }
            catch (ChannelClosedException) { break; }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) {
                this._logger.LogError(ex, "Proposal işleme döngüsünde hata oluştu.");
            }
        }
    }
      
    private async Task ReplicationLoopAsync(IRaftPeer peer, CancellationToken ct) {
        PeerState peerState = _peerStates[peer.Id];

        while (!ct.IsCancellationRequested) {
            try {
                await peerState.WaitForWorkAsync(_options.HeartbeatInterval, ct);

                // GÜNCELLENDİ: Snapshot gönderme kontrolü eklendi.
                LogIndex prevLogIndex = peerState.NextIndex - 1;
                if (prevLogIndex < _raftLog.LastSnapshotIndex) {
                    await SendSnapshotToPeerAsync(peer, ct);
                    continue;
                }

                // AppendEntries gönderme mantığı
                Term prevLogTerm;
                if (prevLogIndex > LogIndex.Zero) {
                    LogEntry? prevEntry = _raftLog.Get(prevLogIndex);
                    if (prevEntry is null) {
                        // Bu durum, log'un sıkıştırıldığı ama peerState'in henüz güncellenmediği bir
                        // yarış durumunda olabilir. Snapshot göndermeyi zorla.
                        await SendSnapshotToPeerAsync(peer, ct);
                        continue;
                    }
                    prevLogTerm = new Term(prevEntry.Term);
                }
                else {
                    prevLogTerm = Term.Zero;
                }

                var entriesToSend = new List<LogEntry>();
                if (_raftLog.LastIndex >= peerState.NextIndex) {
                    for (var i = peerState.NextIndex.Value; i <= _raftLog.LastIndex.Value; i++) {
                        entriesToSend.Add(_raftLog.Get(new LogIndex(i))!);
                    }
                }

                var args = new AppendEntriesArgs {
                    Term = _stateManager.GetCurrentTerm(),
                    LeaderId = NodeId.From(_options.NodeId),
                    PrevLogIndex = prevLogIndex,
                    PrevLogTerm = prevLogTerm,
                    LeaderCommitIndex = _engine.CommitIndex,
                    Entries = entriesToSend
                };

                AppendEntriesResult result = await peer.AppendEntriesAsync(args, ct);

                if (await _stateManager.StepDownIfGreaterTermAsync(result.Term)) {
                    await _engine.TransitionToAsync(NodeState.Follower);
                    return;
                }

                if (result.Success) {
                    if (entriesToSend.Count > 0) {
                        peerState.MatchIndex = prevLogIndex + entriesToSend.Count;
                        peerState.NextIndex = peerState.MatchIndex + 1;
                    }
                    AdvanceCommitIndex();
                }
                else {
                    peerState.NextIndex = LogIndex.Max(new LogIndex(1), peerState.NextIndex - 1);
                    peerState.Notify();
                }
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) {
                _logger.LogWarning(ex, "{PeerId} için replikasyon döngüsünde hata.", peer.Id);
                await Task.Delay(1000, ct);
            }
        }
    }

    private async Task SendSnapshotToPeerAsync(IRaftPeer peer, CancellationToken ct) {
        try {
            _logger.LogInformation("{PeerId} çok geride. Snapshot gönderiliyor...", peer.Id);

            var snapshotData = await _raftLog.GetSnapshotDataAsync();
            if (snapshotData is null) {
                _logger.LogWarning("Gönderilecek snapshot bulunamadı.");
                return;
            }

            var args = new InstallSnapshotArgs {
                Term = _stateManager.GetCurrentTerm(),
                LeaderId = NodeId.From(_options.NodeId),
                LastIncludedIndex = _raftLog.LastSnapshotIndex,
                LastIncludedTerm = _raftLog.LastSnapshotTerm,
                Data = snapshotData
            };

            var result = await peer.InstallSnapshotAsync(args, ct);

            if (ct.IsCancellationRequested) return;

            if (await _stateManager.StepDownIfGreaterTermAsync(result.Term)) {
                await _engine.TransitionToAsync(NodeState.Follower);
                return;
            }

            var peerState = _peerStates[peer.Id];
            peerState.MatchIndex = args.LastIncludedIndex;
            peerState.NextIndex = args.LastIncludedIndex + 1;

            // Snapshot sonrası commit index'i yeniden değerlendir.
            AdvanceCommitIndex();
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) {
            _logger.LogError(ex, "{PeerId} adresine snapshot gönderilirken hata oluştu.", peer.Id);
        }
    }

    private void AdvanceCommitIndex() {
        var matchIndexes = this._peerStates.Values.Select(s => s.MatchIndex).Append(this._raftLog.LastIndex).ToList();
        matchIndexes.Sort((a, b) => b.CompareTo(a));

        // Çoğunluğun sağlandığı indeksi bul (kendimiz dahil n/2).
        var quorumIndex = this._cluster.TotalNodes / 2;
        LogIndex potentialCommitIndex = matchIndexes[quorumIndex];

        // Raft Kuralı: Liderler, önceki dönemlerden gelen girdileri doğrudan commit edemez.
        // Sadece kendi dönemindeki bir girdi commit edildiğinde, dolaylı olarak öncekiler de commit edilir.
        LogEntry? entryToCommit = this._raftLog.Get(potentialCommitIndex);
        if (potentialCommitIndex > this._engine.CommitIndex && entryToCommit?.Term == this._stateManager.GetCurrentTerm().Value) {
            this._logger.LogInformation("Commit index {OldIndex}'den {NewIndex}'e yükseltildi.", this._engine.CommitIndex, potentialCommitIndex);
            this._engine.SetCommitIndex(potentialCommitIndex);
        }
    }

    public async Task<AppendEntriesResult> HandleAppendEntriesAsync(AppendEntriesArgs args) {
        if (await _stateManager.StepDownIfGreaterTermAsync(args.Term)) {
            await _engine.TransitionToAsync(NodeState.Follower);
        }
        return new AppendEntriesResult { Term = _stateManager.GetCurrentTerm(), Success = false };
    }

    public async Task<RequestVoteResult> HandleRequestVoteAsync(RequestVoteArgs args) {
        if (await _stateManager.StepDownIfGreaterTermAsync(args.Term)) {
            await _engine.TransitionToAsync(NodeState.Follower);
        }
        return new RequestVoteResult { Term = _stateManager.GetCurrentTerm(), VoteGranted = false };
    }

    public async Task<InstallSnapshotResult> HandleInstallSnapshotAsync(InstallSnapshotArgs args) {
        if (await _stateManager.StepDownIfGreaterTermAsync(args.Term)) {
            await _engine.TransitionToAsync(NodeState.Follower);
        }
        return new InstallSnapshotResult { Term = _stateManager.GetCurrentTerm() };
    }
}

public sealed class PeerState {
    private readonly ILogger _logger;
    private readonly AsyncAutoResetEvent _workSignal = new(false);
    public LogIndex NextIndex { get; set; }
    public LogIndex MatchIndex { get; set; }

    public PeerState(LogIndex nextIndex, ILogger logger) {
        this.NextIndex = nextIndex;
        this.MatchIndex = LogIndex.Zero;
        this._logger = logger;
    }

    public void Notify() {
        this._workSignal.Set();
    }

    public async Task WaitForWorkAsync(TimeSpan timeout, CancellationToken ct) {
        try {
            using var timeoutCts = new CancellationTokenSource(timeout);
            using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);
            await this._workSignal.WaitAsync(combinedCts.Token);
        }
        catch (OperationCanceledException) {
            if (ct.IsCancellationRequested) throw; // Gerçek iptal
            // OperationTimeout'a ulaşıldı, bu bir hata değil, heartbeat zamanı demektir.
        }
    }
}