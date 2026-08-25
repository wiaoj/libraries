using Microsoft.Extensions.Logging;
using Wiaoj.DistributedCounter;
using Wiaoj.Resilience.CircuitBreaker;

namespace Wiaoj.Resilience.Internal;

/// <summary>
/// Distributed circuit breaker store built on top of <see cref="IDistributedCounterFactory"/>.
/// Supports both high-performance in-memory CAS and cluster-wide Redis storage.
/// </summary>
internal sealed class DistributedCircuitBreakerStore : ICircuitBreakerStore {
    private readonly IDistributedCounterFactory _counterFactory;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<DistributedCircuitBreakerStore> _logger;

    // Safety-net TTL for a claimed half-open probe ticket. CanExecuteAsync doesn't receive
    // CircuitBreakerOptions (only RecordFailureAsync does), so this can't be derived from
    // BreakDuration. It only needs to outlive a single downstream call: if the caller who
    // claimed the probe crashes or never reports back via RecordSuccessAsync/RecordFailureAsync,
    // this TTL self-heals the lock so a future caller can claim a fresh probe instead of the
    // circuit staying stuck denying everyone forever.
    private static readonly TimeSpan ProbeClaimTtl = TimeSpan.FromSeconds(30);

    // Returned to callers who lose the race to claim the half-open probe ticket. Short on
    // purpose: it's just a "come back in a moment" hint, not a real break-duration wait.
    private static readonly TimeSpan ProbePendingRetryAfter = TimeSpan.FromMilliseconds(250);

    /// <summary>
    /// Initializes a new instance of the <see cref="DistributedCircuitBreakerStore"/> class.
    /// </summary>
    /// <param name="counterFactory">The distributed counter factory.</param>
    /// <param name="timeProvider">The time provider for timestamp and TTL math.</param>
    /// <param name="logger">The logger instance.</param>
    public DistributedCircuitBreakerStore(
        IDistributedCounterFactory counterFactory,
        TimeProvider timeProvider,
        ILogger<DistributedCircuitBreakerStore> logger) {
        Preca.ThrowIfNull(counterFactory);
        Preca.ThrowIfNull(timeProvider);
        Preca.ThrowIfNull(logger);

        this._counterFactory = counterFactory;
        this._timeProvider = timeProvider;
        this._logger = logger;
    }

    /// <inheritdoc/>
    public async ValueTask<CircuitExecutionDecision> CanExecuteAsync(string key, CancellationToken cancellationToken = default) {
        Preca.ThrowIfNullOrWhiteSpace(key);
        cancellationToken.ThrowIfCancellationRequested();

        IDistributedCounter trippedCounter = this._counterFactory.Create(FormatTrippedKey(key));
        CounterValue trippedVal = await trippedCounter.GetValueAsync(cancellationToken).ConfigureAwait(false);

        if(trippedVal.Value <= 0) {
            return CircuitExecutionDecision.Allowed();
        }

        DateTimeOffset now = this._timeProvider.GetUtcNow();
        DateTimeOffset blockedUntil = new(trippedVal.Value, TimeSpan.Zero);

        if(blockedUntil > now) {
            return CircuitExecutionDecision.Denied(blockedUntil - now);
        }

        // Break duration has elapsed -> the circuit is eligible for a Half-Open trial probe.
        // This must be a *single* trial request, not "whoever asks first happens to get one" -
        // if we just returned HalfOpenProbe() here unconditionally, every concurrent caller
        // that lands in this branch at the same moment would ALL get treated as the probe,
        // hammering a possibly-still-broken downstream with N requests instead of 1.
        //
        // TryIncrementAsync(amount: 1, limit: 1, ...) is an atomic CAS-based claim: only the
        // first caller to reach it succeeds (IsAllowed == true); everyone else is denied until
        // the ticket is released (see RecordSuccessAsync / RecordFailureAsync below) or its
        // safety-net TTL expires.
        IDistributedCounter probeCounter = this._counterFactory.Create(FormatProbeKey(key));
        CounterLimitResult probeClaim = await probeCounter
            .TryIncrementAsync(1, 1, CounterExpiry.From(ProbeClaimTtl), cancellationToken)
            .ConfigureAwait(false);

        if(probeClaim.IsAllowed) {
            return CircuitExecutionDecision.HalfOpenProbe();
        }

        // Someone else already holds the probe ticket and its result is still pending -> treat
        // this caller the same as if the circuit were still fully Open.
        return CircuitExecutionDecision.Denied(ProbePendingRetryAfter);
    }

    /// <inheritdoc/>
    public async ValueTask RecordSuccessAsync(string key, CancellationToken cancellationToken = default) {
        Preca.ThrowIfNullOrWhiteSpace(key);

        IDistributedCounter failuresCounter = this._counterFactory.Create(FormatFailuresKey(key));
        IDistributedCounter trippedCounter = this._counterFactory.Create(FormatTrippedKey(key));
        IDistributedCounter probeCounter = this._counterFactory.Create(FormatProbeKey(key));

        await failuresCounter.ResetAsync(cancellationToken).ConfigureAwait(false);
        await trippedCounter.ResetAsync(cancellationToken).ConfigureAwait(false);

        // Release the probe ticket too: a successful call (whether it was a plain Closed-state
        // call or the Half-Open probe itself) means there's nothing in flight to guard against.
        await probeCounter.ResetAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask RecordFailureAsync(string key, CircuitBreakerOptions options, CancellationToken cancellationToken = default) {
        Preca.ThrowIfNullOrWhiteSpace(key);
        Preca.ThrowIfNull(options);
        options.Validate();

        IDistributedCounter failuresCounter = this._counterFactory.Create(FormatFailuresKey(key));
        IDistributedCounter trippedCounter = this._counterFactory.Create(FormatTrippedKey(key));
        IDistributedCounter probeCounter = this._counterFactory.Create(FormatProbeKey(key));

        // CanExecuteAsync only ever lets a caller through when the circuit is Closed or
        // Half-Open. If a tripped marker is already present at this point, this failure can
        // only have come from a Half-Open trial probe that just failed. A failed probe must
        // re-trip the circuit IMMEDIATELY - the whole point of Half-Open is "one trial, and
        // its outcome decides everything" - it must NOT wait for the normal consecutive-failure
        // threshold to be reached again (which, for FailureThreshold > 1, would otherwise let
        // several more requests slip through a circuit that just proved itself still broken).
        CounterValue existingTrip = await trippedCounter.GetValueAsync(cancellationToken).ConfigureAwait(false);
        if(existingTrip.Value > 0) {
            await TripAsync(key, options, failuresCounter, trippedCounter, probeCounter, cancellationToken).ConfigureAwait(false);

            this._logger.LogWarning(
                "Circuit breaker RE-TRIPPED to OPEN for key '{Key}' after a failed half-open probe. Breaking for {DurationMs:F0}ms.",
                key, options.BreakDuration.TotalMilliseconds);
            return;
        }

        // Keep failure count alive for 2x break duration sliding window.
        CounterExpiry failureExpiry = CounterExpiry.From(options.BreakDuration * 2);
        CounterValue newFailureCount = await failuresCounter.IncrementAsync(1, failureExpiry, cancellationToken).ConfigureAwait(false);

        if(newFailureCount.Value >= options.FailureThreshold) {
            await TripAsync(key, options, failuresCounter, trippedCounter, probeCounter, cancellationToken).ConfigureAwait(false);

            this._logger.LogWarning("Circuit breaker TRIPPED to OPEN for key '{Key}'. Threshold {Threshold} reached. Breaking for {DurationMs:F0}ms.",
                key, options.FailureThreshold, options.BreakDuration.TotalMilliseconds);
        }
    }

    private async ValueTask TripAsync(
        string key,
        CircuitBreakerOptions options,
        IDistributedCounter failuresCounter,
        IDistributedCounter trippedCounter,
        IDistributedCounter probeCounter,
        CancellationToken cancellationToken) {

        DateTimeOffset blockedUntil = this._timeProvider.GetUtcNow().Add(options.BreakDuration);

        // IMPORTANT: the tripped marker's storage TTL must outlive BreakDuration, not equal it.
        // CanExecuteAsync's Open-vs-HalfOpen decision is made by comparing the stored
        // `blockedUntil` timestamp against "now" - it needs to still be able to READ that
        // timestamp once BreakDuration has elapsed in order to correctly return HalfOpenProbe().
        // If the storage TTL were set to exactly BreakDuration, the underlying counter storage
        // would delete the key at precisely the moment blockedUntil is reached, so
        // GetValueAsync would come back as "0 / not tripped" and CanExecuteAsync would
        // (incorrectly) report Closed/Allowed instead of HalfOpen - skipping the half-open
        // trial phase entirely. 2x BreakDuration gives enough buffer for that read to still
        // succeed; the trip is explicitly cleared anyway via ResetAsync on the next
        // RecordSuccessAsync/RecordFailureAsync, so the extended TTL is purely a read-safety
        // margin, not a behavioral change.
        CounterExpiry tripExpiry = CounterExpiry.From(options.BreakDuration * 2);
        await trippedCounter.SetAsync(blockedUntil.UtcTicks, tripExpiry, cancellationToken).ConfigureAwait(false);

        // A fresh break window has just started: any previously-claimed probe ticket is stale
        // and the consecutive-failure streak restarts clean for the next cycle.
        await probeCounter.ResetAsync(cancellationToken).ConfigureAwait(false);
        await failuresCounter.ResetAsync(cancellationToken).ConfigureAwait(false);
    }

    private static string FormatFailuresKey(string key) {
        return $"wh:cb:fail:{key}";
    }

    private static string FormatTrippedKey(string key) {
        return $"wh:cb:open:{key}";
    }

    private static string FormatProbeKey(string key) {
        return $"wh:cb:probe:{key}";
    }
}