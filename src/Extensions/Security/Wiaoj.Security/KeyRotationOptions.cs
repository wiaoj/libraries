namespace Wiaoj.Security;

/// <summary>
/// Configuration for the automatic key rotation system.
/// </summary>
public sealed class KeyRotationOptions {
    public static readonly TimeSpan DefaultRotationInterval = TimeSpan.FromDays(90);

    // ── Rotation Timing ───────────────────────────────────────────────────────

    /// <summary>
    /// How long a key stays active before being rotated.
    /// Default: 90 days.
    /// </summary>
    public TimeSpan RotationInterval { get; set; } = DefaultRotationInterval;

    /// <summary>
    /// How often the background service wakes up to check if rotation is needed.
    /// Default: 6 hours.
    /// </summary>
    public TimeSpan CheckInterval { get; set; } = TimeSpan.FromHours(6);

    /// <summary>
    /// If an error occurs during the rotation check (e.g., DB is down), 
    /// how long to wait before trying again, overriding the normal CheckInterval.
    /// Default: 5 minutes.
    /// </summary>
    public TimeSpan RetryIntervalOnError { get; set; } = TimeSpan.FromMinutes(5);

    // ── Cryptography ──────────────────────────────────────────────────────────

    /// <summary>
    /// AES key size in bits. Must be 128, 192, or 256.
    /// Default: 256.
    /// </summary>
    public int KeySizeInBits { get; set; } = 256;
    public int KeySizeInBytes => KeySizeInBits / 8;

    // ── Data Rotation (Re-encryption) ─────────────────────────────────────────

    /// <summary>
    /// When true, the background service will also re-encrypt application data
    /// encrypted with older key versions after generating a new key.
    /// Default: true.
    /// </summary>
    public bool AutoRotateData { get; set; } = true;

    /// <summary>
    /// How many records to re-encrypt per batch during data rotation.
    /// Default: 100.
    /// </summary>
    public int DataRotationBatchSize { get; set; } = 100;

    /// <summary>
    /// A small delay injected between batches during data rotation to prevent 
    /// saturating the database CPU and thread pool. 
    /// Default: 50 milliseconds.
    /// </summary>
    public TimeSpan DataRotationBatchDelay { get; set; } = TimeSpan.FromMilliseconds(50);

    // ── Validation ────────────────────────────────────────────────────────────

    public void Validate() {
        if(KeySizeInBits is not (128 or 192 or 256))
            throw new InvalidOperationException("KeySizeInBits must be 128, 192, or 256.");

        if(RotationInterval <= TimeSpan.Zero)
            throw new InvalidOperationException("RotationInterval must be positive.");

        if(CheckInterval <= TimeSpan.Zero)
            throw new InvalidOperationException("CheckInterval must be positive.");

        if(RetryIntervalOnError <= TimeSpan.Zero)
            throw new InvalidOperationException("RetryIntervalOnError must be positive.");

        if(DataRotationBatchSize <= 0)
            throw new InvalidOperationException("DataRotationBatchSize must be positive.");

        if(DataRotationBatchDelay < TimeSpan.Zero)
            throw new InvalidOperationException("DataRotationBatchDelay cannot be negative.");
    }
}