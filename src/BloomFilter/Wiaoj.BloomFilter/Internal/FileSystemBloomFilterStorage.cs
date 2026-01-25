using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.IO.Compression;

namespace Wiaoj.BloomFilter.Internal;

/// <summary>
/// File-system implementation with support for compression and atomic writes.
/// </summary>
public class FileSystemBloomFilterStorage : IBloomFilterStorage {
    private readonly string _baseDirectory;
    private readonly bool _enableCompression;
    private readonly int _bufferSize;
    private readonly bool _ignoreErrors;
    private readonly ILogger<FileSystemBloomFilterStorage> _logger;
    private const string Ext = ".wbf";

    public FileSystemBloomFilterStorage(IOptions<BloomFilterOptions> options, ILogger<FileSystemBloomFilterStorage> logger) {
        StorageOptions opts = options.Value.Storage;
        this._logger = logger;
        this._enableCompression = opts.EnableCompression;
        this._bufferSize = opts.BufferSizeBytes;
        this._ignoreErrors = opts.IgnoreErrors;

        var path = opts.Path;
        if(string.IsNullOrWhiteSpace(path)) path = "BloomData";

        this._baseDirectory = Path.IsPathFullyQualified(path)
            ? path
            : Path.Combine(AppContext.BaseDirectory, path);

        if(!Directory.Exists(this._baseDirectory)) Directory.CreateDirectory(this._baseDirectory);
    }

    public async ValueTask SaveAsync(string filterName, BloomFilterConfiguration config, Stream source, CancellationToken ct = default) {
        var finalPath = GetPath(filterName);
        var tempPath = finalPath + ".tmp";
        var lockPath = finalPath + ".lock";

        using(await AcquireLockAsync(lockPath, TimeSpan.FromSeconds(10), ct)) {
            try {
                using(var fs = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, this._bufferSize, useAsync: true)) {
                    if(this._enableCompression) {
                        // Wrap with GZip if enabled
                        using var gzip = new GZipStream(fs, CompressionLevel.Fastest, leaveOpen: true);
                        await source.CopyToAsync(gzip, ct);
                    }
                    else {
                        await source.CopyToAsync(fs, ct);
                    }
                    await fs.FlushAsync(ct);
                }

                File.Move(tempPath, finalPath, overwrite: true);
            }
            catch(Exception ex) when(this._ignoreErrors) {
                this._logger.LogError(ex, "Failed to save filter '{Name}' to file system.", filterName);
            }
        }
    }

    public ValueTask<(BloomFilterConfiguration Config, Stream DataStream)?> LoadStreamAsync(string filterName, CancellationToken ct = default) {
        try {
            var path = GetPath(filterName);
            if(!File.Exists(path) || new FileInfo(path).Length == 0)
                return ValueTask.FromResult<(BloomFilterConfiguration, Stream)?>(null);

            Stream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, this._bufferSize, useAsync: true);

            if(this._enableCompression) {
                // Return the decompression stream
                // Note: The consumer is responsible for disposing this, which will dispose the underlying fs.
                fs = new GZipStream(fs, CompressionMode.Decompress);
            }

            return ValueTask.FromResult<(BloomFilterConfiguration, Stream)?>((null!, fs));
        }
        catch(Exception ex) when(this._ignoreErrors) {
            this._logger.LogError(ex, "Failed to load filter '{Name}' from file system.", filterName);
            return ValueTask.FromResult<(BloomFilterConfiguration, Stream)?>(null);
        }
    }

    public Task DeleteAsync(string filterName, CancellationToken cancellationToken = default) {
        try {
            var pattern = $"{filterName}*{Ext}";
            foreach(var file in Directory.GetFiles(this._baseDirectory, pattern)) {
                File.Delete(file);
            }
        }
        catch(Exception ex) when(this._ignoreErrors) {
            this._logger.LogError(ex, "Failed to delete filter '{Name}'.", filterName);
        }
        return Task.CompletedTask;
    }

    private string GetPath(string name) {
        return Path.Combine(this._baseDirectory, $"{name}{Ext}");
    }

    private async Task<IDisposable> AcquireLockAsync(string lockPath, TimeSpan timeout, CancellationToken ct) {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        while(sw.Elapsed < timeout) {
            ct.ThrowIfCancellationRequested();

            try {
                // FileMode.CreateNew: Dosya yoksa oluşturur (başarılı), varsa HATA fırlatır (atomik).
                var fs = new FileStream(lockPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1, FileOptions.DeleteOnClose);

                // Başarılı olduk, bu stream'i kapatınca dosya silinecek (DeleteOnClose).
                // Stream'i tutan basit bir Disposable dönüyoruz.
                return fs;
            }
            catch(IOException) {
                // Dosya zaten var, yani kilitli. Biraz bekle.
                // İleri seviye: Dosya oluşturulma tarihi çok eskiyse (örn 5 dk) "stale lock" diyip silebilirsiniz.
            }

            await Task.Delay(100, ct); // 100ms bekle tekrar dene
        }

        throw new TimeoutException($"Could not acquire lock for {lockPath} after {timeout.TotalSeconds} seconds.");
    }
}