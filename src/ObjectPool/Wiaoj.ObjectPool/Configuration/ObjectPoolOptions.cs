namespace Wiaoj.ObjectPool;
/// <summary>
/// Provides configuration options for an object pool.
/// </summary>
public sealed class ObjectPoolOptions {
    /// <summary>
    /// Gets or sets the maximum number of objects to retain in the pool when 
    /// an object is returned. The default value is twice the number of processors.
    /// </summary>
    /// <remarks>
    /// Setting this value helps to prevent excessive memory consumption by limiting
    /// the number of idle objects stored in the pool.
    /// </remarks>
    public int MaximumRetained { get; set; } = Environment.ProcessorCount * 2;

    public PoolAccessMode AccessMode { get; set; } = PoolAccessMode.FIFO;
}

public enum PoolAccessMode {
    /// <summary>
    /// (Varsayılan) Havuz boşsa beklemez, hemen yeni üretir. Limit sadece havuza geri dönerken geçerlidir.
    /// Yüksek performans ve düşük latency için uygundur.
    /// </summary>
    FIFO = 0, // veya "Elastic"

    /// <summary>
    /// Havuz limiti dolduysa, biri nesne iade edene kadar BEKLER.
    /// Veritabanı bağlantıları gibi sınırlı kaynaklar için uygundur.
    /// </summary>
    Bounded = 1 // veya "Fixed"
}