namespace Wiaoj.Consensus.Raft.Abstractions;

/// <summary>
/// Kümedeki diğer düğümlerle (peer) olan iletişimi yönetir.
/// </summary>
public interface IRaftCluster {
    /// <summary>
    /// Kümedeki mevcut düğüm hariç diğer tüm düğümleri (peer) alır.
    /// </summary>
    IEnumerable<IRaftPeer> Peers { get; }

    /// <summary>
    /// Mevcut düğüm dahil kümedeki toplam düğüm sayısını alır.
    /// </summary>
    int TotalNodes { get; }
}
