namespace Wiaoj.ObjectPool.Configuration;
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

#if DEBUG
    /// <summary>
    /// Gets or sets a value indicating whether leak detection should be enabled.
    /// When enabled, the pool tracks leased objects and reports a leak if an object
    /// is garbage-collected without being returned.
    /// This feature is only available in DEBUG builds and is enabled by default.
    /// </summary>
    public bool LeakDetectionEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets an action that validates an object's state just before it's returned to the pool.
    /// If the object's state is invalid (i.e., not properly reset), this action should throw an exception
    /// to immediately alert the developer of the logic error in the resetter.
    /// This feature is only active in DEBUG builds.
    /// </summary>
    /// <example>
    /// options.OnReturnValidation = list => {
    ///     if (list.Count > 0) throw new InvalidOperationException("List was not cleared!");
    /// };
    /// </example>
    public Action<object>? OnReturnValidation { get; set; }
#endif
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