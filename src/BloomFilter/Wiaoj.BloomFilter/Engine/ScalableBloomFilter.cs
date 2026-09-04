using System.Diagnostics;
using System.Numerics;
using System.Text;
using Wiaoj.BloomFilter.Diagnostics;
using Wiaoj.Concurrency;
using Wiaoj.Preconditions;
using Wiaoj.Primitives;
using Wiaoj.Primitives.Buffers;

namespace Wiaoj.BloomFilter.Engine;

/// <summary>
/// A scalable, persistent Bloom Filter that automatically layers new filters when the current active layer reaches a saturation threshold.
/// This implementation allows the filter to grow dynamically while maintaining a target false positive rate.
/// </summary>
internal sealed class ScalableBloomFilter : IPersistentBloomFilter, IDisposable {
    private readonly ReaderWriterLockSlim _lock = new(LockRecursionPolicy.NoRecursion);
    private readonly BloomFilterContext _context;
    private readonly GrowthRate _growthRate;
    private readonly Percentage _saturationThreshold;
    private readonly DisposeState _disposeState = new();

    private IPersistentBloomFilter[] _layers;

    /// <inheritdoc/>
    public FilterName Name => this.Configuration.Name;

    /// <inheritdoc/>
    public BloomFilterConfiguration Configuration { get; }

    /// <summary>
    /// Gets a value indicating whether any of the underlying layers have been modified and require persistence.
    /// </summary>
    public bool IsDirty {
        get {
            IPersistentBloomFilter[] currentLayers = Atomic.Read(ref this._layers);
            for(int i = 0; i < currentLayers.Length; i++) {
                if(currentLayers[i].IsDirty) return true;
            }
            return false;
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ScalableBloomFilter"/> class.
    /// </summary>
    /// <param name="baseConfig">The initial configuration for the first layer.</param>
    /// <param name="context">The shared context containing logging, storage, and factory services.</param>
    /// <param name="growthRate">The multiplier used to increase capacity for each new layer. Defaults to <see cref="GrowthRate.Double"/>.</param>
    /// <param name="saturationThreshold">The fill ratio at which a new layer is triggered. Defaults to 50%.</param>
    public ScalableBloomFilter(
        BloomFilterConfiguration baseConfig,
        BloomFilterContext context,
        GrowthRate growthRate = default,
        Percentage saturationThreshold = default) {

        Preca.ThrowIfNull(baseConfig);
        Preca.ThrowIfNull(context);

        this.Configuration = baseConfig;
        this._context = context;
        this._growthRate = growthRate.Value == 0 ? GrowthRate.Double : growthRate;
        this._saturationThreshold = saturationThreshold.IsZero ? Percentage.Half : saturationThreshold;

        this._layers = [CreateLayer(baseConfig)];
    }

    private long _addCount = 0;

    /// <inheritdoc/>
    public bool Add(ReadOnlySpan<byte> item) {
        this._disposeState.ThrowIfDisposingOrDisposed(this.Name);

        IPersistentBloomFilter[] layers = Atomic.Read(ref this._layers);

        // 1. Search - Check all layers from newest to oldest
        for(int i = layers.Length - 1; i >= 0; i--) {
            if(layers[i].Contains(item)) return false;
        }

        // 2. Add - Only add to the latest (active) layer
        IPersistentBloomFilter activeLayer = layers[^1];
        bool added = activeLayer.Add(item);

        // 3. Scaling Check - Periodically check for saturation based on layer capacity (to avoid over-saturation on smaller filters)
        long checkInterval = Math.Clamp(activeLayer.Configuration.ExpectedItems / 8, 16, 1000);
        if(added && Atomic.Increment(ref this._addCount) % checkInterval == 0) {
            CheckAndScale(activeLayer);
        }

        return added;
    }

    private void CheckAndScale(IPersistentBloomFilter activeLayer) {
        double fillRatio = (double)activeLayer.GetPopCount() / activeLayer.Configuration.SizeInBits;
        if(fillRatio >= this._saturationThreshold.Value) {
            ScaleUp();
        }
    }

    /// <inheritdoc/>
    public bool Contains(ReadOnlySpan<byte> item) {
        IPersistentBloomFilter[] layers = Atomic.Read(ref this._layers);
        // Search from the newest layer backwards (L4, L3, L2...)
        for(int i = layers.Length - 1; i >= 0; i--) {
            if(layers[i].Contains(item)) return true;
        }
        return false;
    }

    private void ScaleUp() {
        this._lock.EnterWriteLock();
        try {
            IPersistentBloomFilter[] currentLayers = Atomic.Read(ref this._layers);
            IPersistentBloomFilter activeLayer = currentLayers[^1];

            Percentage fillRatio = Percentage.FromDouble((double)activeLayer.GetPopCount() / activeLayer.Configuration.SizeInBits);
            if(fillRatio < this._saturationThreshold) return; // Double-check under lock

            using Activity? activity = BloomFilterDiagnostics.ActivitySource.StartActivity(BloomFilterDiagnostics.ActivityScaleUp);
            activity?.SetTag("bloomfilter.name", this.Name.Value);
            activity?.SetTag("bloomfilter.previous_layer_count", currentLayers.Length);
            activity?.SetTag("bloomfilter.saturation_ratio", fillRatio.Value);

            // Increase expected items by the growth rate
            long newExpectedItems = (long)(activeLayer.Configuration.ExpectedItems * this._growthRate.Value);

            BloomFilterConfiguration newConfig = this._context.ConfigFactory.Create(
                FilterName.Parse($"{this.Configuration.Name.Value}_L{currentLayers.Length}"),
                newExpectedItems,
                activeLayer.Configuration.ErrorRate,
                activeLayer.Configuration.HashSeed + (uint)currentLayers.Length
            );

            // Create new layer using the intelligent factory
            IPersistentBloomFilter newLayer = CreateLayer(newConfig);

            var newLayers = new IPersistentBloomFilter[currentLayers.Length + 1];
            Array.Copy(currentLayers, newLayers, currentLayers.Length);
            newLayers[^1] = newLayer;

            Atomic.Write(ref this._layers, newLayers); 
            
            BloomFilterDiagnostics.ScalableLayerSpawnCounter.Add(
                1, 
                new KeyValuePair<string, object?>(BloomFilterDiagnostics.TagFilterName, this.Name.Value));

            activity?.SetTag("bloomfilter.new_layer_capacity", newExpectedItems);
            this._context.Logger.LogScalableLayerSpawned(this.Name, fillRatio.Value, currentLayers.Length, newExpectedItems);
        }
        finally {
            this._lock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Intelligent Layer Factory: Similar to BloomFilterProvider, determines whether to 
    /// create a ShardedBloomFilter or InMemoryBloomFilter based on the calculated size.
    /// </summary>
    private IPersistentBloomFilter CreateLayer(BloomFilterConfiguration config) {
        return this._context.CreateLeafFilter(config);
    }

    /// <inheritdoc/>
    public async ValueTask SaveAsync(CancellationToken cancellationToken = default) {
        this._disposeState.ThrowIfDisposingOrDisposed(this.Name);

        IPersistentBloomFilter[] currentLayers = Atomic.Read(ref this._layers);

        // Only save layers that have been modified (dirty state).
        // Perform I/O operations in parallel.
        IEnumerable<Task> saveTasks = currentLayers
            .Where(l => l.IsDirty)
            .Select(l => l.SaveAsync(cancellationToken).AsTask());

        await Task.WhenAll(saveTasks);
    }

    /// <inheritdoc/>
    public async ValueTask ReloadAsync(CancellationToken cancellationToken = default) {
        this._disposeState.ThrowIfDisposingOrDisposed(this.Name);

        if(this._context.Storage != null) {
            this._lock.EnterWriteLock();
            try {
                List<IPersistentBloomFilter> loadedLayers = [this._layers[0]];
                await this._layers[0].ReloadAsync(cancellationToken).ConfigureAwait(false);

                int layerIndex = 1;
                while(true) {
                    FilterName nextLayerName = FilterName.Parse($"{this.Configuration.Name.Value}_L{layerIndex}");
                    (BloomFilterConfiguration? Config, Stream DataStream)? loadResult = await this._context.Storage.LoadStreamAsync(nextLayerName, cancellationToken).ConfigureAwait(false);
                    if(!loadResult.HasValue) {
                        break;
                    }

                    await loadResult.Value.DataStream.DisposeAsync().ConfigureAwait(false);

                    IPersistentBloomFilter layer;
                    if(layerIndex < this._layers.Length) {
                        layer = this._layers[layerIndex];
                    }
                    else {
                        IPersistentBloomFilter previous = loadedLayers[^1];
                        long newExpectedItems = (long)(previous.Configuration.ExpectedItems * this._growthRate.Value);
                        BloomFilterConfiguration nextConfig = this._context.ConfigFactory.Create(
                            nextLayerName,
                            newExpectedItems,
                            previous.Configuration.ErrorRate,
                            previous.Configuration.HashSeed + (uint)layerIndex
                        );
                        layer = CreateLayer(nextConfig);
                    }

                    await layer.ReloadAsync(cancellationToken).ConfigureAwait(false);
                    loadedLayers.Add(layer);
                    layerIndex++;
                }

                Atomic.Write(ref this._layers, [.. loadedLayers]);
            }
            finally {
                this._lock.ExitWriteLock();
            }
            return;
        }

        IPersistentBloomFilter[] currentLayers = Atomic.Read(ref this._layers);
        IEnumerable<Task> reloadTasks = currentLayers.Select(l => l.ReloadAsync(cancellationToken).AsTask());

        await Task.WhenAll(reloadTasks).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public bool Add(ReadOnlySpan<char> item) {
        int maxBytes = Encoding.UTF8.GetMaxByteCount(item.Length);
        using ValueBuffer<byte> buffer = new(maxBytes, stackalloc byte[256]);
        int written = Encoding.UTF8.GetBytes(item, buffer.Span);
        return Add(buffer.Slice(0, written));
    }

    /// <inheritdoc/>
    public bool Contains(ReadOnlySpan<char> item) {
        int maxBytes = Encoding.UTF8.GetMaxByteCount(item.Length);
        using ValueBuffer<byte> buffer = new(maxBytes, stackalloc byte[256]);
        int written = Encoding.UTF8.GetBytes(item, buffer.Span);
        return Contains(buffer.Slice(0, written));
    }

    /// <inheritdoc/>
    public long GetPopCount() {
        this._lock.EnterReadLock();
        try {
            long total = 0;
            IPersistentBloomFilter[] currentLayers = Atomic.Read(ref this._layers);
            for(int i = 0; i < currentLayers.Length; i++) total += currentLayers[i].GetPopCount();
            return total;
        }
        finally {
            this._lock.ExitReadLock();
        }
    }

    /// <summary>
    /// Releases all resources used by the Scalable Bloom Filter and its underlying layers.
    /// </summary>
    public void Dispose() {
        if(this._disposeState.TryBeginDispose()) {
            this._lock.EnterWriteLock();
            try {
                IPersistentBloomFilter[] currentLayers = Atomic.Read(ref this._layers);
                foreach(IPersistentBloomFilter? layer in currentLayers) {
                    if(layer is IDisposable disposableLayer) {
                        disposableLayer.Dispose();
                    }
                }
            }
            finally {
                this._lock.ExitWriteLock();
                this._lock.Dispose();
            }
            this._disposeState.SetDisposed();
        }
    }
}