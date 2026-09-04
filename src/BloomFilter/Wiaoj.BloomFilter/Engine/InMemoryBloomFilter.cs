
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using Wiaoj.BloomFilter.Diagnostics;
using Wiaoj.Concurrency;
using Wiaoj.ObjectPool;

namespace Wiaoj.BloomFilter.Engine;
/// <summary>
/// In-memory implementation of a persistent Bloom Filter.
/// Uses SIMD vectorization for hash evaluations and atomic operations for concurrency.
/// </summary>
internal sealed class InMemoryBloomFilter : BloomFilterBase {
    private volatile bool _isDirty;
    private PooledBitArray _bits;

    private readonly IBloomFilterStorage? _storage;
    private readonly ILogger _logger;
    private readonly BloomFilterOptions _options;
    private readonly IObjectPool<MemoryStream> _memoryStreamPool;
    private readonly TimeProvider _timeProvider;
    private readonly AsyncLock _ioLock = new();
    private readonly ReaderWriterLockSlim _rwLock = new(LockRecursionPolicy.NoRecursion);

    /// <inheritdoc/>
    public override bool IsDirty => this._isDirty;

    /// <inheritdoc/>
    public override FilterName Name => this.Configuration.Name;

    /// <inheritdoc/>
    public override BloomFilterConfiguration Configuration { get; }

    /// <summary>
    /// Gets the timestamp when this filter was last successfully persisted.
    /// </summary>
    public DateTimeOffset LastSavedAt { get; private set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="InMemoryBloomFilter"/> class.
    /// </summary>
    public InMemoryBloomFilter(
        BloomFilterConfiguration config,
        BloomFilterContext context) {

        this.Configuration = config;
        this._storage = context.Storage;
        this._memoryStreamPool = context.MemoryStreamPool;
        this._logger = context.Logger;
        this._options = context.Options;
        this._timeProvider = context.TimeProvider;

        this._bits = new PooledBitArray(config.SizeInBits);
        this.LastSavedAt = this._timeProvider.GetUtcNow();

        this._logger.LogFilterInitialized(this.Configuration.Name,
                                          config.ExpectedItems,
                                          config.ErrorRate,
                                          config.SizeInBits,
                                          BloomMath.BitsToBytes(config.SizeInBits),
                                          config.HashFunctionCount); 
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public override bool Add(ReadOnlySpan<byte> item) {
        ThrowIfDisposed();

        if(BloomFilterDiagnostics.AddCounter.Enabled) {
            BloomFilterDiagnostics.AddCounter.Add(1, new KeyValuePair<string, object?>(BloomFilterDiagnostics.TagFilterName, this.Name.Value));
        }

        this._rwLock.EnterReadLock();
        try {
            PooledBitArray bits = this._bits;
            BloomHasher.ComputeBaseHashes(item, this.Configuration.HashSeed, out ulong h1, out ulong h2);

            bool atLeastOneSet = false;
            long size = this.Configuration.SizeInBits;
            int k = this.Configuration.HashFunctionCount;
            int i = 0;

            // Vector256 (AVX2): Process 4 hashes in parallel
            if(Vector256.IsHardwareAccelerated && k >= 4) {
                Vector256<ulong> vH1 = Vector256.Create(h1);
                Vector256<ulong> vH2 = Vector256.Create(h2);
                Vector256<ulong> vIndices = Vector256.Create(0UL, 1UL, 2UL, 3UL);
                Vector256<ulong> vStep = Vector256.Create(4UL, 4UL, 4UL, 4UL);

                for(; i <= k - 4; i += 4) {
                    Vector256<ulong> vCombined = vH1 + (vIndices * vH2);

                    for(int j = 0; j < 4; j++) {
                        ulong finalHash = vCombined.GetElement(j);
                        long pos = (long)(((UInt128)finalHash * (ulong)size) >> 64);
                        if(bits.Set(pos)) {
                            atLeastOneSet = true;
                        }
                    }
                    vIndices += vStep;
                }
            }

            // Vector128 (SSE2 / NEON): Process remaining pairs
            if(Vector128.IsHardwareAccelerated && (k - i) >= 2) {
                Vector128<ulong> vH1 = Vector128.Create(h1);
                Vector128<ulong> vH2 = Vector128.Create(h2);
                Vector128<ulong> vIndices = Vector128.Create((ulong)i, (ulong)(i + 1));
                Vector128<ulong> vStep = Vector128.Create(2UL, 2UL);

                for(; i <= k - 2; i += 2) {
                    Vector128<ulong> vCombined = vH1 + (vIndices * vH2);

                    for(int j = 0; j < 2; j++) {
                        ulong finalHash = vCombined.GetElement(j);
                        long pos = (long)(((UInt128)finalHash * (ulong)size) >> 64);
                        if(bits.Set(pos)) {
                            atLeastOneSet = true;
                        }
                    }
                    vIndices += vStep;
                }
            }

            // Scalar loop for remaining hash iterations
            for(; i < k; i++) {
                long pos = BloomHasher.GetBitPosition(h1, h2, i, size);
                if(bits.Set(pos)) {
                    atLeastOneSet = true;
                }
            }

            if(atLeastOneSet) {
                this._isDirty = true;
            }

            return atLeastOneSet;
        }
        finally {
            this._rwLock.ExitReadLock();
        }
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public override bool Contains(ReadOnlySpan<byte> item) {
        ThrowIfDisposed();

        this._rwLock.EnterReadLock();
        bool result;
        try {
            result = InternalContains(item);
        }
        finally {
            this._rwLock.ExitReadLock();
        }

        if(BloomFilterDiagnostics.LookupCounter.Enabled) {
            BloomFilterDiagnostics.LookupCounter.Add(1, new KeyValuePair<string, object?>(BloomFilterDiagnostics.TagFilterName, this.Name.Value));

            if(result && BloomFilterDiagnostics.HitCounter.Enabled) {
                BloomFilterDiagnostics.HitCounter.Add(1, new KeyValuePair<string, object?>(BloomFilterDiagnostics.TagFilterName, this.Name.Value));
            }
        }

        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.AggressiveInlining)]
    private bool InternalContains(ReadOnlySpan<byte> item) {
        PooledBitArray bits = this._bits;
        BloomHasher.ComputeBaseHashes(item, this.Configuration.HashSeed, out ulong h1, out ulong h2);

        long size = this.Configuration.SizeInBits;
        int k = this.Configuration.HashFunctionCount;
        int i = 0;

        // Vector256 (AVX2): Process 4 hashes in parallel
        if(Vector256.IsHardwareAccelerated && k >= 4) {
            Vector256<ulong> vH1 = Vector256.Create(h1);
            Vector256<ulong> vH2 = Vector256.Create(h2);
            Vector256<ulong> vIndices = Vector256.Create(0UL, 1UL, 2UL, 3UL);
            Vector256<ulong> vStep = Vector256.Create(4UL, 4UL, 4UL, 4UL);

            for(; i <= k - 4; i += 4) {
                Vector256<ulong> vCombined = vH1 + (vIndices * vH2);

                for(int j = 0; j < 4; j++) {
                    ulong finalHash = vCombined.GetElement(j);
                    long pos = (long)(((UInt128)finalHash * (ulong)size) >> 64);
                    if(!bits.Get(pos)) {
                        return false;
                    }
                }
                vIndices += vStep;
            }
        }

        // Vector128 (SSE2 / NEON): Process remaining pairs
        if(Vector128.IsHardwareAccelerated && (k - i) >= 2) {
            Vector128<ulong> vH1 = Vector128.Create(h1);
            Vector128<ulong> vH2 = Vector128.Create(h2);
            Vector128<ulong> vIndices = Vector128.Create((ulong)i, (ulong)(i + 1));
            Vector128<ulong> vStep = Vector128.Create(2UL, 2UL);

            for(; i <= k - 2; i += 2) {
                Vector128<ulong> vCombined = vH1 + (vIndices * vH2);

                for(int j = 0; j < 2; j++) {
                    ulong finalHash = vCombined.GetElement(j);
                    long pos = (long)(((UInt128)finalHash * (ulong)size) >> 64);
                    if(!bits.Get(pos)) {
                        return false;
                    }
                }
                vIndices += vStep;
            }
        }

        // Scalar loop for remaining hash iterations
        for(; i < k; i++) {
            long pos = BloomHasher.GetBitPosition(h1, h2, i, size);
            if(!bits.Get(pos)) {
                return false;
            }
        }

        return true;
    }

    /// <inheritdoc/>
    public override async ValueTask SaveAsync(CancellationToken cancellationToken = default) {
        ThrowIfDisposed();

        if(this._storage == null || !this._isDirty) {
            return;
        }

        long startingTimestamp = Stopwatch.GetTimestamp();

        using Activity? activity = BloomFilterDiagnostics.ActivitySource.StartActivity(BloomFilterDiagnostics.ActivitySave);
        activity?.SetTag(BloomFilterDiagnostics.TagFilterName, this.Name.Value);
        activity?.SetTag(BloomFilterDiagnostics.TagSizeInBits, this.Configuration.SizeInBits);

        using(await this._ioLock.LockAsync(cancellationToken).ConfigureAwait(false)) {
            this._logger.LogSaveStarted(this.Configuration.Name);

            try {
                using PooledObject<MemoryStream> pooledStream = this._memoryStreamPool.Lease();
                MemoryStream snapshotStream = pooledStream.Item;
                snapshotStream.SetLength(0);

                ulong checksum;
                this._rwLock.EnterReadLock();
                try {
                    checksum = this._bits.CalculateChecksum();
                    BloomFilterHeader.WriteHeader(snapshotStream, checksum, this.Configuration);
                    this._bits.WriteToStream(snapshotStream);
                    this._isDirty = false;
                }
                finally {
                    this._rwLock.ExitReadLock();
                }

                snapshotStream.Position = 0;
                await this._storage.SaveAsync(this.Name, this.Configuration, snapshotStream, cancellationToken).ConfigureAwait(false);

                this.LastSavedAt = this._timeProvider.GetUtcNow();

                activity?.SetTag(BloomFilterDiagnostics.TagChecksum, checksum.ToString("X"));
                activity?.SetTag(BloomFilterDiagnostics.TagBytesWritten, snapshotStream.Length);

                TimeSpan elapsed = Stopwatch.GetElapsedTime(startingTimestamp);
                BloomFilterDiagnostics.SaveDuration.Record(
                    elapsed.TotalMilliseconds,
                    new KeyValuePair<string, object?>(BloomFilterDiagnostics.TagFilterName, this.Name.Value));

                BloomFilterDiagnostics.BytesWrittenCounter.Add(
                    snapshotStream.Length,
                    new KeyValuePair<string, object?>(BloomFilterDiagnostics.TagFilterName, this.Name.Value));

                this._logger.LogSaveSuccess(this.Configuration.Name, checksum, (int)snapshotStream.Length);
            }
            catch(Exception ex) {
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                activity?.AddException(ex);
                this._logger.LogSaveFailed(ex, this.Configuration.Name);
                throw;
            }
        }
    }

    /// <inheritdoc/>
    public override async ValueTask ReloadAsync(CancellationToken cancellationToken = default) {
        if(this._storage == null) {
            return;
        }

        ThrowIfDisposed();

        long startingTimestamp = Stopwatch.GetTimestamp();

        using Activity? activity = BloomFilterDiagnostics.ActivitySource.StartActivity(BloomFilterDiagnostics.ActivityReload);
        activity?.SetTag(BloomFilterDiagnostics.TagFilterName, this.Name.Value);

        using(await this._ioLock.LockAsync(cancellationToken).ConfigureAwait(false)) {
            (BloomFilterConfiguration? Config, Stream DataStream)? loadResult = await this._storage.LoadStreamAsync(this.Name, cancellationToken).ConfigureAwait(false);
            if(loadResult == null) {
                this._logger.LogReloadNotFound(this.Configuration.Name);
                return;
            }

            PooledBitArray? newBits = null;

            try {
                using(Stream stream = loadResult.Value.DataStream) {
                    if(!BloomFilterHeader.TryReadHeader(
                        stream,
                        out ulong expectedChecksum,
                        out long storedSize,
                        out int storedHashCount,
                        out ulong storedFingerprint)) {

                        this._logger.LogInvalidHeaderWarning(this.Configuration.Name);
                        if(this._options.Lifecycle.EnableIntegrityCheck) {
                            throw new DataIntegrityException("Invalid Bloom Filter header data.");
                        }

                        if(stream.CanSeek) {
                            stream.Position = 0;
                        }
                    }
                    else {
                        ulong currentFingerprint = BloomFilterHeader.ComputeFingerprint(this.Configuration);
                        if(storedFingerprint != currentFingerprint) {
                            throw new DataIntegrityException($"Configuration fingerprint mismatch during reload. Disk: {storedFingerprint:X}, Memory: {currentFingerprint:X}");
                        }

                        if(storedSize != this.Configuration.SizeInBits) {
                            throw new DataIntegrityException("Bit array size mismatch during reload.");
                        }
                    }

                    newBits = new PooledBitArray(this.Configuration.SizeInBits);
                    ulong actualChecksum = await newBits.LoadFromStreamAsync(stream, cancellationToken).ConfigureAwait(false);

                    if(this._options.Lifecycle.EnableIntegrityCheck && actualChecksum != expectedChecksum) {
                        throw new DataIntegrityException($"Checksum verification failed during reload. Expected: {expectedChecksum:X}, Actual: {actualChecksum:X}");
                    }

                    this._logger.LogReloadSuccess(this.Configuration.Name, expectedChecksum);
                }

                // Swap bit arrays under WriteLock to ensure no active readers/writers touch oldBits
                this._rwLock.EnterWriteLock();
                PooledBitArray? oldBits = null;
                try {
                    oldBits = this._bits;
                    this._bits = newBits;
                    newBits = null;
                    this._isDirty = false;
                    oldBits?.Dispose();
                }
                finally {
                    this._rwLock.ExitWriteLock();
                }

                TimeSpan elapsed = Stopwatch.GetElapsedTime(startingTimestamp);
                BloomFilterDiagnostics.ReloadDuration.Record(
                    elapsed.TotalMilliseconds,
                    new KeyValuePair<string, object?>(BloomFilterDiagnostics.TagFilterName, this.Name.Value));
            }
            catch(Exception ex) {
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                activity?.AddException(ex);
                this._logger.LogReloadFailed(ex, this.Configuration.Name);
                throw;
            }
            finally {
                newBits?.Dispose();
            }
        }
    }

    /// <inheritdoc/>
    public override long GetPopCount() {
        ThrowIfDisposed();
        this._rwLock.EnterReadLock();
        try {
            return this._bits.GetPopCount();
        }
        finally {
            this._rwLock.ExitReadLock();
        }
    }

    /// <inheritdoc/>
    protected override void DisposeCore() {
        this._rwLock.EnterWriteLock();
        try {
            PooledBitArray? bits = this._bits;
            this._bits = null!;
            bits?.Dispose();
        }
        finally {
            this._rwLock.ExitWriteLock();
            this._rwLock.Dispose();
        }
    }
}