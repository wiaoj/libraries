using Microsoft.Extensions.Logging;
using System.Diagnostics;
using Wiaoj.BloomFilter.Diagnostics;
using Wiaoj.Concurrency;
using Wiaoj.Primitives;

namespace Wiaoj.BloomFilter.Engine;

internal sealed class LazyBloomFilterProxy : IPersistentBloomFilter, IDisposable, IAsyncDisposable {
    private readonly AsyncLazy<IPersistentBloomFilter> _lazyFilter;
    private readonly ILogger _logger;
    private readonly DisposeState _disposeState = new();

    public LazyBloomFilterProxy(string name, BloomFilterFactory factory, IBloomFilterRegistry registry, ILoggerFactory loggerFactory) {
        this.Name = name;
        this._logger = loggerFactory.CreateLogger($"Wiaoj.BloomFilter.Proxy.{name}");

        this._lazyFilter = new AsyncLazy<IPersistentBloomFilter>(async (ct) => {
            FilterName filterName = FilterName.Parse(name);
            Stopwatch sw = Stopwatch.StartNew();
            this._logger.LogLazyLoadTriggered(filterName);

            IPersistentBloomFilter filter = await factory.Create(filterName, ct);

            sw.Stop();
            this._logger.LogLazyLoadCompleted(filterName, sw.ElapsedMilliseconds);
            return filter;
        });

        registry.Register(this);
    }

    public async ValueTask EnsureInitializedAsync(CancellationToken ct) {
        this._disposeState.ThrowIfDisposingOrDisposed(this.Name);
        await this._lazyFilter.GetValueAsync(ct);
    }

    private IPersistentBloomFilter InnerFilter {
        get {
            this._disposeState.ThrowIfDisposingOrDisposed(this.Name);
            if(!this._lazyFilter.IsValueCreated) {
                this._logger.LogSyncLazyBlocking(this.Name);
            }
            return this._lazyFilter.GetValueAsync().AsTask().GetAwaiter().GetResult();
        }
    }

    public FilterName Name { get; }
    public BloomFilterConfiguration Configuration => this.InnerFilter.Configuration;
    public bool IsDirty => this._lazyFilter.IsValueCreated && this.InnerFilter.IsDirty;

    public bool Add(ReadOnlySpan<byte> item) {
        return this.InnerFilter.Add(item);
    }

    public bool Contains(ReadOnlySpan<byte> item) {
        return this.InnerFilter.Contains(item);
    }

    public bool Add(ReadOnlySpan<char> item) {
        return this.InnerFilter.Add(item);
    }

    public bool Contains(ReadOnlySpan<char> item) {
        return this.InnerFilter.Contains(item);
    }

    public long GetPopCount() {
        return this.InnerFilter.GetPopCount();
    }

    public ValueTask SaveAsync(CancellationToken ct = default) {
        return this._lazyFilter.IsValueCreated 
            ? this.InnerFilter.SaveAsync(ct) 
            : ValueTask.CompletedTask;
    }

    public ValueTask ReloadAsync(CancellationToken ct = default) {
        return this._lazyFilter.IsValueCreated 
            ? this.InnerFilter.ReloadAsync(ct) 
            : ValueTask.CompletedTask;
    }

    public void Dispose() {
        if(this._disposeState.TryBeginDispose()) {
            try {
                if(this._lazyFilter.IsValueCreated) {
                    IPersistentBloomFilter inner = this._lazyFilter.GetValueAsync().AsTask().GetAwaiter().GetResult();
                    if(inner is IDisposable disposable) {
                        disposable.Dispose();
                    }
                }
            }
            finally {
                this._disposeState.SetDisposed();
            }
            GC.SuppressFinalize(this);
        }
    }

    public async ValueTask DisposeAsync() {
        if(this._disposeState.TryBeginDispose()) {
            try {
                await this._lazyFilter.DisposeAsync().ConfigureAwait(false);
            }
            finally {
                this._disposeState.SetDisposed();
            }
            GC.SuppressFinalize(this);
        }

        await this._disposeState.WaitForDisposedAsync().ConfigureAwait(false);
    }

    internal IPersistentBloomFilter? GetInnerIfCreated() {
        return this._lazyFilter.IsValueCreated ? this._lazyFilter.GetValueAsync().AsTask().GetAwaiter().GetResult() : null;
    }
}