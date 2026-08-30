using Wiaoj.Webhooks.Diagnostics;

namespace Wiaoj.Webhooks.LoopDetection;

/// <summary>
/// Configuration options for webhook execution cycle and hop count loop detection.
/// </summary>
public sealed class LoopDetectionOptions {
    private int _maxHops = 5;
    private string _hopCountHeaderName = WebhookHeaderNames.WebhookHopCount;
    private string _causalChainHeaderName = WebhookHeaderNames.WebhookCausalChain;
    private string _instanceId = WebhookInstanceId.Resolve();

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
    /// Gets or sets the unique identity of the current engine instance used for cycle graph detection.
    /// </summary>
    public string InstanceId {
        get => this._instanceId;
        set {
            Preca.ThrowIfNullOrWhiteSpace(value);
            this._instanceId = value;
        }
    }

    /// <summary>
    /// Gets or sets the behavior executed when a loop or hop limit breach is detected.
    /// Default is <see cref="LoopDetectedBehavior.DropAndLog"/>.
    /// </summary>
    public LoopDetectedBehavior Behavior { get; set; } = LoopDetectedBehavior.DropAndLog;
}
