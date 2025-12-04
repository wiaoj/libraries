//using FASTER.core;
//using MemoryPack;
//using Microsoft.Extensions.Logging;
//using Microsoft.Extensions.Options;
//using Wiaoj.Concurrency;
//using Wiaoj.Consensus.Raft.Abstractions;

//namespace Wiaoj.Consensus.Raft.Persistence.Faster;

//public sealed class FasterState : IRaftLog, IStateManager, IAsyncDisposable {
//    private readonly FasterLog _log;
//    private readonly IDevice _logDevice;
//    private readonly FasterKV<string, string> _metadataStore;
//    private readonly IDevice _metadataDevice; 

//    private readonly IDevice _metadataObjectLogDevice;
//    private readonly ClientSession<string, string, string, string, Empty, IFunctions<string, string, string, string, Empty>> _metadataSession;

//    private readonly List<LogEntry> _inMemoryLogCache = [];

//    private readonly ILogger<FasterState> _logger;
//    private readonly DisposeState _disposeState = new();

//    private Term _currentTerm;
//    private NodeId? _votedFor;

//    public LogIndex LastIndex => new(this._inMemoryLogCache.Count);

//    public FasterState(IOptions<RaftNodeOptions> options, ILogger<FasterState> logger) {
//        _logger = logger;
//        string persistencePath = options.Value.PersistencePath;
//        Directory.CreateDirectory(persistencePath);

//        string logPath = Path.Combine(persistencePath, "entries.log");
//        _logDevice = Devices.CreateLogDevice(logPath);
//        _log = new FasterLog(new FasterLogSettings {
//            LogDevice = _logDevice,
//            AutoCommit = true
//        });


//        string metadataPath = Path.Combine(persistencePath, "metadata");

//        // 1. Ana log cihazını tanımla (Bu zaten vardı)
//        _metadataDevice = Devices.CreateLogDevice(Path.Combine(metadataPath, "hlog.log"));

//        // 2. EKSİK OLAN SATIRI EKLE: Object log cihazını tanımla
//        // Genellikle ".obj.log" uzantısı kullanılır.
//        _metadataObjectLogDevice = Devices.CreateLogDevice(Path.Combine(metadataPath, "hlog.obj.log"));

//        _metadataStore = new FasterKV<string, string>(
//            1L << 20,
//            // 3. LogSettings'e ObjectLogDevice'ı ekle
//            new LogSettings {
//                LogDevice = _metadataDevice,
//                ObjectLogDevice = _metadataObjectLogDevice // Bu satır eklendi
//            },
//            new CheckpointSettings {
//                CheckpointDir = metadataPath
//            }
//        );

//        _metadataSession = _metadataStore.NewSession(new SimpleFunctions<string, string>());
//    }

//    public async Task InitializeAsync(CancellationToken cancellationToken = default) {
//        _logger.LogInformation("FASTER kalıcılık katmanı başlatılıyor...");
//        _disposeState.ThrowIfDisposingOrDisposed(nameof(FasterState));

//        try {
//            await _metadataStore.RecoverAsync(cancellationToken: cancellationToken);
//        }
//        catch (FasterException) {
//            _logger.LogInformation("Mevcut metadata checkpoint'i bulunamadı, sıfırdan başlanıyor.");
//        }

//        var termReadResult = await _metadataSession.ReadAsync("CurrentTerm").ConfigureAwait(false);
//        var (termStatus, termOutput) = termReadResult.Complete();
//        _currentTerm = termStatus.Found ? new Term(long.Parse(termOutput!)) : Term.Zero;

//        var votedForReadResult = await _metadataSession.ReadAsync("VotedFor").ConfigureAwait(false);
//        var (votedForStatus, votedForOutput) = votedForReadResult.Complete();
//        _votedFor = votedForStatus.Found ? new NodeId(votedForOutput!) : null;

//        if (File.Exists(_logDevice.FileName) && _log.TailAddress > _log.BeginAddress) {
//            using var iterator = _log.Scan(_log.BeginAddress, _log.TailAddress);
//            await foreach (var (data, length, _, _) in iterator.GetAsyncEnumerable(cancellationToken).ConfigureAwait(false)) {
//                var entry = MemoryPackSerializer.Deserialize<LogEntry>(data.AsSpan(0, length));
//                if (entry != null) _inMemoryLogCache.Add(entry);
//            }
//        }

//        _logger.LogInformation("FASTER kalıcılık katmanı başlatıldı. Term={Term}, VotedFor={VotedFor}, Bellekteki Log Girdisi={LogEntryCount}", _currentTerm, _votedFor, _inMemoryLogCache.Count);
//    }

//    #region IStateManager Implementation

//    public Term GetCurrentTerm() => _currentTerm;

//    public async ValueTask SetCurrentTermAsync(Term term) {
//        _disposeState.ThrowIfDisposingOrDisposed(nameof(FasterState));
//        _currentTerm = term;
//        await _metadataSession.UpsertAsync("CurrentTerm", term.Value.ToString());
//        // --- HATA 1 DÜZELTMESİ: CheckpointType, metoda parametre olarak verildi. ---
//        await _metadataStore.TakeFullCheckpointAsync(CheckpointType.FoldOver);
//    }

//    public NodeId? GetVotedFor() => _votedFor;

//    public async ValueTask SetVotedForAsync(NodeId? candidateId) {
//        _disposeState.ThrowIfDisposingOrDisposed(nameof(FasterState));
//        _votedFor = candidateId;
//        if (candidateId.HasValue) {
//            await _metadataSession.UpsertAsync("VotedFor", candidateId.Value.Value);
//        }
//        else {
//            await _metadataSession.DeleteAsync("VotedFor");
//        }
//        await _metadataStore.TakeFullCheckpointAsync(CheckpointType.FoldOver);
//    }

//    public async ValueTask<bool> StepDownIfGreaterTermAsync(Term rpcTerm) {
//        _disposeState.ThrowIfDisposingOrDisposed(nameof(FasterState));
//        if (rpcTerm > _currentTerm) {
//            _currentTerm = rpcTerm;
//            _votedFor = null;
//            await _metadataSession.UpsertAsync("CurrentTerm", rpcTerm.Value.ToString());
//            await _metadataSession.DeleteAsync("VotedFor");
//            await _metadataStore.TakeFullCheckpointAsync(CheckpointType.FoldOver);
//            return true;
//        }
//        return false;
//    }

//    #endregion

//    #region IRaftLog Implementation

//    public async ValueTask<LogIndex> AppendAsync(LogEntry entry) {
//        _disposeState.ThrowIfDisposingOrDisposed(nameof(FasterState));
//        var binaryData = MemoryPackSerializer.Serialize(entry);
//        await _log.EnqueueAsync(binaryData);
//        _inMemoryLogCache.Add(entry);
//        return new LogIndex(_inMemoryLogCache.Count);
//    }

//    public async ValueTask<LogIndex> AppendEntriesAsync(IReadOnlyList<LogEntry> entries) {
//        if (entries.Count == 0) return LastIndex;
//        foreach (var entry in entries) {
//            var binaryData = MemoryPackSerializer.Serialize(entry);
//            _log.Enqueue(binaryData);
//            _inMemoryLogCache.Add(entry);
//        }
//        // --- HATA 2 DÜZELTMESİ: FlushAndCommitAsync yerine CommitAsync kullanıldı. ---
//        await _log.CommitAsync();
//        return LastIndex;
//    }

//    public async ValueTask TruncateAsync(LogIndex fromIndex) {
//        _logger.LogWarning("TruncateAsync çağrıldı (index: {FromIndex}), şimdilik sadece bellek içi işlem yapılıyor.", fromIndex);
//        long indexValue = fromIndex.Value;
//        if (indexValue <= 0 || indexValue > _inMemoryLogCache.Count) return;

//        int index = (int)indexValue - 1;
//        int countToRemove = _inMemoryLogCache.Count - index;
//        _inMemoryLogCache.RemoveRange(index, countToRemove);

//        await ValueTask.CompletedTask;
//    }

//    public LogEntry? Get(LogIndex index) {
//        long indexValue = index.Value;
//        if (indexValue <= 0 || indexValue > _inMemoryLogCache.Count) return null;
//        return _inMemoryLogCache[(int)indexValue - 1];
//    }

//    public (Term Term, LogIndex Index) GetLastEntryInfo() {
//        if (_inMemoryLogCache.Count == 0) return (Term.Zero, LogIndex.Zero);
//        var lastIndex = new LogIndex(_inMemoryLogCache.Count);
//        var lastTerm = new Term(_inMemoryLogCache[^1].Term);
//        return (lastTerm, lastIndex);
//    }

//    #endregion

//    public async ValueTask DisposeAsync() {
//        if (!_disposeState.TryBeginDispose()) return;
//        _logger.LogInformation("FASTER kalıcılık katmanı kapatılıyor...");

//        _metadataSession.Dispose();

//        await _metadataStore.TakeFullCheckpointAsync(CheckpointType.FoldOver);
//        _metadataStore.Dispose();

//        // Tüm cihazları dispose et
//        _log.Dispose();
//        _metadataObjectLogDevice.Dispose(); // Yeni eklenen cihaz
//        _metadataDevice.Dispose();

//        _logDevice.Dispose();

//        _logger.LogInformation("FASTER kalıcılık katmanı başarıyla kapatıldı.");
//        _disposeState.SetDisposed();
//    }
//}


using FASTER.core;
using MemoryPack;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wiaoj.Concurrency;
using Wiaoj.Consensus.Raft.Abstractions;

namespace Wiaoj.Consensus.Raft.Persistence.Faster;

/// <summary>
/// Raft'ın log ve durum yönetimi için tüm kalıcılık mantığını Microsoft FASTER kullanarak uygular.
/// Bu sınıf thread-safe'dir ve Raft'ın dayanıklılık garantilerini karşılamak üzere tasarlanmıştır.
/// </summary>
public sealed class FasterState : IRaftLog, IStateManager, IAsyncDisposable {
    private readonly ILogger<FasterState> _logger;
    private readonly FasterLog _log;
    private readonly IDevice _logDevice;
    private readonly FasterKV<string, string> _metadataStore;
    private readonly IDevice _metadataDevice;
    private readonly IDevice _metadataObjectLogDevice;
    private readonly ClientSession<string, string, string, string, Empty, IFunctions<string, string, string, string, Empty>> _metadataSession;

    private readonly string _snapshotFilePath;
    private readonly List<LogEntry> _inMemoryLogCache = [];
    // KRİTİK: Log index'ini FASTER'daki fiziksel adrese eşlemek için bu liste zorunludur. Truncate işlemi için kullanılır.
    private readonly List<long> _logEntryAddresses = [];

    private Term _currentTerm;
    private NodeId? _votedFor;

    public LogIndex LastIndex => new(this.LastSnapshotIndex.Value + this._inMemoryLogCache.Count);
    public LogIndex LastSnapshotIndex { get; private set; }
    public Term LastSnapshotTerm { get; private set; }

    private readonly DisposeState _disposeState = new();

    public FasterState(IOptions<RaftNodeOptions> options, ILogger<FasterState> logger) {
        this._logger = logger;
        string persistencePath = options.Value.PersistencePath;
        Directory.CreateDirectory(persistencePath);

        this._snapshotFilePath = Path.Combine(persistencePath, "state.snapshot");

        string logPath = Path.Combine(persistencePath, "entries.log");
        this._logDevice = Devices.CreateLogDevice(logPath);
        this._log = new FasterLog(new FasterLogSettings { LogDevice = this._logDevice });

        string metadataCheckpointPath = Path.Combine(persistencePath, "metadata-checkpoints");
        this._metadataDevice = Devices.CreateLogDevice(Path.Combine(metadataCheckpointPath, "meta.log"));
        this._metadataObjectLogDevice = Devices.CreateLogDevice(Path.Combine(metadataCheckpointPath, "meta.obj.log"));
        this._metadataStore = new FasterKV<string, string>(
            1L << 20,
            new LogSettings { LogDevice = this._metadataDevice, ObjectLogDevice = this._metadataObjectLogDevice },
            new CheckpointSettings { CheckpointDir = metadataCheckpointPath }
        );
        this._metadataSession = this._metadataStore.NewSession(new SimpleFunctions<string, string>());
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default) {
        this._logger.LogInformation("FASTER kalıcılık katmanı başlatılıyor...");
        this._disposeState.ThrowIfDisposingOrDisposed(nameof(FasterState));

        try {
            await this._metadataStore.RecoverAsync(cancellationToken: cancellationToken);
        }
        catch (FasterException) {
            this._logger.LogInformation("Metadata checkpoint'i bulunamadı, sıfırdan başlanıyor.");
        }

        FasterKV<string, string>.ReadAsyncResult<string, string, Empty> termReadResult = await this._metadataSession.ReadAsync("CurrentTerm", token: cancellationToken).ConfigureAwait(false);
        (Status termStatus, string? termOutput) = termReadResult.Complete();
        this._currentTerm = termStatus.Found ? new Term(long.Parse(termOutput!)) : Term.Zero;

        FasterKV<string, string>.ReadAsyncResult<string, string, Empty> votedForReadResult = await this._metadataSession.ReadAsync("VotedFor", token: cancellationToken).ConfigureAwait(false);
        (Status votedForStatus, string? votedForOutput) = votedForReadResult.Complete();
        this._votedFor = votedForStatus.Found ? new NodeId(votedForOutput!) : null;

        FasterKV<string, string>.ReadAsyncResult<string, string, Empty> lsiReadResult = await this._metadataSession.ReadAsync("LastSnapshotIndex", token: cancellationToken).ConfigureAwait(false);
        (Status lsiStatus, string? lsiOutput) = lsiReadResult.Complete();
        this.LastSnapshotIndex = lsiStatus.Found ? new LogIndex(long.Parse(lsiOutput!)) : LogIndex.Zero;

        FasterKV<string, string>.ReadAsyncResult<string, string, Empty> lstReadResult = await this._metadataSession.ReadAsync("LastSnapshotTerm", token: cancellationToken).ConfigureAwait(false);
        (Status lstStatus, string? lstOutput) = lstReadResult.Complete();
        this.LastSnapshotTerm = lstStatus.Found ? new Term(long.Parse(lstOutput!)) : Term.Zero;

        this._inMemoryLogCache.Clear();
        this._logEntryAddresses.Clear();

        if (File.Exists(this._logDevice.FileName) && this._log.TailAddress > this._log.BeginAddress) {
            using FasterLogScanIterator iterator = this._log.Scan(this._log.BeginAddress, this._log.TailAddress);
            await foreach ((byte[]? data, int length, long currentAddress, long _) in iterator.GetAsyncEnumerable(cancellationToken).ConfigureAwait(false)) {
                LogEntry? entry = MemoryPackSerializer.Deserialize<LogEntry>(data.AsSpan(0, length));
                if (entry != null) {
                    this._inMemoryLogCache.Add(entry);
                    this._logEntryAddresses.Add(currentAddress);
                }
            }
        }

        this._logger.LogInformation("FASTER başlatıldı. Term={Term}, VotedFor={VotedFor}, LastSnapshotIndex={LSI}, Bellekteki Log Girdisi={LogCount}",
            this._currentTerm, this._votedFor, this.LastSnapshotIndex, this._inMemoryLogCache.Count);
    }


    #region IStateManager Implementation

    public Term GetCurrentTerm() {
        return this._currentTerm;
    }

    public async ValueTask SetCurrentTermAsync(Term term) {
        this._disposeState.ThrowIfDisposingOrDisposed(nameof(FasterState));
        this._currentTerm = term;
        await this._metadataSession.UpsertAsync("CurrentTerm", term.Value.ToString());
        await this._metadataStore.TakeFullCheckpointAsync(CheckpointType.FoldOver);
    }

    public NodeId? GetVotedFor() {
        return this._votedFor;
    }

    public async ValueTask SetVotedForAsync(NodeId? candidateId) {
        this._disposeState.ThrowIfDisposingOrDisposed(nameof(FasterState));
        this._votedFor = candidateId;
        if (candidateId.HasValue) {
            await this._metadataSession.UpsertAsync("VotedFor", candidateId.Value.Value);
        }
        else {
            await this._metadataSession.DeleteAsync("VotedFor");
        }
        await this._metadataStore.TakeFullCheckpointAsync(CheckpointType.FoldOver);
    }

    public async ValueTask<bool> StepDownIfGreaterTermAsync(Term rpcTerm) {
        if (rpcTerm > this._currentTerm) {
            this._currentTerm = rpcTerm;
            this._votedFor = null;
            // İki değişikliği de yapıp sonra tek checkpoint almak daha verimli
            await this._metadataSession.UpsertAsync("CurrentTerm", rpcTerm.Value.ToString());
            await this._metadataSession.DeleteAsync("VotedFor");
            await this._metadataStore.TakeFullCheckpointAsync(CheckpointType.FoldOver);
            return true;
        }
        return false;
    }

    #endregion

    #region IRaftLog Implementation
    public ValueTask<LogIndex> AppendAsync(LogEntry entry) {
        return AppendEntriesAsync([entry]);
    }

    public async ValueTask<LogIndex> AppendEntriesAsync(IReadOnlyList<LogEntry> entries) {
        if (entries.Count == 0) return this.LastIndex;
        this._disposeState.ThrowIfDisposingOrDisposed(nameof(FasterState));

        foreach (LogEntry entry in entries) {
            var binaryData = MemoryPackSerializer.Serialize(entry);
            long physicalAddress = this._log.Enqueue(binaryData);
            this._inMemoryLogCache.Add(entry);
            this._logEntryAddresses.Add(physicalAddress);
        }

        await this._log.CommitAsync();
        return this.LastIndex;
    }

    public async ValueTask TruncateAsync(LogIndex fromIndex) {
        this._disposeState.ThrowIfDisposingOrDisposed(nameof(FasterState));
        if (fromIndex > this.LastIndex || fromIndex <= this.LastSnapshotIndex) return;

        int listIndex = (int)(fromIndex.Value - this.LastSnapshotIndex.Value - 1);
        if (listIndex < 0 || listIndex >= this._inMemoryLogCache.Count) return;

        long truncateAddress = this._logEntryAddresses[listIndex];
        this._log.TruncateUntil(truncateAddress);
        await this._log.CommitAsync();

        int countToRemove = this._inMemoryLogCache.Count - listIndex;
        this._inMemoryLogCache.RemoveRange(listIndex, countToRemove);
        this._logEntryAddresses.RemoveRange(listIndex, countToRemove);

        this._logger.LogWarning("Log, index {Index}'den itibaren kalıcı olarak kesildi.", fromIndex);
    }

    public LogEntry? Get(LogIndex index) {
        if (index <= this.LastSnapshotIndex || index > this.LastIndex) return null;
        int listIndex = (int)(index.Value - this.LastSnapshotIndex.Value - 1);
        if (listIndex < 0 || listIndex >= this._inMemoryLogCache.Count) return null;
        return this._inMemoryLogCache[listIndex];
    }

    public (Term Term, LogIndex Index) GetLastEntryInfo() {
        if (this._inMemoryLogCache.Count == 0) return (this.LastSnapshotTerm, this.LastSnapshotIndex);
        LogIndex lastIndex = this.LastIndex;
        var lastTerm = new Term(this._inMemoryLogCache[^1].Term);
        return (lastTerm, lastIndex);
    }

    public async ValueTask CompactLogAsync(byte[] snapshotData, LogIndex lastIncludedIndex, Term lastIncludedTerm) {
        this._disposeState.ThrowIfDisposingOrDisposed(nameof(FasterState));

        if (lastIncludedIndex <= this.LastSnapshotIndex) return;

        var tempSnapshotFile = this._snapshotFilePath + ".tmp";
        await File.WriteAllBytesAsync(tempSnapshotFile, snapshotData);

        this._log.TruncateUntil(this._log.TailAddress);
        await this._log.CommitAsync();
        this._inMemoryLogCache.Clear();
        this._logEntryAddresses.Clear();

        File.Move(tempSnapshotFile, this._snapshotFilePath, true);

        this.LastSnapshotIndex = lastIncludedIndex;
        this.LastSnapshotTerm = lastIncludedTerm;
        await this._metadataSession.UpsertAsync("LastSnapshotIndex", lastIncludedIndex.Value.ToString());
        await this._metadataSession.UpsertAsync("LastSnapshotTerm", lastIncludedTerm.Value.ToString());
        await this._metadataStore.TakeFullCheckpointAsync(CheckpointType.FoldOver);

        this._logger.LogInformation("Log, index {Index}'e kadar başarıyla sıkıştırıldı.", lastIncludedIndex);
    }

    public async ValueTask<byte[]?> GetSnapshotDataAsync() {
        this._disposeState.ThrowIfDisposingOrDisposed(nameof(FasterState));
        if (!File.Exists(this._snapshotFilePath)) return null;
        return await File.ReadAllBytesAsync(this._snapshotFilePath);
    }
    #endregion

    public async ValueTask DisposeAsync() {
        if (!this._disposeState.TryBeginDispose()) return;
        this._logger.LogInformation("FASTER kalıcılık katmanı kapatılıyor...");

        // Metadata deposunu güvenli bir şekilde kapat
        this._metadataSession.Dispose();
        await this._metadataStore.TakeFullCheckpointAsync(CheckpointType.FoldOver);
        this._metadataStore.Dispose();
        this._metadataObjectLogDevice.Dispose();
        this._metadataDevice.Dispose();

        // Log deposunu güvenli bir şekilde kapat
        try {
            // UYGULAMA KAPANIRKEN VERİ KAYBINI ÖNLEMEK İÇİN EN GARANTİLİ YÖNTEM:
            // `Commit(true)` senkron ve engelleyici bir I/O çağrısıdır.
            // Bunu Task.Run içinde çalıştırmak, async context'i bloke etmeden
            // işlemin bitmesini beklememizi sağlar.
            await Task.Run(() => this._log.Commit(true));
        }
        catch (Exception ex) {
            this._logger.LogCritical(ex, "FASTER Log kapatılırken son commit sırasında kritik hata.");
        }
        finally {
            this._log.Dispose();
            this._logDevice.Dispose();
        }

        this._logger.LogInformation("FASTER kalıcılık katmanı başarıyla kapatıldı.");
        this._disposeState.SetDisposed();
    }
}