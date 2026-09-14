using System.Collections.Concurrent;

namespace Wiaoj.WellKnown.Discovery;

/// <summary>
/// Caches fetched metadata documents by URL, for as long as their response allowed and no longer.
/// </summary>
/// <remarks>
/// <para>
/// Shared by every <see cref="OAuthDiscoveryClient"/> resolved from one container, because typed clients are created per
/// use. Concurrent requests for a document that is not cached share one fetch; a failed fetch is not cached, so the next
/// request tries again.
/// </para>
/// <para>
/// The shared fetch does not observe any one caller's cancellation — one caller giving up must not fail the others —
/// but each caller stops waiting when its own token is cancelled. The fetch is bounded by the HttpClient's timeout.
/// </para>
/// </remarks>
internal sealed class DiscoveryDocumentCache(OAuthDiscoveryOptions options, TimeProvider timeProvider) {
    private readonly ConcurrentDictionary<string, Entry> _entries = new(StringComparer.Ordinal);

    public OAuthDiscoveryOptions Options { get; } = options;

    public TimeProvider TimeProvider { get; } = timeProvider;

    internal int Count => this._entries.Count;

    /// <summary>
    /// Returns the cached document for <paramref name="url"/> while it is fresh, or fetches it.
    /// </summary>
    /// <param name="url">The document's URL, the cache key.</param>
    /// <param name="fetch">Fetches the document and says how long it may be cached.</param>
    /// <param name="refreshIfOlderThan">When set, a cached document fetched longer ago than this is fetched again even while fresh.</param>
    /// <param name="cancellationToken">Stops this caller waiting.</param>
    public async Task<T> GetAsync<T>(
        Uri url,
        Func<CancellationToken, Task<(T Document, TimeSpan Freshness)>> fetch,
        TimeSpan? refreshIfOlderThan,
        CancellationToken cancellationToken) where T : class {

        string key = url.AbsoluteUri;

        while(true) {
            DateTimeOffset now = this.TimeProvider.GetUtcNow();

            if(this._entries.TryGetValue(key, out Entry? existing)) {
                if(!existing.Task.IsCompleted) {
                    return (T)(await existing.Task.WaitAsync(cancellationToken).ConfigureAwait(false)).Document;
                }

                if(existing.Task.IsCompletedSuccessfully && existing.Task.Result is var cached && now < cached.ExpiresAt
                   && (refreshIfOlderThan is not { } age || now - cached.FetchedAt < age)) {
                    return (T)cached.Document;
                }

                // Stale, or asked to refresh: replace it, unless another caller already did.
                Entry replacement = this.Start(fetch);
                if(!this._entries.TryUpdate(key, replacement, existing)) {
                    continue;
                }

                return await this.CompleteAsync<T>(key, replacement, cancellationToken).ConfigureAwait(false);
            }

            Entry created = this.Start(fetch);
            if(!this._entries.TryAdd(key, created)) {
                continue;
            }

            this.Evict(except: key);
            return await this.CompleteAsync<T>(key, created, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Forgets every cached document.</summary>
    public void Clear() => this._entries.Clear();

    private Entry Start<T>(Func<CancellationToken, Task<(T Document, TimeSpan Freshness)>> fetch) where T : class {
        return new Entry(this.FetchAsync(fetch));
    }

    private async Task<Fetched> FetchAsync<T>(Func<CancellationToken, Task<(T Document, TimeSpan Freshness)>> fetch) where T : class {
        // Yield first, so the entry is in the dictionary before the fetch can complete and remove it.
        await Task.Yield();

        (T document, TimeSpan freshness) = await fetch(CancellationToken.None).ConfigureAwait(false);
        DateTimeOffset fetchedAt = this.TimeProvider.GetUtcNow();
        TimeSpan lifetime = freshness > this.Options.MaxCacheDuration ? this.Options.MaxCacheDuration : freshness;

        return new Fetched(document, fetchedAt, fetchedAt + lifetime);
    }

    private async Task<T> CompleteAsync<T>(string key, Entry entry, CancellationToken cancellationToken) where T : class {
        try {
            Fetched fetched = await entry.Task.WaitAsync(cancellationToken).ConfigureAwait(false);

            if(fetched.ExpiresAt <= fetched.FetchedAt) {
                // Not cacheable: it served the callers that were waiting, and is forgotten now.
                this._entries.TryRemove(new KeyValuePair<string, Entry>(key, entry));
            }

            return (T)fetched.Document;
        }
        catch when(entry.Task.IsFaulted || entry.Task.IsCanceled) {
            this._entries.TryRemove(new KeyValuePair<string, Entry>(key, entry));
            throw;
        }
    }

    /// <summary>Keeps the cache within its bound: expired documents go first, then the least recently fetched.</summary>
    private void Evict(string except) {
        if(this._entries.Count <= this.Options.MaxCachedDocuments) {
            return;
        }

        DateTimeOffset now = this.TimeProvider.GetUtcNow();
        foreach(KeyValuePair<string, Entry> entry in this._entries) {
            if(entry.Key != except && entry.Value.Task is { IsCompletedSuccessfully: true, Result.ExpiresAt: var expires } && expires <= now) {
                this._entries.TryRemove(entry);
            }
        }

        while(this._entries.Count > this.Options.MaxCachedDocuments) {
            KeyValuePair<string, Entry>? oldest = null;

            foreach(KeyValuePair<string, Entry> entry in this._entries) {
                if(entry.Key == except || !entry.Value.Task.IsCompletedSuccessfully) {
                    continue;
                }

                if(oldest is null || entry.Value.Task.Result.FetchedAt < oldest.Value.Value.Task.Result.FetchedAt) {
                    oldest = entry;
                }
            }

            if(oldest is null) {
                return; // Everything else is still being fetched; those entries remove themselves if not cacheable.
            }

            this._entries.TryRemove(oldest.Value);
        }
    }

    private sealed record Fetched(object Document, DateTimeOffset FetchedAt, DateTimeOffset ExpiresAt);

    private sealed class Entry(Task<Fetched> task) {
        public Task<Fetched> Task { get; } = task;
    }
}
