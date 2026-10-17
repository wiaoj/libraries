namespace Wiaoj.Webhooks;

/// <summary>
/// The causal history a webhook carries: how many webhook hops led to it, and which services those hops passed through.
/// </summary>
/// <remarks>
/// A receiver captures the causality of an inbound webhook (see <see cref="WebhookCausalityScope"/>); every job published
/// while handling it inherits that causality, so a loop such as A → B → A is visible to the next outbound delivery.
/// </remarks>
public readonly record struct WebhookCausality {
    /// <summary>Gets the causality of a webhook that was not caused by another webhook.</summary>
    public static WebhookCausality None => default;

    /// <summary>Gets the number of webhook hops that led to this one. <c>0</c> when it was not caused by a webhook.</summary>
    public int HopCount { get; }

    /// <summary>
    /// Gets the comma-separated service names the causing webhooks passed through, oldest first,
    /// or <see langword="null"/> when there are none.
    /// </summary>
    public string? CausalChain { get; }

    /// <summary>Gets a value indicating whether this causality carries no history.</summary>
    public bool IsNone => this.HopCount == 0 && this.CausalChain is null;

    /// <summary>
    /// Initializes a new instance of the <see cref="WebhookCausality"/> struct.
    /// </summary>
    /// <param name="hopCount">The number of webhook hops that led to this one.</param>
    /// <param name="causalChain">The comma-separated service names those hops passed through, oldest first.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="hopCount"/> is negative.</exception>
    public WebhookCausality(int hopCount, string? causalChain) {
        Preca.ThrowIfNegative(hopCount);

        this.HopCount = hopCount;
        this.CausalChain = string.IsNullOrWhiteSpace(causalChain) ? null : causalChain.Trim();
    }

    /// <summary>
    /// Determines whether <paramref name="serviceName"/> already appears in <see cref="CausalChain"/>.
    /// Tokens are trimmed and unquoted and compared case-insensitively, so <c>"svc1"</c> never matches <c>"svc10"</c>.
    /// </summary>
    /// <param name="serviceName">The service name to look for.</param>
    /// <returns><see langword="true"/> when the chain contains the service; otherwise <see langword="false"/>.</returns>
    public bool Contains(string serviceName) {
        Preca.ThrowIfNullOrWhiteSpace(serviceName);
        return ChainContains(this.CausalChain, serviceName);
    }

    /// <summary>
    /// Determines whether a comma-separated causal chain contains <paramref name="serviceName"/>.
    /// </summary>
    /// <param name="chain">The comma-separated chain, as carried in the <see cref="WebhookHeaderNames.WebhookCausalChain"/> header.</param>
    /// <param name="serviceName">The service name to look for.</param>
    /// <returns><see langword="true"/> when the chain contains the service; otherwise <see langword="false"/>.</returns>
    public static bool ChainContains(string? chain, string serviceName) {
        Preca.ThrowIfNullOrWhiteSpace(serviceName);

        ReadOnlySpan<char> chainSpan = chain.AsSpan();
        while(!chainSpan.IsEmpty) {
            int commaIndex = chainSpan.IndexOf(',');
            ReadOnlySpan<char> token = commaIndex >= 0 ? chainSpan[..commaIndex] : chainSpan;
            token = token.Trim().Trim('"');

            if(token.Equals(serviceName.AsSpan(), StringComparison.OrdinalIgnoreCase)) {
                return true;
            }

            if(commaIndex < 0) {
                break;
            }

            chainSpan = chainSpan[(commaIndex + 1)..];
        }

        return false;
    }
}
