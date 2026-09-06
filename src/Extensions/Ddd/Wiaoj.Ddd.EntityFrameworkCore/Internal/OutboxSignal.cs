namespace Wiaoj.Ddd.EntityFrameworkCore.Internal;

/// <summary>
/// Wakes the outbox processor as soon as a transaction commits rows, instead of leaving it to discover them
/// on its next poll.
/// </summary>
/// <remarks>
/// <para>
/// Polling alone puts a full <c>PollingInterval</c> between a commit and the handler running — ten seconds by
/// default, for work the process could start immediately.
/// </para>
/// <para>
/// This carries no messages, only the fact that there are some. The rows are claimed from the database like
/// any others, so a signal that is missed (a crash between commit and pulse) costs latency and nothing else:
/// the next poll finds the rows. Handing the messages themselves to the processor in memory would instead
/// require them to be born locked, which is what makes a crashed instance's rows wait out a lock expiry.
/// </para>
/// </remarks>
internal sealed class OutboxSignal<TContext> : IDisposable {
    private readonly SemaphoreSlim _gate = new(initialCount: 0, maxCount: 1);

    /// <summary>Signals that rows are waiting. Repeated pulses before a wait collapse into one.</summary>
    public void Pulse() {
        try {
            this._gate.Release();
        }
        catch(SemaphoreFullException) {
            // Already signalled and not yet consumed; one wake-up is all a batch needs.
        }
        catch(ObjectDisposedException) {
            // Shutting down.
        }
    }

    /// <summary>
    /// Waits for a pulse or for <paramref name="timeout"/> to elapse, whichever comes first.
    /// </summary>
    /// <returns><see langword="true"/> when woken by a pulse; <see langword="false"/> on timeout.</returns>
    public Task<bool> WaitAsync(TimeSpan timeout, CancellationToken cancellationToken) {
        return this._gate.WaitAsync(timeout, cancellationToken);
    }

    public void Dispose() {
        this._gate.Dispose();
    }
}
