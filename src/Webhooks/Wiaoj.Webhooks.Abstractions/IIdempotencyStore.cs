namespace Wiaoj.Webhooks;

/// <summary>
/// Defines a contract for persisting and evaluating idempotency state with time-to-live (TTL) expiration windows.
/// </summary>
public interface IIdempotencyStore {
    /// <summary>
    /// Checks whether an active, unexpired idempotency key already exists in the store.
    /// </summary>
    /// <param name="key">The idempotency key to check.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> if the key was already successfully processed; otherwise, <see langword="false"/>.</returns>
    ValueTask<bool> ContainsAsync(IdempotencyKey key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks an idempotency key as successfully processed within the specified validity window.
    /// </summary>
    /// <param name="key">The idempotency key to record.</param>
    /// <param name="window">The time window during which duplicate dispatches of this key are rejected.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    ValueTask MarkProcessedAsync(IdempotencyKey key, TimeSpan window, CancellationToken cancellationToken = default);

    /// <summary>
    /// Attempts to atomically record an idempotency key within the specified validity window.
    /// </summary>
    /// <param name="key">The idempotency key to record.</param>
    /// <param name="window">The time window during which duplicate dispatches of this key are rejected.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>
    /// <see langword="true"/> if the key was successfully recorded (first time seen);
    /// <see langword="false"/> if the key already exists and has not yet expired (duplicate detected).
    /// </returns>
    ValueTask<bool> TryMarkProcessedAsync(IdempotencyKey key, TimeSpan window, CancellationToken cancellationToken = default);
}