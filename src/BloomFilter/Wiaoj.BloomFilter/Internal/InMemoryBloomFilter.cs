using Microsoft.Extensions.Logging;
using System.Buffers;
using System.IO.Hashing;
using System.Runtime.Intrinsics;
using System.Text;
using Wiaoj.BloomFilter.Diagnostics;
using Wiaoj.BloomFilter.Extensions;
using Wiaoj.Concurrency;
using Wiaoj.ObjectPool;
using Wiaoj.Primitives;
using Wiaoj.Primitives.Buffers;
using Wiaoj.Serialization;
using DisposeState = Wiaoj.Primitives.DisposeState;

namespace Wiaoj.BloomFilter.Internal;
/// <summary>
/// A high-performance, thread-safe, in-memory implementation of a Bloom Filter
/// with support for persistence, streaming, and pooled memory management.
/// </summary>
internal sealed class InMemoryBloomFilter : IPersistentBloomFilter, IDisposable {
    private volatile bool _isDirty;
    public bool IsDirty => this._isDirty;

    public string Name => this.Configuration.Name;
    public BloomFilterConfiguration Configuration { get; }

    private PooledBitArray _bits;
    private readonly IBloomFilterStorage? _storage;
    private readonly ILogger _logger;
    private readonly BloomFilterOptions _options;
    private readonly IObjectPool<MemoryStream> _memoryStreamPool;
    private readonly ISerializer<InMemorySerializerKey> _serializer;

    // Lock for Storage I/O (Serialization/Deserialization)
    private readonly AsyncLock _ioLock = new();

    // Lock for Memory Access (Add/Contains vs Reload/Swap)
    // Using ReaderWriterLockSlim allows concurrent Add/Contains (ReadLock)
    // while blocking everything during Reload (WriteLock).
    private readonly ReaderWriterLockSlim _memoryLock = new();

    private readonly DisposeState _disposeState = new();
    private readonly TimeProvider _timeProvider;
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

        // Allocate memory from pool
        this._bits = new PooledBitArray(config.SizeInBits);

        this._logger.LogFilterInitialized(this.Name, config.ExpectedItems, config.ErrorRate.Value, config.SizeInBits);

        this._timeProvider = context.TimeProvider;
        this.LastSavedAt = this._timeProvider.GetUtcNow();
        this._serializer = context.Serializer;
    }

    /// <inheritdoc/>
    public bool Add(ReadOnlySpan<byte> item) {
        this._disposeState.ThrowIfDisposingOrDisposed(this.Name);
        this._memoryLock.EnterReadLock();
        try {
            ulong hash64 = XxHash3.HashToUInt64(item);
            // Kirsch-Mitzenmacher tekniği için iki 64 bitlik taban hash
            ulong h1 = hash64;
            ulong h2 = (hash64 >> 32) | (hash64 << 32);

            bool atLeastOneSet = false;
            long size = this.Configuration.SizeInBits;
            int k = this.Configuration.HashFunctionCount;
            int i = 0;

            // SIMD: 64-bit (ulong) üzerinden 2'şerli gruplar
            if(Vector128.IsHardwareAccelerated && k >= 2) {
                Vector128<ulong> vIndices = Vector128.Create(0UL, 1UL);
                Vector128<ulong> vH1 = Vector128.Create(h1);
                Vector128<ulong> vH2 = Vector128.Create(h2);
                Vector128<ulong> vStep = Vector128.Create(2UL, 2UL);

                for(; i <= k - 2; i += 2) {
                    // combinedHash = h1 + (i * h2)
                    Vector128<ulong> vCombined = vH1 + (vIndices * vH2);

                    for(int j = 0; j < 2; j++) {
                        ulong finalHash = vCombined.GetElement(j);
                        // 64-bit Fast Modulo ( UInt128 kullanarak )
                        long pos = (long)(((UInt128)finalHash * (ulong)size) >> 64);
                        if(this._bits.Set(pos)) atLeastOneSet = true;
                    }
                    vIndices += vStep;
                }
            }

            // Kalanlar için Standart 64-bit Döngü
            for(; i < k; i++) {
                ulong combinedHash = h1 + ((ulong)i * h2);
                long pos = (long)(((UInt128)combinedHash * (ulong)size) >> 64);
                if(this._bits.Set(pos)) atLeastOneSet = true;
            }

            if(atLeastOneSet) {
                this._isDirty = true;
            }

            return atLeastOneSet;
        }
        finally { this._memoryLock.ExitReadLock(); }
    }

    /// <inheritdoc/>
    public bool Contains(ReadOnlySpan<byte> item) {
        this._disposeState.ThrowIfDisposingOrDisposed(this.Name);
        this._memoryLock.EnterReadLock();
        try {
            ulong hash64 = XxHash3.HashToUInt64(item);
            ulong h1 = hash64;
            ulong h2 = (hash64 >> 32) | (hash64 << 32);

            long size = this.Configuration.SizeInBits;
            int k = this.Configuration.HashFunctionCount;
            int i = 0;

            if(Vector128.IsHardwareAccelerated && k >= 2) {
                Vector128<ulong> vIndices = Vector128.Create(0UL, 1UL);
                Vector128<ulong> vH1 = Vector128.Create(h1);
                Vector128<ulong> vH2 = Vector128.Create(h2);
                Vector128<ulong> vStep = Vector128.Create(2UL, 2UL);

                for(; i <= k - 2; i += 2) {
                    Vector128<ulong> vCombined = vH1 + (vIndices * vH2);
                    for(int j = 0; j < 2; j++) {
                        ulong finalHash = vCombined.GetElement(j);
                        long pos = (long)(((UInt128)finalHash * (ulong)size) >> 64);
                        if(!this._bits.Get(pos)) return false;
                    }
                    vIndices += vStep;
                }
            }

            for(; i < k; i++) {
                ulong combinedHash = h1 + ((ulong)i * h2);
                long pos = (long)(((UInt128)combinedHash * (ulong)size) >> 64);
                if(!this._bits.Get(pos)) return false;
            }

            return true;
        }
        finally { this._memoryLock.ExitReadLock(); }
    }

    /// <inheritdoc/>  
    public bool Add(ReadOnlySpan<char> item) {
        int maxBytes = Encoding.UTF8.GetMaxByteCount(item.Length);

        // 256 byte'a kadar stackalloc kullan, fazlası için pool'a git
        using ValueBuffer<byte> buffer = new(maxBytes, stackalloc byte[256]);

        int written = Encoding.UTF8.GetBytes(item, buffer.Span);
        return Add(buffer.Span[..written]);
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
        finally { ArrayPool<byte>.Shared.Return(rented); }
    }

    /// <inheritdoc/>
    public async ValueTask SaveAsync(CancellationToken cancellationToken = default) {
        this._disposeState.ThrowIfDisposingOrDisposed(this.Name);

        if(this._storage == null) return;
        if(!this._isDirty) return;


        using(await this._ioLock.LockAsync(cancellationToken)) {
            this._logger.LogSaveStarted(this.Name);

            try {
                using var memoryStreamPool = this._memoryStreamPool.Lease();
                MemoryStream snapshotStream = memoryStreamPool.Item;
                snapshotStream.SetLength(0);

                ulong checksum = 0;

                // 2. Memory Kilidi: RAM'deki veriyi dondur (Snapshot al)
                this._memoryLock.EnterWriteLock();
                try {
                    // a. Checksum hesapla (Anlık durum)
                    checksum = this._bits.CalculateChecksum();
  
                    // b. Header'ı stream'e yaz
                    BloomFilterHeader.WriteHeader(snapshotStream, checksum, this.Configuration, Encoding.UTF8);
                      
                    // c. Veriyi stream'e yaz (Hala kilitli, veri değişemez)
                    await this._bits.WriteToStreamAsync(snapshotStream, cancellationToken);
                    this._isDirty = false;
                }
                finally {
                    this._memoryLock.ExitWriteLock();
                }

                // 3. Disk Yazma (Yavaş işlem kilit dışındadır, Add işlemlerini bloklamaz)
                snapshotStream.Position = 0;
                await this._storage.SaveAsync(this.Name, this.Configuration, snapshotStream, cancellationToken);

                this.LastSavedAt = this._timeProvider.GetUtcNow();
                this._logger.LogSaveSuccess(this.Name, checksum, (int)snapshotStream.Length);
            }
            catch(Exception ex) {
                this._logger.LogSaveFailed(ex, this.Name);
                throw; // Hatayı yutma, yukarı bildir
            }
        }
    }

    /// <inheritdoc/>
    public async ValueTask ReloadAsync(CancellationToken cancellationToken = default) {
        if(this._storage == null) return;
        this._disposeState.ThrowIfDisposingOrDisposed(this.Name);

        // I/O Kilidini al (Aynı anda tek bir reload/save çalışsın)
        using(await this._ioLock.LockAsync(cancellationToken)) {

            var loadResult = await this._storage.LoadStreamAsync(this.Name, cancellationToken);
            if(loadResult == null) {
                this._logger.LogReloadNotFound(this.Name);
                return;
            }

            PooledBitArray? newBits = null;

            try {
                using(Stream stream = loadResult.Value.DataStream) {
                    // 1. Yeni Header Yapısını Oku (Encoding parametresi ile)
                    if(!BloomFilterHeader.TryReadHeader(
                        stream,
                        out ulong expectedChecksum,
                        out long storedSize,
                        out int storedHashCount,
                        out ulong storedFingerprint, // Fingerprint eklendi
                        Encoding.UTF8)) // Encoder eklendi
                    {
                        this._logger.LogInvalidHeaderWarning(this.Name);

                        if(this._options.Lifecycle.EnableIntegrityCheck)
                            throw new DataIntegrityException("Invalid Bloom Filter Header.");

                        // Header okuyamazsak devam etmeye çalışmak riskli,
                        // ama eski mantığa uyarak seek yapıp raw okumayı deneyebilirsin.
                        if(stream.CanSeek) stream.Position = 0;
                    }
                    else {
                        // 2. Fingerprint ve Config Kontrolü
                        // Reload sırasında config değişmişse (Fingerprint tutmuyorsa) yüklemeyi reddetmeliyiz.
                        if(storedFingerprint != this.Configuration.GetFingerprint()) {
                            throw new DataIntegrityException($"Fingerprint mismatch during reload! Disk: {storedFingerprint:X}, Memory: {this.Configuration.GetFingerprint():X}");
                        }

                        if(storedSize != this.Configuration.SizeInBits) {
                            throw new DataIntegrityException("Disk size mismatch during reload!");
                        }
                    }

                    // 3. Geçici (Swap için) yeni bir BitArray oluştur
                    // Bu işlem sadece ~628MB yer ayırır (Buffer kiralama yok)
                    newBits = new PooledBitArray(this.Configuration.SizeInBits);

                    // 4. Doğrudan Stream'den Oku (Zero-Copy / Chunked Read)
                    // LoadFromStreamAsync metodumuz veriyi okurken checksum'ı da hesaplayıp dönüyordu.
                    ulong actualChecksum = await newBits.LoadFromStreamAsync(stream, cancellationToken);

                    // 5. Checksum Doğrulama
                    if(this._options.Lifecycle.EnableIntegrityCheck) {
                        if(actualChecksum != expectedChecksum)
                            throw new DataIntegrityException($"Checksum mismatch!...");
                    }

                    this._logger.LogReloadSuccess(this.Name, expectedChecksum);
                }

                // 6. KRİTİK SWAP (Bellek Değişimi)
                // Okuma ve doğrulama bitti, şimdi canlı veriyi değiştirmek için WriteLock alıyoruz.
                this._memoryLock.EnterWriteLock();
                try {
                    // A. Eski referansı yerel bir değişkene al (Sakla)
                    var oldBits = this._bits;

                    // B. Yeni veriyi asıl field'a ata (Atomik referans değişimi)
                    // Şu andan itibaren 'Contains/Add' metodları yeni bitleri kullanmaya başlar.
                    this._bits = newBits;

                    // C. newBits'in finally bloğunda yanlışlıkla dispose edilmesini engelle
                    // Çünkü sahipliği artık this._bits'e devrettik.
                    newBits = null;

                    // Veri diskten taze geldiği için 'Kirli' değildir
                    this._isDirty = false;

                    // D. Artık kullanılmayan eski belleği temizle.
                    // Bunu burada yapmak güvenlidir çünkü WriteLock içindeyiz, kimse okuyamaz.
                    oldBits?.Dispose();
                }
                finally {
                    this._memoryLock.ExitWriteLock();
                    newBits?.Dispose();
                }
            }
            catch(Exception ex) {
                this._logger.LogError(ex, "Reload failed for '{Name}'", this.Name);
                throw; // Hatayı yukarı fırlat
            }
            finally {
                // Eğer swap gerçekleşmeden hata oluştuysa, oluşturduğumuz geçici newBits'i temizle
                newBits?.Dispose();
            }
        }
    }


    public long GetPopCount() {
        this._disposeState.ThrowIfDisposingOrDisposed(this.Name);

        this._memoryLock.EnterReadLock();
        try {
            return this._bits.GetPopCount();
        }
        finally {
            this._memoryLock.ExitReadLock();
        }
    }

    /// <summary>
    /// Releases all resources used by the Bloom Filter.
    /// </summary>
    public void Dispose() {
        if(this._disposeState.TryBeginDispose()) {
            // Acquire WriteLock to ensure no active operations during disposal
            this._memoryLock.EnterWriteLock();
            try {
                this._bits?.Dispose();
            }
            finally {
                this._memoryLock.ExitWriteLock();
                this._memoryLock.Dispose();
            }
            this._disposeState.SetDisposed();
        }
    }

    /// <summary>
    /// Factory method to create and hydrate a filter from a data stream.
    /// </summary>
    internal static async Task<InMemoryBloomFilter> CreateFromStreamAsync(BloomFilterConfiguration config,
                                                                          Stream sourceStream,
                                                                          BloomFilterContext context,
                                                                          CancellationToken cancellationToken) {

        using(sourceStream) {
            context.Logger.LogHydratingFromStream(config.Name);
            InMemoryBloomFilter filter = new(config, context);

            try {
                if(BloomFilterHeader.TryReadHeader(sourceStream,
                                                   out ulong expectedChecksum,
                                                   out long storedSize,
                                                   out int storedHashCount,
                                                   out ulong storedFingerprint,
                                                    Encoding.UTF8)) {
                    // Fingerprint kontrolü
                    if(storedFingerprint != config.GetFingerprint()) {
                        if(context.Options.Lifecycle.AutoResetOnMismatch) {
                            context.Logger.LogWarning("Fingerprint mismatch...");
                            return new InMemoryBloomFilter(config, context);
                        }
                        throw new DataIntegrityException("Fingerprint mismatch!");
                    }

                    // Size ve HashCount kontrolü (Fingerprint tutuyorsa bunlar da tutar ama double-check)
                    if(storedSize != config.SizeInBits || storedHashCount != config.HashFunctionCount) {
                        throw new DataIntegrityException("Config mismatch despite matching fingerprint!");
                    }
                }

                ulong actual = await filter._bits.LoadFromStreamAsync(sourceStream, cancellationToken);

                if(context.Options.Lifecycle.EnableIntegrityCheck && actual != expectedChecksum)
                    throw new DataIntegrityException("Checksum mismatch!");

                context.Logger.LogHydrationSuccess(config.Name);
                return filter;
            }
            catch {
                filter.Dispose();
                throw;
            }
        }
    }
}