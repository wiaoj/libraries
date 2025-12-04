namespace Wiaoj.Consensus.Raft.Abstractions;

/// <summary>
/// Raft tarafından yönetilecek olan, çoğaltılmış durum makinesinin kontratı.
/// </summary>
public interface IStateMachine {
    /// <summary>
    /// Küme tarafından kesinleşmiş bir komutu durum makinesine uygular.
    /// </summary>
    /// <param name="command">Uygulanacak komutun byte dizisi.</param>
    /// <returns>Uygulama sonucunu temsil eden bir byte dizisi.</returns>
    Task<CommandPayload> ApplyAsync(byte[] command);

    /// <summary>
    /// Durum makinesinin o anki durumunun bir anlık görüntüsünü (snapshot) oluşturur.
    /// </summary>
    /// <returns>Durumun serialize edilmiş halini içeren byte dizisi.</returns>
    Task<byte[]> CreateSnapshotAsync();

    /// <summary>
    /// Verilen bir anlık görüntüden (snapshot) durum makinesinin durumunu tamamen geri yükler.
    /// </summary>
    /// <param name="snapshot">Geri yüklenecek durumu içeren byte dizisi.</param>
    Task RestoreFromSnapshotAsync(byte[] snapshot);
}