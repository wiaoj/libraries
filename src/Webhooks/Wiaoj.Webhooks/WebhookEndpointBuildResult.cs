namespace Wiaoj.Webhooks;

/// <summary>How building a <see cref="WebhookEndpoint"/> ended.</summary>
public enum WebhookEndpointBuildStatus {
    /// <summary>The endpoint was built.</summary>
    Built,

    /// <summary>The target URL resolves only to addresses outside the network policy.</summary>
    DestinationRefused,

    /// <summary>The target URL's host could not be resolved.</summary>
    DestinationUnresolvable
}

/// <summary>
/// The outcome of <see cref="WebhookEndpointBuilder.TryBuildAsync"/>: the endpoint, or why its target URL cannot be used.
/// </summary>
/// <remarks>
/// A refused or unresolvable URL is an expected outcome of registering a URL someone supplied, so it is returned for the
/// caller to report — for example as a validation error — rather than thrown.
/// </remarks>
public sealed record WebhookEndpointBuildResult {
    private WebhookEndpointBuildResult(WebhookEndpointBuildStatus status, WebhookEndpoint? endpoint, string? error, Exception? resolutionError) {
        this.Status = status;
        this.Endpoint = endpoint;
        this.Error = error;
        this.ResolutionError = resolutionError;
    }

    /// <summary>Gets how building ended.</summary>
    public WebhookEndpointBuildStatus Status { get; }

    /// <summary>Gets whether the endpoint was built.</summary>
    [System.Diagnostics.CodeAnalysis.MemberNotNullWhen(true, nameof(Endpoint))]
    [System.Diagnostics.CodeAnalysis.MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess => this.Status == WebhookEndpointBuildStatus.Built;

    /// <summary>Gets the endpoint, when it was built.</summary>
    public WebhookEndpoint? Endpoint { get; }

    /// <summary>Gets a description of why the target URL cannot be used; null when the endpoint was built.</summary>
    /// <remarks>Names the host, not the addresses it resolved to.</remarks>
    public string? Error { get; }

    /// <summary>Gets the resolver's error, when the status is <see cref="WebhookEndpointBuildStatus.DestinationUnresolvable"/>.</summary>
    public Exception? ResolutionError { get; }

    internal static WebhookEndpointBuildResult Built(WebhookEndpoint endpoint) => new(WebhookEndpointBuildStatus.Built, endpoint, null, null);

    internal static WebhookEndpointBuildResult Refused(string error) => new(WebhookEndpointBuildStatus.DestinationRefused, null, error, null);

    internal static WebhookEndpointBuildResult Unresolvable(string error, Exception? resolutionError) =>
        new(WebhookEndpointBuildStatus.DestinationUnresolvable, null, error, resolutionError);
}