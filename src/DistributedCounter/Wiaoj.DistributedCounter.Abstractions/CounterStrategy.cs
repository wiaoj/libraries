namespace Wiaoj.DistributedCounter;
/// <summary>
/// Defines how the counter operations are synchronized with the remote storage.
/// </summary>
public enum CounterStrategy {
    /// <summary>
    /// Every increment request is sent directly to the storage. 
    /// Guaranteed consistency but higher latency. Use for critical quotas.
    /// </summary>
    Immediate,

    /// <summary>
    /// Increments are aggregated in memory and flushed to storage periodically. 
    /// High performance but carries a risk of data loss on application crash.
    /// </summary>
    Buffered
}