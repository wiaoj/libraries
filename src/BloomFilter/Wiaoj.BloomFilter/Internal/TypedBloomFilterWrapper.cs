using Microsoft.Extensions.Logging;
using System.Diagnostics;
using Wiaoj.BloomFilter.Diagnostics;
using Wiaoj.Concurrency;

namespace Wiaoj.BloomFilter.Internal;
internal sealed class TypedBloomFilterWrapper<TTag> : IBloomFilter<TTag> where TTag : notnull {
    private readonly AsyncLazy<IPersistentBloomFilter> _lazyFilter;
    private readonly ILogger _logger;

    public TypedBloomFilterWrapper(
        IBloomFilterProvider provider,
        string name,
        ILoggerFactory loggerFactory) {

        this._logger = loggerFactory.CreateLogger($"Wiaoj.BloomFilter.Wrapper.{name}");

        this._lazyFilter = new AsyncLazy<IPersistentBloomFilter>(async (ct) => {
            Stopwatch sw = Stopwatch.StartNew();
            this._logger.LogLazyLoadTriggered(name);

            IPersistentBloomFilter filter = await provider.GetAsync(name);

            sw.Stop();
            this._logger.LogLazyLoadCompleted(name, sw.ElapsedMilliseconds);
            return filter;
        });
    }

    private IBloomFilter InnerFilter {
        get {
            if(!this._lazyFilter.IsValueCreated) {
                this._logger.LogSyncLazyBlocking(typeof(TTag).Name);
            }
            return this._lazyFilter.GetValueAsync().AsTask().GetAwaiter().GetResult();
        }
    }

    public string Name => this.InnerFilter.Name;
    public BloomFilterConfiguration Configuration => this.InnerFilter.Configuration;

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
}