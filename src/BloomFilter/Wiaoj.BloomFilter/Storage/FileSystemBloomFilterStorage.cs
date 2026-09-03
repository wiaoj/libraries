using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.IO.Compression;
using System.Text.Json;
using Wiaoj.Preconditions;

namespace Wiaoj.BloomFilter;

/// <summary>
/// File system persistence provider for Bloom Filters.
/// Implements atomic file replacements, stale lock recovery, and uncancelled cleanup on aborts.
/// </summary>
internal sealed class FileSystemBloomFilterStorage : IBloomFilterStorage {
    private readonly string _baseDirectory;
    private readonly bool _enableCompression;
    private readonly int _bufferSize;
    private readonly bool _ignoreErrors;
    private readonly ILogger<FileSystemBloomFilterStorage> _logger;
    private const string Extension = ".wbf";

    private static readonly TimeSpan StaleLockThreshold = TimeSpan.FromSeconds(30);

    public FileSystemBloomFilterStorage(IOptions<BloomFilterOptions> options, ILogger<FileSystemBloomFilterStorage> logger) {
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
        string tempPath = finalPath + ".tmp";
        string lockPath = finalPath + ".lock";

        await using FileLockHandle lockHandle = await AcquireLockAsync(lockPath, TimeSpan.FromSeconds(10), cancellationToken).ConfigureAwait(false);

        try {
            await using(FileStream fs = new(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, this._bufferSize, useAsync: true)) {
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

            File.Move(tempPath, finalPath, overwrite: true);
            return true;
        }
        catch(OperationCanceledException) {
            CleanupBestEffort(tempPath);
            throw;
        }
        catch(Exception ex) {
            CleanupBestEffort(tempPath);

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

    private void CleanupBestEffort(string path) {
        try {
            if(File.Exists(path)) {
                File.Delete(path);
            }
        }
        catch(Exception ex) {
            this._logger.LogWarning(ex, "Failed to clean up temporary file '{Path}'.", path);
        }
    }

    private async ValueTask<FileLockHandle> AcquireLockAsync(string lockPath, TimeSpan timeout, CancellationToken ct) {
        Stopwatch sw = Stopwatch.StartNew();
        TimeSpan delay = TimeSpan.FromMilliseconds(20);

        while(sw.Elapsed < timeout) {
            ct.ThrowIfCancellationRequested();

            try {
                FileStream fs = new(lockPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1, FileOptions.DeleteOnClose);
                await WriteLockOwnerAsync(fs, ct).ConfigureAwait(false);
                return new FileLockHandle(fs);
            }
            catch(IOException) {
                if(TryReclaimStaleLock(lockPath)) {
                    continue;
                }

                TimeSpan jittered = delay + TimeSpan.FromMilliseconds(Random.Shared.Next(0, 25));
                await Task.Delay(jittered, ct).ConfigureAwait(false);
                delay = TimeSpan.FromMilliseconds(Math.Min(delay.TotalMilliseconds * 1.5, 500));
            }
        }

        throw new TimeoutException($"Could not acquire lock for '{lockPath}' within {timeout.TotalSeconds:F0} seconds.");
    }

    private static async ValueTask WriteLockOwnerAsync(FileStream lockStream, CancellationToken ct) {
        LockOwnerInfo info = new(Environment.ProcessId, Environment.MachineName, DateTimeOffset.UtcNow);
        await JsonSerializer.SerializeAsync(lockStream, info, cancellationToken: ct).ConfigureAwait(false);
        await lockStream.FlushAsync(ct).ConfigureAwait(false);
    }

    private bool TryReclaimStaleLock(string lockPath) {
        try {
            if(!File.Exists(lockPath)) return false;

            TimeSpan age = DateTimeOffset.UtcNow - new FileInfo(lockPath).LastWriteTimeUtc;
            bool ownerLooksDead = false;

            try {
                using FileStream content = new(lockPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                LockOwnerInfo? owner = JsonSerializer.Deserialize<LockOwnerInfo>(content);

                if(owner is { } o && o.MachineName == Environment.MachineName) {
                    try {
                        Process.GetProcessById(o.ProcessId);
                    }
                    catch(ArgumentException) {
                        ownerLooksDead = true;
                    }
                }
            }
            catch { /* File is being written or unreadable */ }

            if(ownerLooksDead || age > StaleLockThreshold) {
                this._logger.LogWarning("Reclaiming abandoned lock '{Path}'.", lockPath);
                File.Delete(lockPath);
                return true;
            }
            return false;
        }
        catch(IOException) {
            return false;
        }
    }

    private readonly record struct LockOwnerInfo(int ProcessId, string MachineName, DateTimeOffset AcquiredAtUtc);

    private sealed class FileLockHandle(FileStream stream) : IAsyncDisposable {
        public ValueTask DisposeAsync() {
            stream.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}