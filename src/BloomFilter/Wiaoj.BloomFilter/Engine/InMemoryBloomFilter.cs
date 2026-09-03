using Microsoft.Extensions.Logging;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Text;
using Wiaoj.BloomFilter.Diagnostics;
using Wiaoj.Concurrency;
using Wiaoj.ObjectPool;
using Wiaoj.Primitives;

namespace Wiaoj.BloomFilter;

/// <summary>
/// In-memory implementation of a persistent Bloom Filter.
/// Uses SIMD vectorization for hash evaluations and atomic operations for concurrency.
/// </summary>
internal sealed class InMemoryBloomFilter : IPersistentBloomFilter, IDisposable {
    private volatile bool _isDirty;
    private PooledBitArray _bits;

    private readonly IBloomFilterStorage? _storage;
    private readonly ILogger _logger;
    private readonly BloomFilterOptions _options;
    private readonly IObjectPool<MemoryStream> _memoryStreamPool;
    private readonly TimeProvider _timeProvider;
    private readonly AsyncLock _ioLock = new();
    private readonly DisposeState _disposeState = new();

    /// <inheritdoc/>
    public bool IsDirty => this._isDirty;

    /// <inheritdoc/>
    public string Name => this.Configuration.Name.Value;

    /// <inheritdoc/>
    public BloomFilterConfiguration Configuration { get; }

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

        this._logger.LogFilterInitialized(this.Configuration.Name, config.ExpectedItems, config.ErrorRate, config.SizeInBits);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public bool Add(ReadOnlySpan<byte> item) {
        this._disposeState.ThrowIfDisposingOrDisposed(this.Name);

        PooledBitArray bits = Volatile.Read(ref this._bits);
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

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public bool Contains(ReadOnlySpan<byte> item) {
        this._disposeState.ThrowIfDisposingOrDisposed(this.Name);

        PooledBitArray bits = Volatile.Read(ref this._bits);
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
    public bool Add(ReadOnlySpan<char> item) {
        int maxBytes = Encoding.UTF8.GetMaxByteCount(item.Length);

        if(maxBytes <= 256) {
            Span<byte> buffer = stackalloc byte[maxBytes];
            int written = Encoding.UTF8.GetBytes(item, buffer);
            return Add(buffer[..written]);
        }

        byte[] rented = ArrayPool<byte>.Shared.Rent(maxBytes);
        try {
            int written = Encoding.UTF8.GetBytes(item, rented);
            return Add(rented.AsSpan(0, written));
        }
        finally {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }

    /// <inheritdoc/>
    public bool Contains(ReadOnlySpan<char> item) {
        int maxBytes = Encoding.UTF8.GetMaxByteCount(item.Length);

        if(maxBytes <= 256) {
            Span<byte> buffer = stackalloc byte[maxBytes];
            int written = Encoding.UTF8.GetBytes(item, buffer);
            return Contains(buffer[..written]);
        }

        byte[] rented = ArrayPool<byte>.Shared.Rent(maxBytes);
        try {
            int written = Encoding.UTF8.GetBytes(item, rented);
            return Contains(rented.AsSpan(0, written));
        }
        finally {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }

    /// <inheritdoc/>
    public async ValueTask SaveAsync(CancellationToken cancellationToken = default) {
        this._disposeState.ThrowIfDisposingOrDisposed(this.Name);

        if(this._storage == null || !this._isDirty) {
            return;
        }

        using(await this._ioLock.LockAsync(cancellationToken).ConfigureAwait(false)) {
            this._logger.LogSaveStarted(this.Configuration.Name);

            try {
                using PooledObject<MemoryStream> pooledStream = this._memoryStreamPool.Lease();
                MemoryStream snapshotStream = pooledStream.Item;
                snapshotStream.SetLength(0);

                PooledBitArray bits = Volatile.Read(ref this._bits);
                ulong checksum = bits.CalculateChecksum();

                BloomFilterHeader.WriteHeader(snapshotStream, checksum, this.Configuration, Encoding.UTF8);
                await bits.WriteToStreamAsync(snapshotStream, cancellationToken).ConfigureAwait(false);
                this._isDirty = false;

                snapshotStream.Position = 0;
                await this._storage.SaveAsync(this.Name, this.Configuration, snapshotStream, cancellationToken).ConfigureAwait(false);

                this.LastSavedAt = this._timeProvider.GetUtcNow();
                this._logger.LogSaveSuccess(this.Configuration.Name, checksum, (int)snapshotStream.Length);
            }
            catch(Exception ex) {
                this._logger.LogSaveFailed(ex, this.Configuration.Name);
                throw;
            }
        }
    }

    /// <inheritdoc/>
    public async ValueTask ReloadAsync(CancellationToken cancellationToken = default) {
        if(this._storage == null) {
            return;
        }

        this._disposeState.ThrowIfDisposingOrDisposed(this.Name);

        using(await this._ioLock.LockAsync(cancellationToken).ConfigureAwait(false)) {
            var loadResult = await this._storage.LoadStreamAsync(this.Name, cancellationToken).ConfigureAwait(false);
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
                        out ulong storedFingerprint,
                        Encoding.UTF8)) {

                        this._logger.LogInvalidHeaderWarning(this.Configuration.Name);
                        if(this._options.Lifecycle.EnableIntegrityCheck) {
                            throw new DataIntegrityException("Invalid Bloom Filter header data.");
                        }

                        if(stream.CanSeek) {
                            stream.Position = 0;
                        }
                    }
                    else {
                        if(storedFingerprint != this.Configuration.GetFingerprint()) {
                            throw new DataIntegrityException($"Configuration fingerprint mismatch during reload. Disk: {storedFingerprint:X}, Memory: {this.Configuration.GetFingerprint():X}");
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

                // Atomic reference swap
                PooledBitArray oldBits = Interlocked.Exchange(ref this._bits, newBits);
                newBits = null;
                this._isDirty = false;

                oldBits?.Dispose();
            }
            catch(Exception ex) {
                this._logger.LogError(ex, "Failed to reload Bloom Filter '{Name}'.", this.Name);
                throw;
            }
            finally {
                newBits?.Dispose();
            }
        }
    }

    /// <inheritdoc/>
    public long GetPopCount() {
        this._disposeState.ThrowIfDisposingOrDisposed(this.Name);
        PooledBitArray bits = Volatile.Read(ref this._bits);
        return bits.GetPopCount();
    }

    /// <inheritdoc/>
    public void Dispose() {
        if(this._disposeState.TryBeginDispose()) {
            PooledBitArray? bits = Interlocked.Exchange(ref this._bits, null!);
            bits?.Dispose();
            this._disposeState.SetDisposed();
        }
    }
}