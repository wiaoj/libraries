using System.Collections.Concurrent;

namespace Wiaoj.Webhooks.Idempotency;

/// <summary>
/// Thread-safe in-memory implementation of <see cref="IIdempotencyStore"/> with atomic time-to-live (TTL) expiration window evaluation.
/// </summary>
public sealed class InMemoryIdempotencyStore : IIdempotencyStore {
    private readonly ConcurrentDictionary<IdempotencyKey, DateTimeOffset> _entries = new();
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="InMemoryIdempotencyStore"/> class using the system clock.
    /// </summary>
    public InMemoryIdempotencyStore() : this(TimeProvider.System) {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="InMemoryIdempotencyStore"/> class with a custom <see cref="TimeProvider"/>.
    /// </summary>
    /// <param name="timeProvider">The time provider used to calculate time-to-live expiration windows.</param>
    public InMemoryIdempotencyStore(TimeProvider timeProvider) {
        Preca.ThrowIfNull(timeProvider);
        this._timeProvider = timeProvider;
    }

    /// <inheritdoc/>
    public ValueTask<bool> ContainsAsync(IdempotencyKey key, CancellationToken cancellationToken = default) {
        Preca.ThrowIfNullOrWhiteSpace(key.Value);

        DateTimeOffset now = this._timeProvider.GetUtcNow();
        if(this._entries.TryGetValue(key, out DateTimeOffset existingExpiry) && existingExpiry > now) {
            return ValueTask.FromResult(true);
        }

        return ValueTask.FromResult(false);
    }

    /// <inheritdoc/>
    public ValueTask MarkProcessedAsync(IdempotencyKey key, TimeSpan window, CancellationToken cancellationToken = default) {
        Preca.ThrowIfNullOrWhiteSpace(key.Value);
        Preca.ThrowIfNegativeOrZero(window);

        DateTimeOffset expiresAt = this._timeProvider.GetUtcNow().Add(window);
        this._entries[key] = expiresAt;
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask<bool> TryMarkProcessedAsync(IdempotencyKey key, TimeSpan window, CancellationToken cancellationToken = default) {
        Preca.ThrowIfNullOrWhiteSpace(key.Value);
        Preca.ThrowIfNegativeOrZero(window);

        DateTimeOffset now = this._timeProvider.GetUtcNow();
        DateTimeOffset expiresAt = now.Add(window);

        while(!cancellationToken.IsCancellationRequested) {
            if(this._entries.TryGetValue(key, out DateTimeOffset existingExpiry)) {
                if(existingExpiry > now) {
                    return ValueTask.FromResult(false);
                }

                if(this._entries.TryUpdate(key, expiresAt, existingExpiry)) {
                    return ValueTask.FromResult(true);
                }

                continue;
            }

            if(this._entries.TryAdd(key, expiresAt)) {
                return ValueTask.FromResult(true);
            }
        }

        return ValueTask.FromCanceled<bool>(cancellationToken);
    }

    /// <summary>
    /// Sweeps and removes all expired idempotency keys from memory to reclaim resources.
    /// </summary>
    /// <returns>The total number of expired keys removed from the store.</returns>
    public int SweepExpired() {
        DateTimeOffset now = this._timeProvider.GetUtcNow();
        int removedCount = 0;

        foreach(KeyValuePair<IdempotencyKey, DateTimeOffset> kvp in this._entries) {
            if(kvp.Value <= now && this._entries.TryRemove(kvp.Key, out _)) {
                removedCount++;
            }
        }

        return removedCount;
    }
}