using Microsoft.Extensions.Logging;

namespace Wiaoj.BloomFilter.Diagnostics;

/// <summary>
/// High-performance, zero-allocation structured logging for Bloom Filter operations.
/// </summary>
internal static partial class BloomFilterLoggerExtensions {

    #region 1000 - 1999: Debug & Trace (Routine Background Flow)

    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Debug,
        Message = "Starting persistence for '{FilterName}'. Waiting for I/O lock...")]
    public static partial void LogSaveStarted(this ILogger logger, FilterName filterName);

    [LoggerMessage(
        EventId = 1002,
        Level = LogLevel.Debug,
        Message = "Saved '{FilterName}' to storage. Checksum: {Checksum:X}, Size: {SizeBytes:N0} bytes.")]
    public static partial void LogSaveSuccess(this ILogger logger, FilterName filterName, ulong checksum, int sizeBytes);

    [LoggerMessage(
        EventId = 1003,
        Level = LogLevel.Debug,
        Message = "Hydrating BloomFilter '{FilterName}' from storage stream...")]
    public static partial void LogHydratingFromStream(this ILogger logger, FilterName filterName);

    [LoggerMessage(
        EventId = 1004,
        Level = LogLevel.Debug,
        Message = "Reloaded '{FilterName}' successfully from storage. Verified Checksum: {Checksum:X}.")]
    public static partial void LogReloadSuccess(this ILogger logger, FilterName filterName, ulong checksum);

    [LoggerMessage(
        EventId = 1005,
        Level = LogLevel.Debug,
        Message = "Snapshot file for '{FilterName}' not found in storage. Initializing clean in-memory filter.")]
    public static partial void LogReloadNotFound(this ILogger logger, FilterName filterName);

    [LoggerMessage(
        EventId = 1006,
        Level = LogLevel.Debug,
        Message = "Triggering periodic auto-save cycle for dirty filters...")]
    public static partial void LogAutoSaveTriggered(this ILogger logger);

    [LoggerMessage(
        EventId = 1007,
        Level = LogLevel.Debug,
        Message = "Lazy loading triggered for '{FilterName}'...")]
    public static partial void LogLazyLoadTriggered(this ILogger logger, FilterName filterName);

    [LoggerMessage(
        EventId = 1008,
        Level = LogLevel.Debug,
        Message = "Lazy load for '{FilterName}' completed in {ElapsedMs}ms.")]
    public static partial void LogLazyLoadCompleted(this ILogger logger, FilterName filterName, long elapsedMs);

    [LoggerMessage(
        EventId = 1009,
        Level = LogLevel.Trace,
        Message = "Synchronous access to uninitialized filter '{FilterName}'. Blocking thread until load completes.")]
    public static partial void LogSyncLazyBlocking(this ILogger logger, FilterName filterName);

    [LoggerMessage(
        EventId = 1010,
        Level = LogLevel.Debug,
        Message = "Seeding '{FilterName}' progress: {Count:N0} items processed...")]
    public static partial void LogSeedingProgress(this ILogger logger, FilterName filterName, long count);

    [LoggerMessage(
        EventId = 1011,
        Level = LogLevel.Debug,
        Message = "Global save triggered for all active Bloom Filters.")]
    public static partial void LogGlobalSaveTriggered(this ILogger logger);

    #endregion

    #region 2000 - 2999: Information (Real Milestones)

    [LoggerMessage(
        EventId = 2001,
        Level = LogLevel.Information,
        Message = "BloomFilter '{FilterName}' initialized. Capacity: {ExpectedItems:N0}, ErrorRate: {ErrorRate:P2}, Size: {SizeInBits:N0} bits ({SizeInBytes:N0} bytes), HashFunctions: {HashFunctions}.")]
    public static partial void LogFilterInitialized(
        this ILogger logger,
        FilterName filterName,
        long expectedItems,
        double errorRate,
        long sizeInBits,
        long sizeInBytes,
        int hashFunctions);

    [LoggerMessage(
        EventId = 2002,
        Level = LogLevel.Information,
        Message = "Starting pre-warming for all registered Bloom Filters...")]
    public static partial void LogWarmUpStarted(this ILogger logger);

    [LoggerMessage(
        EventId = 2003,
        Level = LogLevel.Information,
        Message = "All Bloom Filters successfully warmed up in {ElapsedMs}ms.")]
    public static partial void LogWarmUpCompleted(this ILogger logger, long elapsedMs);

    [LoggerMessage(
        EventId = 2004,
        Level = LogLevel.Information,
        Message = "Seeding process started for '{FilterName}'...")]
    public static partial void LogSeedingStarted(this ILogger logger, FilterName filterName);

    [LoggerMessage(
        EventId = 2005,
        Level = LogLevel.Information,
        Message = "Filter '{FilterName}' successfully seeded with {TotalCount:N0} items.")]
    public static partial void LogSeedingCompleted(this ILogger logger, FilterName filterName, long totalCount);

    [LoggerMessage(
        EventId = 2006,
        Level = LogLevel.Information,
        Message = "Performing final persistent flush on application shutdown...")]
    public static partial void LogFinalSaveStarted(this ILogger logger);

    [LoggerMessage(
        EventId = 2007,
        Level = LogLevel.Information,
        Message = "ScalableBloomFilter '{FilterName}' reached saturation ({FillRatio:P2}). Spawned Layer #{LayerIndex} with capacity {NewCapacity:N0} items.")]
    public static partial void LogScalableLayerSpawned(
        this ILogger logger,
        FilterName filterName,
        double fillRatio,
        int layerIndex,
        long newCapacity);

    #endregion

    #region 3000 - 3999: Warning (Anomalies & Handled Degradations)

    [LoggerMessage(
        EventId = 3001,
        Level = LogLevel.Warning,
        Message = "BloomFilter '{FilterName}' stream header is invalid. Attempting fallback raw read.")]
    public static partial void LogInvalidHeaderWarning(this ILogger logger, FilterName filterName);

    [LoggerMessage(
        EventId = 3002,
        Level = LogLevel.Warning,
        Message = "Filter '{FilterName}' has been permanently deleted from storage.")]
    public static partial void LogFilterDeleted(this ILogger logger, FilterName filterName);

    [LoggerMessage(
        EventId = 3003,
        Level = LogLevel.Warning,
        Message = "Automatic reseeding for '{FilterName}' was aborted due to application shutdown.")]
    public static partial void LogSeedingAborted(this ILogger logger, FilterName filterName);

    [LoggerMessage(
        EventId = 3004,
        Level = LogLevel.Warning,
        Message = "Failed to clean up temporary storage file '{FilePath}'.")]
    public static partial void LogTemporaryFileCleanupFailed(this ILogger logger, Exception ex, string filePath);

    [LoggerMessage(
        EventId = 3005,
        Level = LogLevel.Warning,
        Message = "Failed to delete corrupted storage file for '{FilterName}'.")]
    public static partial void LogCorruptFileCleanupFailed(this ILogger logger, Exception ex, FilterName filterName);

    [LoggerMessage(
        EventId = 3006,
        Level = LogLevel.Warning,
        Message = "BloomFilter '{FilterName}' is nearing saturation! Current FillRatio: {FillRatio:P2}. False positive probability: {FpProb:P4}")]
    public static partial void LogSaturationWarning(this ILogger logger, FilterName filterName, double fillRatio, double fpProb);

    #endregion

    #region 4000 - 4999: Error (Failures)

    [LoggerMessage(
        EventId = 4001,
        Level = LogLevel.Error,
        Message = "Failed to persist BloomFilter '{FilterName}' to storage.")]
    public static partial void LogSaveFailed(this ILogger logger, Exception ex, FilterName filterName);

    [LoggerMessage(
        EventId = 4002,
        Level = LogLevel.Error,
        Message = "Failed to reload BloomFilter '{FilterName}' from storage.")]
    public static partial void LogReloadFailed(this ILogger logger, Exception ex, FilterName filterName);

    [LoggerMessage(
        EventId = 4003,
        Level = LogLevel.Error,
        Message = "Failed to hydrate BloomFilter '{FilterName}' from storage. Reinitializing clean filter.")]
    public static partial void LogHydrationFailed(this ILogger logger, Exception ex, FilterName filterName);

    [LoggerMessage(
        EventId = 4004,
        Level = LogLevel.Error,
        Message = "Auto-save background cycle failed for filter '{FilterName}'.")]
    public static partial void LogAutoSaveFailed(this ILogger logger, Exception ex, FilterName filterName);

    [LoggerMessage(
        EventId = 4005,
        Level = LogLevel.Error,
        Message = "Final shutdown save failed for filter '{FilterName}'.")]
    public static partial void LogFinalSaveFailed(this ILogger logger, Exception ex, FilterName filterName);

    [LoggerMessage(
        EventId = 4006,
        Level = LogLevel.Error,
        Message = "Failed to pre-warm BloomFilter '{FilterName}'.")]
    public static partial void LogWarmUpFilterFailed(this ILogger logger, Exception ex, FilterName filterName);

    [LoggerMessage(
        EventId = 4007,
        Level = LogLevel.Error,
        Message = "Execution failed during automatic reseeding of '{FilterName}'.")]
    public static partial void LogSeedingExecutionFailed(this ILogger logger, Exception ex, FilterName filterName);

    [LoggerMessage(
        EventId = 4008,
        Level = LogLevel.Error,
        Message = "Storage provider failed to load stream for '{FilterName}'.")]
    public static partial void LogStorageLoadFailed(this ILogger logger, Exception ex, FilterName filterName);

    [LoggerMessage(
        EventId = 4009,
        Level = LogLevel.Error,
        Message = "Storage provider failed to delete files for '{FilterName}'.")]
    public static partial void LogStorageDeleteFailed(this ILogger logger, Exception ex, FilterName filterName);

    #endregion

    #region 5000 - 5999: Critical

    [LoggerMessage(
        EventId = 5001,
        Level = LogLevel.Critical,
        Message = "Configuration missing for BloomFilter '{FilterName}'. Filter instance cannot be created.")]
    public static partial void LogMissingConfiguration(this ILogger logger, Exception ex, FilterName filterName);

    #endregion
}