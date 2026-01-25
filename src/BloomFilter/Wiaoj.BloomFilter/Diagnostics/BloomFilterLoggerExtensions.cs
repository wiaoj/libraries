using Microsoft.Extensions.Logging;

namespace Wiaoj.BloomFilter.Diagnostics;
/// <summary>
/// Provides high-performance, structured logging extension methods for Bloom Filter operations.
/// Uses [LoggerMessage] source generator for zero-allocation logging when log levels are disabled.
/// </summary>
public static partial class BloomFilterLoggerExtensions {

    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Information,
        Message = "BloomFilter '{FilterName}' initialized. Capacity: {ExpectedItems}, ErrorRate: {ErrorRate}, Size: {SizeInBits} bits.")]
    public static partial void LogFilterInitialized(this ILogger logger, FilterName filterName, long expectedItems, double errorRate, long sizeInBits);

    [LoggerMessage(
        EventId = 1002,
        Level = LogLevel.Debug,
        Message = "Hydrating BloomFilter '{FilterName}' from stream...")]
    public static partial void LogHydratingFromStream(this ILogger logger, FilterName filterName);

    [LoggerMessage(
        EventId = 1003,
        Level = LogLevel.Information,
        Message = "BloomFilter '{FilterName}' successfully hydrated from stream.")]
    public static partial void LogHydrationSuccess(this ILogger logger, FilterName filterName);

    [LoggerMessage(
        EventId = 1004,
        Level = LogLevel.Warning,
        Message = "BloomFilter '{FilterName}' stream header is invalid or missing. Attempting legacy/raw read.")]
    public static partial void LogInvalidHeaderWarning(this ILogger logger, FilterName filterName);

    [LoggerMessage(
        EventId = 1005,
        Level = LogLevel.Warning,
        Message = "Stream for '{FilterName}' ended unexpectedly. Expected {ExpectedBytes} bytes, but read {ReadBytes} bytes.")]
    public static partial void LogIncompleteStreamWarning(this ILogger logger, FilterName filterName, int expectedBytes, int readBytes);

    [LoggerMessage(
        EventId = 1006,
        Level = LogLevel.Debug,
        Message = "Checksum verified for '{FilterName}'.")]
    public static partial void LogChecksumVerified(this ILogger logger, FilterName filterName);

    [LoggerMessage(
        EventId = 1007,
        Level = LogLevel.Trace,
        Message = "Item added to '{FilterName}'. Bits updated.")]
    public static partial void LogItemAdded(this ILogger logger, FilterName filterName);

    [LoggerMessage(
        EventId = 1008,
        Level = LogLevel.Debug,
        Message = "Starting persistence for '{FilterName}'. Waiting for I/O lock...")]
    public static partial void LogSaveStarted(this ILogger logger, FilterName filterName);

    [LoggerMessage(
        EventId = 1009,
        Level = LogLevel.Information,
        Message = "Saved '{FilterName}' to storage. Checksum: {Checksum:X}, Size: {SizeBytes} bytes.")]
    public static partial void LogSaveSuccess(this ILogger logger, FilterName filterName, ulong checksum, int sizeBytes);

    [LoggerMessage(
        EventId = 1010,
        Level = LogLevel.Error,
        Message = "Failed to save BloomFilter '{FilterName}' to storage.")]
    public static partial void LogSaveFailed(this ILogger logger, Exception ex, FilterName filterName);

    [LoggerMessage(
        EventId = 1011,
        Level = LogLevel.Information,
        Message = "Reloaded '{FilterName}' successfully. Verified Checksum: {Checksum:X}.")]
    public static partial void LogReloadSuccess(this ILogger logger, FilterName filterName, ulong checksum);

    [LoggerMessage(
        EventId = 1012,
        Level = LogLevel.Warning,
        Message = "Reload failed for '{FilterName}': Data not found in storage.")]
    public static partial void LogReloadNotFound(this ILogger logger, FilterName filterName);

    [LoggerMessage(
        EventId = 1020,
        Level = LogLevel.Debug,
        Message = "Triggering auto-save for dirty filters...")]
    public static partial void LogAutoSaveTriggered(this ILogger logger);

    [LoggerMessage(
        EventId = 1021,
        Level = LogLevel.Error,
        Message = "Auto-save failed for filter '{FilterName}'.")]
    public static partial void LogAutoSaveFailed(this ILogger logger, Exception ex, FilterName filterName);

    [LoggerMessage(
        EventId = 1030,
        Level = LogLevel.Information,
        Message = "Seeding filter '{FilterName}' started...")]
    public static partial void LogSeedingStarted(this ILogger logger, FilterName filterName);

    [LoggerMessage(
        EventId = 1031,
        Level = LogLevel.Information,
        Message = "Seeding '{FilterName}': {Count} items processed...")]
    public static partial void LogSeedingProgress(this ILogger logger, FilterName filterName, long count);

    [LoggerMessage(
        EventId = 1032,
        Level = LogLevel.Information,
        Message = "Filter '{FilterName}' seeded successfully with {Count} total items.")]
    public static partial void LogSeedingCompleted(this ILogger logger, FilterName filterName, long count);

    [LoggerMessage(
        EventId = 1040,
        Level = LogLevel.Trace,
        Message = "Synchronous access to uninitialized filter '{FilterName}'. Blocking thread until load completes.")]
    public static partial void LogSyncLazyBlocking(this ILogger logger, FilterName filterName);

    [LoggerMessage(
        EventId = 1041,
        Level = LogLevel.Debug,
        Message = "Lazy loading triggered for '{FilterName}'...")]
    public static partial void LogLazyLoadTriggered(this ILogger logger, FilterName filterName);

    [LoggerMessage(
        EventId = 1042,
        Level = LogLevel.Information,
        Message = "Lazy load for '{FilterName}' completed in {Elapsed}ms.")]
    public static partial void LogLazyLoadCompleted(this ILogger logger, FilterName filterName, long elapsed);

    [LoggerMessage(EventId = 2001, Level = LogLevel.Information,
    Message = "BloomFilter '{FilterName}' state synchronized. Bits: {BitsSet}, FillRatio: {FillRatio:P4}")]
    public static partial void LogFilterState(this ILogger logger, FilterName filterName, long bitsSet, double fillRatio);

    [LoggerMessage(EventId = 2002, Level = LogLevel.Warning,
        Message = "BloomFilter '{FilterName}' is nearing saturation. Current FillRatio: {FillRatio:P2}. False positive probability: {FpProb:P4}")]
    public static partial void LogSaturationWarning(this ILogger logger, FilterName filterName, double fillRatio, double fpProb);

    [LoggerMessage(EventId = 5001, Level = LogLevel.Error,
        Message = "Critical failure in BloomFilter '{FilterName}' during '{Operation}': {ErrorMessage}")]
    public static partial void LogCriticalOperationError(this ILogger logger, Exception ex, FilterName filterName, string operation, string errorMessage);
}