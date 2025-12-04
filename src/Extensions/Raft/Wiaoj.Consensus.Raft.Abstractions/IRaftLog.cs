namespace Wiaoj.Consensus.Raft.Abstractions;

/// <summary>
/// Raft'ın çoğaltılmış log'u için kalıcılık kontratını tanımlar.
/// </summary>
public interface IRaftLog : IAsyncDisposable {
    /// <summary>
    /// Log'un sonuna yeni bir girdi ekler.
    /// </summary>
    /// <param name="entry">Eklenecek log girdisi.</param>
    /// <returns>Yeni eklenen girdinin log indeksini döndürür.</returns>
    ValueTask<LogIndex> AppendAsync(LogEntry entry);

    ValueTask<LogIndex> AppendEntriesAsync(IReadOnlyList<LogEntry> entries);

    /// <summary>
    /// Belirtilen indeksten başlayarak log'un sonundaki tüm girdileri siler.
    /// </summary>
    /// <param name="fromIndex">Silme işleminin başlayacağı log indeksi.</param>
    ValueTask TruncateAsync(LogIndex fromIndex);

    /// <summary>
    /// Belirtilen indeksteki log girdisini alır.
    /// </summary>
    /// <param name="index">Alınacak girdinin indeksi.</param>
    /// <returns>Log girdisini veya bulunamazsa null döner.</returns>
    LogEntry? Get(LogIndex index);

    /// <summary>
    /// Log'daki son girdinin dönemini ve indeksini alır.
    /// </summary>
    /// <returns>Son girdinin dönem ve indeksini içeren bir tuple.</returns>
    (Term Term, LogIndex Index) GetLastEntryInfo();

    /// <summary>
    /// Log'daki son girdinin indeksini alır. Log boşsa 0 olmalıdır.
    /// </summary>
    LogIndex LastIndex { get; }

    /// <summary>
    /// Log'un başlangıcını temsil eden en son snapshot'ın indeksini alır.
    /// </summary>
    LogIndex LastSnapshotIndex { get; }

    /// <summary>
    /// Log'un başlangıcını temsil eden en son snapshot'ın dönemini alır.
    /// </summary>
    Term LastSnapshotTerm { get; }

    /// <summary>
    /// Belirtilen indekse kadar olan log'u sıkıştırır ve verilen snapshot'ı kalıcı hale getirir.
    /// Bu işlem, belirtilen indeksten önceki TÜM log girdilerini siler.
    /// </summary>
    /// <param name="snapshotData">Durum makinesinin anlık görüntüsü.</param>
    /// <param name="lastIncludedIndex">Snapshot'ın içerdiği son log girdisinin indeksi.</param>
    /// <param name="lastIncludedTerm">Snapshot'ın içerdiği son log girdisinin dönemi.</param>
    ValueTask CompactLogAsync(byte[] snapshotData, LogIndex lastIncludedIndex, Term lastIncludedTerm);
    ValueTask<byte[]?> GetSnapshotDataAsync();
}