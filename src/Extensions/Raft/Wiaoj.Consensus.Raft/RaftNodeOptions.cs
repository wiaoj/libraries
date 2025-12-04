namespace Wiaoj.Consensus.Raft;

public sealed class RaftNodeOptions {
    /// <summary>
    /// Küme içindeki bu düğümün benzersiz kimliği.
    /// </summary>
    public required string NodeId { get; set; }

    /// <summary>
    /// Kümedeki diğer tüm düğümlerin adresleri.
    /// (Örn: "http://localhost:5001", "http://localhost:5002")
    /// </summary>
    public IReadOnlyList<string> Peers { get; set; } = [];

    /// <summary>
    /// Kalıcı durumun saklanacağı dizin yolu.
    /// </summary>
    public required string PersistencePath { get; set; }

    /// <summary>
    /// Liderden kalp atışı (heartbeat) gelmediğinde bir seçimin ne kadar
    /// süre sonra başlayacağını belirler. Genellikle 150ms-300ms arasıdır.
    /// </summary>
    public TimeSpan ElectionTimeout { get; set; } = TimeSpan.FromMilliseconds(1250);

    /// <summary>
    /// Liderin takipçilere ne sıklıkla kalp atışı (heartbeat) göndereceğini belirler.
    /// Bu, ElectionTimeout'tan önemli ölçüde daha küçük olmalıdır.
    /// </summary>
    public TimeSpan HeartbeatInterval { get; set; } = TimeSpan.FromMilliseconds(1100);

    /// <summary>
    /// Diğer düğümlere yapılan RPC çağrıları için zaman aşımı süresi.
    /// </summary>
    public TimeSpan RpcTimeout { get; set; } = TimeSpan.FromMilliseconds(1100);

    /// <summary>
    /// Bir snapshot'ın ne sıklıkla alınacağını belirler.
    /// Durum makinesine uygulanan log girdisi sayısı bu değeri aştığında
    /// yeni bir snapshot tetiklenir.
    /// </summary>
    public int SnapshotThreshold { get; set; } = 10000;
}