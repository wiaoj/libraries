namespace Wiaoj.Webhooks;

/// <summary>
/// Categorizes non-transient, permanent delivery failures in a transport-agnostic manner
/// for auditing, metrics collection, and administrative dashboards.
/// </summary>
public enum PermanentFailureReason {
    /// <summary>
    /// Unspecified permanent failure.
    /// </summary>
    General = 0,

    /// <summary>
    /// The destination target permanently rejected the delivery request (e.g., HTTP 401/403/404 or gRPC PermissionDenied).
    /// </summary>
    DestinationRejected = 1,

    /// <summary>
    /// The destination endpoint identifier could not be resolved in the store or directory.
    /// </summary>
    EndpointNotFound = 2,

    /// <summary>
    /// The target endpoint is explicitly disabled or suspended by administrative policy.
    /// </summary>
    EndpointDisabled = 3,

    /// <summary>
    /// The target destination URL, network address, or URI scheme is invalid or malformed.
    /// </summary>
    InvalidDestination = 4,

    /// <summary>
    /// The serialized event payload exceeds maximum size limits allowed by the destination or transport.
    /// </summary>
    PayloadTooLarge = 5,

    /// <summary>
    /// Cryptographic signing, TLS handshake, or outbound authorization failed.
    /// </summary>
    SecurityValidationFailed = 6
}