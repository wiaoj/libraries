namespace Wiaoj.BloomFilter.Storage;

/// <summary>
/// Configuration options for the file system Bloom Filter persistence provider.
/// </summary>
public sealed class FileSystemStorageOptions {
    /// <summary>
    /// The configuration section name in application settings.
    /// </summary>
    public const string SectionName = "BloomFilter:FileSystemStorage";

    /// <summary>
    /// Gets or sets the base directory path for file persistence. Default: "BloomData".
    /// </summary>
    public string Path { get; set; } = "BloomData";

    /// <summary>
    /// Gets or sets a value indicating whether GZip compression is enabled for snapshots. Default: false.
    /// </summary>
    public bool EnableCompression { get; set; }

    /// <summary>
    /// Gets or sets the buffer size in bytes for storage streams. Default: 81920 (80 KB).
    /// </summary>
    public int BufferSizeBytes { get; set; } = 81920;

    /// <summary>
    /// Gets or sets a value indicating whether to suppress storage I/O errors and operate in-memory. Default: true.
    /// </summary>
    public bool IgnoreErrors { get; set; } = true;
}
