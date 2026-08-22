namespace Wiaoj.Webhooks.Retries;

/// <summary>
/// Implements a linear backoff retry strategy where the delay increases incrementally by a constant step on each attempt.
/// </summary>
public sealed class LinearBackoffPolicy : IWebhookRetryPolicy {
    private readonly int _maxAttempts;
    private readonly TimeSpan _initialDelay;
    private readonly TimeSpan _step;
    private readonly TimeSpan _maxDelay;

    /// <summary>
    /// Initializes a new instance of the <see cref="LinearBackoffPolicy"/> class with default settings.
    /// </summary>
    public LinearBackoffPolicy() : this(5, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2), TimeSpan.FromMinutes(10)) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="LinearBackoffPolicy"/> class with specified parameters.
    /// </summary>
    /// <param name="maxAttempts">The maximum total number of delivery attempts.</param>
    /// <param name="initialDelay">The delay before the first retry.</param>
    /// <param name="step">The additional duration added to each subsequent retry delay.</param>
    /// <param name="maxDelay">The maximum delay cap.</param>
    public LinearBackoffPolicy(int maxAttempts, TimeSpan initialDelay, TimeSpan step, TimeSpan maxDelay) {
        Preca.ThrowIfLessThan(maxAttempts, 1);
        if(initialDelay < TimeSpan.Zero) {
            throw new ArgumentOutOfRangeException(nameof(initialDelay), "Initial delay cannot be negative.");
        }
        if(step < TimeSpan.Zero) {
            throw new ArgumentOutOfRangeException(nameof(step), "Step cannot be negative.");
        }
        if(maxDelay < initialDelay) {
            throw new ArgumentOutOfRangeException(nameof(maxDelay), "Max delay cannot be less than initial delay.");
        }

        this._maxAttempts = maxAttempts;
        this._initialDelay = initialDelay;
        this._step = step;
        this._maxDelay = maxDelay;
    }

    /// <inheritdoc/>
    public bool ShouldRetry(WebhookDeliveryContext context, WebhookDeliveryResult lastResult, out TimeSpan nextDelay) {
        Preca.ThrowIfNull(context);
        Preca.ThrowIfNull(lastResult);

        nextDelay = TimeSpan.Zero;

        if(lastResult is not WebhookDeliveryResult.TransientFailure) {
            return false;
        }

        int completedAttempts = context.AttemptHistory.Count + 1;
        if(completedAttempts >= this._maxAttempts) {
            return false;
        }

        double calculatedMs = this._initialDelay.TotalMilliseconds + ((completedAttempts - 1) * this._step.TotalMilliseconds);
        double cappedMs = Math.Min(calculatedMs, this._maxDelay.TotalMilliseconds);

        nextDelay = TimeSpan.FromMilliseconds(cappedMs);
        return true;
    }
}