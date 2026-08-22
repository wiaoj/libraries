namespace Wiaoj.Webhooks.Transports.InMemory;

/// <summary>
/// Options for configuring the in-memory webhook transport and background consumer worker pool.
/// </summary>
public sealed class InMemoryWebhookTransportOptions {
    private int _concurrency = Math.Max(Environment.ProcessorCount * 2, 4);
    private int? _capacity;
    private TimeSpan _drainTimeout = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Gets or sets the number of concurrent worker loops actively consuming jobs from the in-memory channel.
    /// Default is <c>Environment.ProcessorCount * 2</c> (minimum 4).
    /// </summary>
    public int Concurrency {
        get => this._concurrency;
        set {
            Preca.ThrowIfLessThan(value, 1);
            this._concurrency = value;
        }
    }

    /// <summary>
    /// Gets or sets the maximum bounded channel capacity before backpressure is applied.
    /// When <see langword="null"/>, an unbounded channel is used.
    /// </summary>
    public int? Capacity {
        get => this._capacity;
        set {
            if(value.HasValue) {
                Preca.ThrowIfLessThan(value.Value, 1);
            }
            this._capacity = value;
        }
    }

    /// <summary>
    /// Gets or sets the maximum duration to wait for in-flight and buffered jobs to drain during application shutdown.
    /// Default is 5 seconds.
    /// </summary>
    public TimeSpan DrainTimeout {
        get => this._drainTimeout;
        set {
            Preca.ThrowIfNegative(value);
            this._drainTimeout = value;
        }
    }
}
