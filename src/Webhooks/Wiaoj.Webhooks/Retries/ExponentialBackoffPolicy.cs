using Wiaoj.Extensions;

namespace Wiaoj.Webhooks.Retries;

/// <summary>
/// Implements an exponential backoff retry strategy with optional full-jitter desynchronization and HTTP transient error filtering.
/// </summary>
public sealed class ExponentialBackoffPolicy : IWebhookRetryPolicy {
    private readonly ExponentialBackoffOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExponentialBackoffPolicy"/> class with default options.
    /// </summary>
    public ExponentialBackoffPolicy() : this(new ExponentialBackoffOptions()) {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ExponentialBackoffPolicy"/> class with specified options.
    /// </summary>
    /// <param name="options">The exponential backoff options.</param>
    public ExponentialBackoffPolicy(ExponentialBackoffOptions options) {
        Preca.ThrowIfNull(options);
        options.Validate();
        this._options = options;
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
        if(completedAttempts >= this._options.MaxAttempts) {
            return false;
        }

        double factor = Math.Pow(this._options.Multiplier, completedAttempts - 1);
        double calculatedDelayMs = this._options.InitialDelay.TotalMilliseconds * factor;
        double cappedDelayMs = Math.Min(calculatedDelayMs, this._options.MaxDelay.TotalMilliseconds);
        TimeSpan baseDelay = TimeSpan.FromMilliseconds(cappedDelayMs);

        nextDelay = this._options.Jitter.HasValue && !this._options.Jitter.Value.IsZero
            ? baseDelay.WithJitter(this._options.Jitter.Value)
            : baseDelay;

        return true;
    }
}