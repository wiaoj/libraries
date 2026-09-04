using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.IO.Compression;
using Wiaoj.Preconditions;

namespace Wiaoj.BloomFilter.Storage;
/// <summary>
/// File system persistence provider for Bloom Filters.
/// Implements atomic file replacements and compression support without external lock files.
/// </summary>
internal sealed class FileSystemBloomFilterStorage : IBloomFilterStorage {
    private readonly string _baseDirectory;
    private readonly bool _enableCompression;
    private readonly int _bufferSize;
    private readonly bool _ignoreErrors;
    private readonly ILogger<FileSystemBloomFilterStorage> _logger;
    private const string Extension = ".wbf";

    /// <summary>
    /// Initializes a new instance of the <see cref="FileSystemBloomFilterStorage"/> class.
    /// </summary>
    public FileSystemBloomFilterStorage(
        IOptions<BloomFilterOptions> options,
        ILogger<FileSystemBloomFilterStorage> logger) {

        StorageOptions opts = options.Value.Storage;
        this._logger = logger;
        this._enableCompression = opts.EnableCompression;
        this._bufferSize = opts.BufferSizeBytes;
        this._ignoreErrors = opts.IgnoreErrors;

        string path = string.IsNullOrWhiteSpace(opts.Path) ? "BloomData" : opts.Path;
        this._baseDirectory = Path.IsPathFullyQualified(path)
            ? path
            : Path.Combine(AppContext.BaseDirectory, path);

        if(!Directory.Exists(this._baseDirectory)) {
            Directory.CreateDirectory(this._baseDirectory);
        }
    }

    /// <inheritdoc/>
    public async Task<bool> SaveAsync(
        FilterName filterName,
        BloomFilterConfiguration config,
        Stream source,
        CancellationToken cancellationToken = default) {

        if(filterName.IsEmpty) {
            throw new ArgumentException("Filter name cannot be empty.", nameof(filterName));
        }

        Preca.ThrowIfNull(config, nameof(config));
        Preca.ThrowIfNull(source, nameof(source));

        string finalPath = GetPath(filterName);
        string tempPath = $"{finalPath}.{Guid.NewGuid():N}.tmp";

        try {
            await using(FileStream fs = new(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, this._bufferSize, useAsync: true)) {
                if(this._enableCompression) {
                    await using GZipStream gzip = new(fs, CompressionLevel.Fastest, leaveOpen: true);
                    await source.CopyToAsync(gzip, cancellationToken).ConfigureAwait(false);
                    await gzip.FlushAsync(cancellationToken).ConfigureAwait(false);
                }
                else {
                    await source.CopyToAsync(fs, cancellationToken).ConfigureAwait(false);
                }

                await fs.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            // Atomic file replacement guaranteed by the OS kernel
            File.Move(tempPath, finalPath, overwrite: true);
            return true;
        }
        catch(Exception ex) {
            CleanupTemporaryFile(tempPath);

            if(!this._ignoreErrors) {
                throw;
            }

            this._logger.LogError(ex, "Failed to persist Bloom Filter '{Name}' to file system.", filterName.Value);
            return false;
        }
    }

    /// <inheritdoc/>
    public ValueTask<(BloomFilterConfiguration? Config, Stream DataStream)?> LoadStreamAsync(
        FilterName filterName,
        CancellationToken cancellationToken = default) {

        if(filterName.IsEmpty) {
            throw new ArgumentException("Filter name cannot be empty.", nameof(filterName));
        }

        try {
            string path = GetPath(filterName);
            if(!File.Exists(path) || new FileInfo(path).Length == 0) {
                return ValueTask.FromResult<(BloomFilterConfiguration?, Stream)?>(null);
            }

            Stream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, this._bufferSize, useAsync: true);

            if(this._enableCompression) {
                fs = new GZipStream(fs, CompressionMode.Decompress);
            }

            return ValueTask.FromResult<(BloomFilterConfiguration?, Stream)?>((null, fs));
        }
        catch(Exception ex) when(this._ignoreErrors) {
            this._logger.LogError(ex, "Failed to load Bloom Filter stream for '{Name}'.", filterName.Value);
            return ValueTask.FromResult<(BloomFilterConfiguration?, Stream)?>(null);
        }
    }

    /// <inheritdoc/>
    public Task DeleteAsync(
        FilterName filterName,
        CancellationToken cancellationToken = default) {

        if(filterName.IsEmpty) {
            throw new ArgumentException("Filter name cannot be empty.", nameof(filterName));
        }

        try {
            string pattern = $"{filterName.Value}*{Extension}";
            foreach(string file in Directory.GetFiles(this._baseDirectory, pattern)) {
                File.Delete(file);
            }
        }
        catch(Exception ex) when(this._ignoreErrors) {
            this._logger.LogError(ex, "Failed to delete filter files for '{Name}'.", filterName.Value);
        }

        return Task.CompletedTask;
    }

    private string GetPath(FilterName name) {
        return Path.Combine(this._baseDirectory, $"{name.Value}{Extension}");
    }

    private void CleanupTemporaryFile(string path) {
        try {
            if(File.Exists(path)) {
                File.Delete(path);
            }
        }
        catch(Exception ex) {
            this._logger.LogWarning(ex, "Failed to clean up temporary file '{Path}'.", path);
        }
    }
}