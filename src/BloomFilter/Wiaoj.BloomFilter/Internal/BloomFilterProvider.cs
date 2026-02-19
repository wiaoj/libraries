using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Numerics;
using Wiaoj.BloomFilter.Diagnostics;
using Wiaoj.Concurrency;
using Wiaoj.ObjectPool;
using Wiaoj.Primitives;
using DisposeState = Wiaoj.Primitives.DisposeState;

namespace Wiaoj.BloomFilter.Internal;

/// <summary>
/// Manages the lifecycle and retrieval of Bloom Filters.
/// Supports async lazy loading, caching, and automatic invalidation on configuration changes.
/// </summary>  
internal class BloomFilterProvider : IBloomFilterProvider, IBloomFilterLifecycleManager, IDisposable, IAsyncDisposable {
    private readonly IBloomFilterStorage? _storage;
    private readonly IObjectPool<MemoryStream> _memoryStreamPool;
    private readonly IOptionsMonitor<BloomFilterOptions> _optionsMonitor;
    private readonly ILogger<BloomFilterProvider> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IEnumerable<IAutoBloomFilterSeeder> _autoSeeders;
    private readonly DisposeState _disposeState = new();
    private readonly TimeProvider _timeProvider;
    private readonly CancellationTokenSource _shutdownCts = new();

    private readonly ConcurrentDictionary<FilterName, AsyncLazy<IPersistentBloomFilter>> _filters = new();

    public BloomFilterProvider(
        IOptionsMonitor<BloomFilterOptions> optionsMonitor,
        ILogger<BloomFilterProvider> logger,
        ILoggerFactory loggerFactory,
        IEnumerable<IAutoBloomFilterSeeder> autoSeeders,
        TimeProvider timeProvider,
        IObjectPool<MemoryStream> memoryStreamPool,
        IBloomFilterStorage? storage = null) {

        this._storage = storage;
        this._logger = logger;
        this._loggerFactory = loggerFactory;
        this._optionsMonitor = optionsMonitor;
        this._autoSeeders = autoSeeders;
        this._timeProvider = timeProvider;
        this._memoryStreamPool = memoryStreamPool;
    }

    public ValueTask<IPersistentBloomFilter> GetAsync(FilterName name) {
        this._disposeState.ThrowIfDisposingOrDisposed(nameof(BloomFilterProvider));

        // 1. Güncel ayarları al
        BloomFilterOptions currentOptions = this._optionsMonitor.CurrentValue;

        // 2. Filtre tanımını bul
        if(!currentOptions.Filters.TryGetValue(name.Value, out FilterDefinition? definition)) {
            InvalidOperationException ex = new($"Filter configuration for '{name}' not found. Check appsettings.json or AddFilter code.");
            this._logger.LogError(ex, "Configuration missing.");
            throw ex;
        }

        // 3. Konfigürasyon nesnesini oluştur
        BloomFilterConfiguration config = new(
            name,
            definition.ExpectedItems,
            Percentage.FromDouble(definition.ErrorRate),
            currentOptions.Performance.GlobalHashSeed
        );

        // 4. Lazy Loading (Sadece ilk çağrıldığında çalışır)
        AsyncLazy<IPersistentBloomFilter> lazy = this._filters.GetOrAdd(
            name,
            key => new AsyncLazy<IPersistentBloomFilter>((callerToken) => CreateAndLoadFilterAsync(key, config, currentOptions, callerToken)));

        return lazy.GetValueAsync();
    }

    private async Task<IPersistentBloomFilter> CreateAndLoadFilterAsync(FilterName filterName,
                                                                        BloomFilterConfiguration config,
                                                                        BloomFilterOptions currentOptions,
                                                                        CancellationToken callerToken) {
        using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(callerToken, this._shutdownCts.Token);
        CancellationToken cancellationToken = linkedCts.Token;

        // --- A. Sharding Hesabı ---
        long totalBytes = (config.SizeInBits + 7) / 8;
        int calculatedShards = 1;

        if(totalBytes > currentOptions.Lifecycle.ShardingThresholdBytes) {
            double ratio = (double)totalBytes / currentOptions.Lifecycle.ShardingThresholdBytes;
            int needed = (int)Math.Ceiling(ratio);
            calculatedShards = (int)BitOperations.RoundUpToPowerOf2((uint)needed);
        }

        BloomFilterConfiguration finalConfig = config.WithShardCount(calculatedShards);
        IPersistentBloomFilter filter;

        BloomFilterContext context = new(
              this._storage,
              this._memoryStreamPool,
              this._loggerFactory.CreateLogger(filterName.Value),
              currentOptions,
              this._timeProvider
          );


        // --- B. Factory (Nesne Oluşturma) ---
        if(calculatedShards > 1) {
            this._logger.LogInformation("Creating Sharded Filter '{Name}' with {Count} shards.", filterName, calculatedShards);
            filter = new ShardedBloomFilter(finalConfig, context);
        }
        else {
            filter = new InMemoryBloomFilter(finalConfig, context);
        }

        // --- C. Yükleme ve Hata Yönetimi ---
        try {
            await filter.ReloadAsync(cancellationToken);
        }
        catch(OperationCanceledException) {
            // Eğer dispose yüzünden iptal edildiyse, yarım kalan nesneyi temizle ve fırlat
            if(filter is IDisposable d) d.Dispose();
            throw;
        }
        catch(Exception ex) {
            this._logger.LogError(ex, "Load failed for '{Name}'. Data might be corrupt. Resetting...", filterName);

            // 1. Bozuk dosyayı sil
            if(this._storage != null) {
                await this._storage.DeleteAsync(filterName.Value, cancellationToken);
            }

            // 2. Otomatik Reseed (Doldurma) Tetikle
            if(currentOptions.Lifecycle.AutoReseed) {
                // KRİTİK NOKTA: "filter" nesnesi şu an elimizde (boş halde).
                // Onu parametre olarak metoda geçiyoruz. Provider'ı tekrar çağırmıyoruz.
                _ = Task.Run(() => TriggerAutoReseedAsync(filter, filterName, CancellationToken.None), CancellationToken.None);
            }
        }

        // Filtre (dolu veya boş) dönülür. 
        // Eğer reseed çalışıyorsa, arkada dolmaya devam eder (Thread-safe).
        return filter;
    }

    /// <summary>
    /// Background job tarafından çağrılan, tüm kirli filtreleri kaydetme metodu.
    /// </summary>
    public async Task SaveAllDirtyAsync(CancellationToken ct) {
        if(this._storage == null) return;

        foreach(KeyValuePair<FilterName, AsyncLazy<IPersistentBloomFilter>> kvp in this._filters) {
            if(kvp.Value.IsValueCreated) {
                try {
                    // Filtre zaten bellekteyse al, yoksa oluşturma.
                    IPersistentBloomFilter filter = await kvp.Value.GetValueAsync(ct);
                    if(filter.IsDirty) {
                        await filter.SaveAsync(ct);
                    }
                }
                catch(Exception ex) {
                    this._logger.LogAutoSaveFailed(ex, kvp.Key);
                }
            }
        }
    }

    public void Dispose() {
        if(this._disposeState.TryBeginDispose()) {
            this._shutdownCts.Cancel();
            foreach(KeyValuePair<FilterName, AsyncLazy<IPersistentBloomFilter>> kvp in this._filters) {
                if(kvp.Value.IsValueCreated) {
                    ValueTask<IPersistentBloomFilter> task = kvp.Value.GetValueAsync();
                    if(task.IsCompletedSuccessfully && task.Result is IDisposable disposableFilter) {
                        disposableFilter.Dispose();
                    }
                }
            }
            this._filters.Clear();
            this._shutdownCts.Dispose();
            this._disposeState.SetDisposed();
            GC.SuppressFinalize(this);
        }
    }

    public async ValueTask DisposeAsync() {
        if(this._disposeState.TryBeginDispose()) {
            try {
                // 1. Devam eden tüm işlemlere iptal sinyali gönder
                this._shutdownCts.Cancel();
            }
            catch(AggregateException) {
                // Bazen Cancel sırasında exception fırlayabilir, yutuyoruz.
            }

            List<Task> cleanupTasks = [];

            foreach(KeyValuePair<FilterName, AsyncLazy<IPersistentBloomFilter>> kvp in this._filters) {
                // Sadece işlem görmüş (veya görmekte olan) filtrelerle ilgileniyoruz
                if(kvp.Value.IsValueCreated) {

                    // Her bir filtreyi temizleme işini bir Task olarak listeye ekle
                    cleanupTasks.Add(Task.Factory.StartNew(async () => {
                        try {
                            // 2. Filtre instance'ını almayı dene (Yükleniyorsa bitmesini/iptalini bekle)
                            // _shutdownCts iptal edildiği için bu işlem muhtemelen hızlıca bitecek veya hata fırlatacaktır.
                            IPersistentBloomFilter filter = await kvp.Value.GetValueAsync();

                            // 3. Filtreyi Dispose et
                            if(filter is IDisposable disposable) {
                                disposable.Dispose();
                            }
                        }
                        catch(Exception) {
                            // Yükleme sırasında iptal edildiyse veya hata aldıysa buraya düşer.
                            // Kapanış sırasında olduğumuz için bu hataları loglayıp geçebiliriz.
                            // Önemli olan deadlock yaratmamak.
                        }
                    }));
                }
            }

            // 4. Tüm temizlik işlemlerinin bitmesini bekle
            await Task.WhenAll(cleanupTasks);

            this._filters.Clear();
            this._shutdownCts.Dispose();
            this._disposeState.SetDisposed();
            GC.SuppressFinalize(this);
        }
    }

    /// <summary>
    /// Otomatik veri doldurma işlemini yönetir.
    /// </summary>
    /// <param name="filter">Doldurulacak hedef filtre.</param>
    /// <param name="name">Filtre ismi (Seeder eşleşmesi için).</param>
    private async Task TriggerAutoReseedAsync(IPersistentBloomFilter filter, FilterName name, CancellationToken cancellationToken) {
        List<IAutoBloomFilterSeeder> matchingSeeders = [.. this._autoSeeders.Where(s => s.FilterName == name)];

        if(matchingSeeders.Count > 0) {
            this._logger.LogInformation("Auto-Reseed started for '{Name}' using {Count} registered seeders (Parallel Execution).", name, matchingSeeders.Count);

            try {
                // 1. Tüm görevleri (Task) oluştur ama bekleme (await etme)
                IEnumerable<Task> seedTasks = matchingSeeders.Select(async seeder => {
                    try {
                        this._logger.LogDebug("Starting seeder: {SeederType}", seeder.GetType().Name);
                        // Bloom Filter thread-safe olduğu için buraya aynı anda girebilirler
                        await seeder.SeedAsync(filter, cancellationToken);
                    }
                    catch(Exception ex) {
                        // Bir seeder patlarsa diğerleri etkilenmesin veya hatayı loglayıp fırlatsın
                        this._logger.LogError(ex, "Seeder failed: {SeederType}", seeder.GetType().Name);
                        throw; // Hepsini durdurmak istiyorsan fırlat
                    }
                });

                // 2. Hepsini aynı anda başlat ve bitmelerini bekle
                // Bu yöntem I/O bound (veritabanı/network) işlerde muazzam hız kazandırır.
                await Task.WhenAll(seedTasks);

                // 3. Hepsi bittiğinde tek seferde diske kaydet
                await filter.SaveAsync(cancellationToken);

                this._logger.LogInformation("Auto-Reseed completed and saved for '{Name}'.", name);
            }
            catch(Exception ex) {
                this._logger.LogError(ex, "Auto-Reseed failed for '{Name}'.", name);
                // Burada Exception handling stratejine göre karar ver (örn: partial success kabul ediliyor mu?)
                throw;
            }
        }
        else {
            this._logger.LogWarning("Auto-Reseed requested for '{Name}' but no matching seeder found.", name);
        }
    }
}