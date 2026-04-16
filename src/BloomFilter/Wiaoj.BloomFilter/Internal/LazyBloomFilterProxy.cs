using Microsoft.Extensions.Logging;
using System.Diagnostics;
using Wiaoj.BloomFilter.Diagnostics;
using Wiaoj.Concurrency;

namespace Wiaoj.BloomFilter.Internal;

internal sealed class LazyBloomFilterProxy : IPersistentBloomFilter, IDisposable {
    private readonly AsyncLazy<IPersistentBloomFilter> _lazyFilter;
    private readonly ILogger _logger;
    private readonly string _name;

    public LazyBloomFilterProxy(string name, BloomFilterFactory factory, IBloomFilterRegistry registry, ILoggerFactory loggerFactory) {
        this._name = name;
        this._logger = loggerFactory.CreateLogger($"Wiaoj.BloomFilter.Proxy.{name}");

        this._lazyFilter = new AsyncLazy<IPersistentBloomFilter>(async (ct) => {
            Stopwatch sw = Stopwatch.StartNew();
            this._logger.LogLazyLoadTriggered(FilterName.Parse(name));

            var filter = await factory.CreateAndLoadAsync(name, ct);

            sw.Stop();
            this._logger.LogLazyLoadCompleted(FilterName.Parse(name), sw.ElapsedMilliseconds);
            return filter;
        });

        // AutoSave gibi işlemler için kendini Registry'e kaydet
        registry.Register(this);
    }

    // WarmUp Servisinin tetiklemesi için
    public async ValueTask EnsureInitializedAsync(CancellationToken ct) {
        await _lazyFilter.GetValueAsync(ct);
    }

    private IPersistentBloomFilter InnerFilter {
        get {
            if(!this._lazyFilter.IsValueCreated) {
                this._logger.LogSyncLazyBlocking(FilterName.Parse(this._name));
            }
            return this._lazyFilter.GetValueAsync().AsTask().GetAwaiter().GetResult();
        }
    }

    public string Name => this._name;
    public BloomFilterConfiguration Configuration => InnerFilter.Configuration;
    public bool IsDirty => this._lazyFilter.IsValueCreated && InnerFilter.IsDirty;

    public bool Add(ReadOnlySpan<byte> item) => InnerFilter.Add(item);
    public bool Contains(ReadOnlySpan<byte> item) => InnerFilter.Contains(item);
    public bool Add(ReadOnlySpan<char> item) => InnerFilter.Add(item);
    public bool Contains(ReadOnlySpan<char> item) => InnerFilter.Contains(item);
    public long GetPopCount() => InnerFilter.GetPopCount();

    public ValueTask SaveAsync(CancellationToken ct = default) => this._lazyFilter.IsValueCreated ? InnerFilter.SaveAsync(ct) : ValueTask.CompletedTask;
    public ValueTask ReloadAsync(CancellationToken ct = default) => this._lazyFilter.IsValueCreated ? InnerFilter.ReloadAsync(ct) : ValueTask.CompletedTask;

    public void Dispose() {
        if(this._lazyFilter.IsValueCreated && InnerFilter is IDisposable d) {
            d.Dispose();
        }
    }

    // Inspector için asıl nesneye erişim
    internal IPersistentBloomFilter? GetInnerIfCreated() => this._lazyFilter.IsValueCreated ? InnerFilter : null;
}