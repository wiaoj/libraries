using Wiaoj.Webhooks.Diagnostics;

namespace Wiaoj.Webhooks.LoopDetection;

/// <summary>
/// Configuration options for webhook execution cycle and hop count loop detection.
/// </summary>
public sealed class LoopDetectionOptions {
    private int _maxHops = 5;
    private string _hopCountHeaderName = WebhookHeaderNames.WebhookHopCount;
    private string _causalChainHeaderName = WebhookHeaderNames.WebhookCausalChain;
    private string _serviceName = WebhookServiceName.Resolve();

    /// <summary>
    /// Gets or sets a value indicating whether outbound deliveries are checked for loops and stamped with the hop count and
    /// causal chain. Default is <see langword="true"/>. When <see langword="false"/>, the middleware passes every delivery through.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum allowable hop count before a delivery is intercepted as an infinite loop.
    /// Default is <c>5</c>.
    /// </summary>
    public int MaxHops {
        get => this._maxHops;
        set {
            Preca.ThrowIfLessThanOrEqualTo(value, 0);
            this._maxHops = value;
        }
    }

    /// <summary>
    /// Gets or sets the HTTP header name carrying the integer hop counter.
    /// Default is <see cref="WebhookHeaderNames.WebhookHopCount"/>.
    /// </summary>
    public string HopCountHeaderName {
        get => this._hopCountHeaderName;
        set {
            Preca.ThrowIfNullOrWhiteSpace(value);
            this._hopCountHeaderName = value;
        }
    }

    /// <summary>
    /// Gets or sets the HTTP header name carrying the causal origin execution chain.
    /// Default is <see cref="WebhookHeaderNames.WebhookCausalChain"/>.
    /// </summary>
    public string CausalChainHeaderName {
        get => this._causalChainHeaderName;
        set {
            Preca.ThrowIfNullOrWhiteSpace(value);
            this._causalChainHeaderName = value;
        }
    }

    /// <summary>
    /// Gets or sets a value indicating whether to trace and evaluate causal execution graph node identities.
    /// Default is <see langword="true"/>.
    /// </summary>
    public bool TrackCausalChain { get; set; } = true;

    /// <summary>
    /// Gets or sets the name of this service in the causal chain. It must be the same on every instance (pod) of the service,
    /// so a webhook that returns to another instance is still recognised as a loop.
    /// Default is <c>OTEL_SERVICE_NAME</c>, then the entry assembly name (see <see cref="WebhookServiceName.Resolve()"/>).
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when the value is <see langword="null"/>, empty, whitespace, or contains a comma.</exception>
    public string ServiceName {
        get => this._serviceName;
        set {
            Preca.ThrowIfNullOrWhiteSpace(value);
            Preca.ThrowIf(
                value.Contains(','),
                static () => new ArgumentException("The service name cannot contain a comma: the causal chain is comma-separated.", nameof(value)));
            this._serviceName = value.Trim();
        }
    }

    /// <summary>
    /// Gets or sets the behavior executed when a loop or hop limit breach is detected.
    /// Default is <see cref="LoopDetectedBehavior.DropAndLog"/>.
    /// </summary>
    public LoopDetectedBehavior Behavior { get; set; } = LoopDetectedBehavior.DropAndLog;

    internal void CopyTo(LoopDetectionOptions target) {
        target.Enabled = this.Enabled;
        target.MaxHops = this.MaxHops;
        target.HopCountHeaderName = this.HopCountHeaderName;
        target.CausalChainHeaderName = this.CausalChainHeaderName;
        target.TrackCausalChain = this.TrackCausalChain;
        target.ServiceName = this.ServiceName;
        target.Behavior = this.Behavior;
    }
}
