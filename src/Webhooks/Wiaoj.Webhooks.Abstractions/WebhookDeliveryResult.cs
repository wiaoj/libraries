using System.Text.Json.Serialization;

namespace Wiaoj.Webhooks;

/// <summary>
/// Represents the closed polymorphic result hierarchy produced by a webhook delivery attempt.
/// Dictates downstream execution behavior such as persistence updates, retry scheduling, and dead-letter routing.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(Delivered), "delivered")]
[JsonDerivedType(typeof(Deduplicated), "deduplicated")]
[JsonDerivedType(typeof(TransientFailure), "transient_failure")]
[JsonDerivedType(typeof(PermanentFailure), "permanent_failure")]
public abstract record WebhookDeliveryResult {
    /// <summary>
    /// Gets a value indicating whether this delivery outcome is considered logically successful.
    /// </summary>
    public abstract bool IsSuccess { get; }

    /// <summary>
    /// Prevents external inheritance to guarantee a closed discriminated union hierarchy.
    /// </summary>
    private WebhookDeliveryResult() { }

    // ────────────────────────────────────────────────────────────────────────
    // 1. DELIVERED
    // ────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Represents a successful outbound delivery that reached the destination and was accepted.
    /// </summary>
    public sealed record Delivered : WebhookDeliveryResult {
        /// <summary>Gets the transport-level status code returned by the destination target (e.g., HTTP 200, 201, 204).</summary>
        public int StatusCode { get; }

        /// <summary>Gets the optional response body returned by the destination target.</summary>
        public string? ResponseBody { get; }

        /// <inheritdoc/>
        public override bool IsSuccess => true;

        /// <summary>Initializes a new instance of the <see cref="Delivered"/> class with a status code.</summary>
        /// <param name="statusCode">The transport-level status code.</param>
        public Delivered(int statusCode) : this(statusCode, null) { }

        /// <summary>Initializes a new instance of the <see cref="Delivered"/> class with a status code and response body.</summary>
        /// <param name="statusCode">The transport-level status code.</param>
        /// <param name="responseBody">The response payload string received from the destination target.</param>
        public Delivered(int statusCode, string? responseBody) {
            this.StatusCode = statusCode;
            this.ResponseBody = responseBody;
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    // 2. DEDUPLICATED
    // ────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Represents a delivery that was intentionally suppressed because an identical event was already successfully delivered.
    /// </summary>
    public sealed record Deduplicated : WebhookDeliveryResult {
        /// <summary>Gets the deduplication key matched by the deduplication filter.</summary>
        public string DeduplicationKey { get; }

        /// <inheritdoc/>
        public override bool IsSuccess => true;

        /// <summary>Initializes a new instance of the <see cref="Deduplicated"/> class.</summary>
        /// <param name="deduplicationKey">The deduplication key that matched.</param>
        public Deduplicated(string deduplicationKey) {
            Preca.ThrowIfNullOrWhiteSpace(deduplicationKey);
            this.DeduplicationKey = deduplicationKey;
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    // 3. TRANSIENT FAILURE (Retryable with Classified Reason)
    // ────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Represents a temporary delivery failure (e.g., 5xx error, timeout, rate limit, circuit breaker) eligible for retry.
    /// </summary>
    public sealed record TransientFailure : WebhookDeliveryResult {
        /// <summary>Gets the diagnostic error message describing the failure.</summary>
        public string ErrorMessage { get; }

        /// <summary>Gets the optional transport status code (e.g., HTTP 503, 429, 408), if received before failure.</summary>
        public int? StatusCode { get; }

        /// <summary>Gets the optional delay requested before attempting the next retry.</summary>
        public TimeSpan? RetryAfter { get; }

        /// <summary>Gets the classified domain reason for this transient failure.</summary>
        public TransientFailureReason Reason { get; }

        /// <summary>Gets the underlying exception that caused the failure, if any.</summary>
        public Exception? Exception { get; }

        /// <inheritdoc/>
        public override bool IsSuccess => false;

        /// <summary>Initializes a new instance of the <see cref="TransientFailure"/> class with an error message.</summary>
        public TransientFailure(string errorMessage)
            : this(errorMessage, null, null, TransientFailureReason.General, null) { }

        /// <summary>Initializes a new instance of the <see cref="TransientFailure"/> class with an error message and status code.</summary>
        public TransientFailure(string errorMessage, int statusCode)
            : this(errorMessage, statusCode, null, TransientFailureReason.ServerUnavailable, null) { }

        /// <summary>Initializes a new instance of the <see cref="TransientFailure"/> class with a classified reason.</summary>
        public TransientFailure(string errorMessage, TransientFailureReason reason)
            : this(errorMessage, null, null, reason, null) { }

        /// <summary>Initializes a new instance of the <see cref="TransientFailure"/> class with a status code and retry delay.</summary>
        public TransientFailure(string errorMessage, int statusCode, TimeSpan? retryAfter)
            : this(errorMessage, statusCode, retryAfter, TransientFailureReason.ServerUnavailable, null) { }

        /// <summary>Initializes a new instance of the <see cref="TransientFailure"/> class with status code, retry delay, and reason.</summary>
        public TransientFailure(string errorMessage, int? statusCode, TimeSpan? retryAfter, TransientFailureReason reason)
            : this(errorMessage, statusCode, retryAfter, reason, null) { }

        /// <summary>Initializes a new instance of the <see cref="TransientFailure"/> class with all parameters.</summary>
        public TransientFailure(string errorMessage, int? statusCode, TimeSpan? retryAfter, TransientFailureReason reason, Exception? exception) {
            Preca.ThrowIfNullOrWhiteSpace(errorMessage);
            this.ErrorMessage = errorMessage;
            this.StatusCode = statusCode;
            this.RetryAfter = retryAfter;
            this.Reason = reason;
            this.Exception = exception;
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    // 4. PERMANENT FAILURE (Terminal / Non-Retryable)
    // ────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Represents a terminal delivery failure that cannot succeed upon retry and must transition directly to dead-letter.
    /// </summary>
    public sealed record PermanentFailure : WebhookDeliveryResult {
        /// <summary>Gets the diagnostic error message describing the failure.</summary>
        public string ErrorMessage { get; }

        /// <summary>Gets the optional transport status code (e.g., HTTP 400, 401, 404), if received before failure.</summary>
        public int? StatusCode { get; }

        /// <summary>Gets the classified domain category for this permanent failure.</summary>
        public PermanentFailureReason Reason { get; }

        /// <inheritdoc/>
        public override bool IsSuccess => false;

        /// <summary>Initializes a new instance of the <see cref="PermanentFailure"/> class.</summary>
        public PermanentFailure(string errorMessage)
            : this(errorMessage, null, PermanentFailureReason.General) { }

        /// <summary>Initializes a new instance of the <see cref="PermanentFailure"/> class with a status code.</summary>
        public PermanentFailure(string errorMessage, int statusCode)
            : this(errorMessage, statusCode, PermanentFailureReason.General) { }

        /// <summary>Initializes a new instance of the <see cref="PermanentFailure"/> class with a classification reason.</summary>
        public PermanentFailure(string errorMessage, PermanentFailureReason reason)
            : this(errorMessage, null, reason) { }

        /// <summary>Initializes a new instance of the <see cref="PermanentFailure"/> class with all parameters.</summary>
        public PermanentFailure(string errorMessage, int? statusCode, PermanentFailureReason reason) {
            Preca.ThrowIfNullOrWhiteSpace(errorMessage);
            this.ErrorMessage = errorMessage;
            this.StatusCode = statusCode;
            this.Reason = reason;
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    // FACTORY METHODS (Domain-Driven & Intent-Revealing)
    // ────────────────────────────────────────────────────────────────────────

    /// <summary>Creates a successful <see cref="Delivered"/> result.</summary>
    public static WebhookDeliveryResult Success(int statusCode) {
        return new Delivered(statusCode);
    }

    /// <summary>Creates a successful <see cref="Delivered"/> result with response body.</summary>
    public static WebhookDeliveryResult Success(int statusCode, string responseBody) {
        return new Delivered(statusCode, responseBody);
    }

    /// <summary>Creates a <see cref="Deduplicated"/> result representing an event suppressed due to prior delivery.</summary>
    public static WebhookDeliveryResult Duplicate(string deduplicationKey) {
        return new Deduplicated(deduplicationKey);
    }

    /// <summary>Creates a general <see cref="TransientFailure"/> result.</summary>
    public static WebhookDeliveryResult Transient(string errorMessage) {
        return new TransientFailure(errorMessage);
    }

    /// <summary>Creates a general <see cref="TransientFailure"/> result with a status code.</summary>
    public static WebhookDeliveryResult Transient(string errorMessage, int statusCode) {
        return new TransientFailure(errorMessage, statusCode);
    }

    /// <summary>Creates a general <see cref="TransientFailure"/> result with a status code and retry delay.</summary>
    public static WebhookDeliveryResult Transient(string errorMessage, int statusCode, TimeSpan? retryAfter) {
        return new TransientFailure(errorMessage, statusCode, retryAfter);
    }

    /// <summary>Creates a <see cref="TransientFailure"/> result specifically caused by an open circuit breaker.</summary>
    public static WebhookDeliveryResult CircuitBroken(string endpointId, TimeSpan retryAfter) {
        return new TransientFailure($"Circuit breaker is OPEN for endpoint '{endpointId}'.", 503, retryAfter, TransientFailureReason.CircuitBreakerOpen);
    }

    /// <summary>Creates a <see cref="TransientFailure"/> result specifically caused by rate limiting throttling.</summary>
    public static WebhookDeliveryResult RateLimited(string endpointId, TimeSpan retryAfter) {
        return new TransientFailure($"Rate limit exceeded for endpoint '{endpointId}'.", 429, retryAfter, TransientFailureReason.RateLimitThrottled);
    }

    /// <summary>Creates a <see cref="TransientFailure"/> result caused by an outbound request timeout.</summary>
    public static WebhookDeliveryResult Timeout(string errorMessage) {
        return new TransientFailure(errorMessage, 408, null, TransientFailureReason.Timeout);
    }

    /// <summary>Creates a <see cref="TransientFailure"/> result caused by a network socket or DNS failure.</summary>
    public static WebhookDeliveryResult NetworkFailure(string errorMessage, Exception? exception) {
        return new TransientFailure(errorMessage, null, null, TransientFailureReason.NetworkGlitch, exception);
    }

    /// <summary>Creates a <see cref="TransientFailure"/> result caused by a network exception with an optional status code.</summary>
    public static WebhookDeliveryResult NetworkFailure(string errorMessage, int? statusCode, Exception? exception) {
        return new TransientFailure(
            errorMessage,
            statusCode,
            null,
            statusCode.HasValue ? TransientFailureReason.ServerUnavailable : TransientFailureReason.NetworkGlitch,
            exception);
    }

    /// <summary>Creates a <see cref="PermanentFailure"/> result.</summary>
    public static WebhookDeliveryResult Permanent(string errorMessage) {
        return new PermanentFailure(errorMessage);
    }

    /// <summary>Creates a <see cref="PermanentFailure"/> result with a status code.</summary>
    public static WebhookDeliveryResult Permanent(string errorMessage, int statusCode) {
        return new PermanentFailure(errorMessage, statusCode);
    }

    /// <summary>Creates a <see cref="PermanentFailure"/> result with a classification reason.</summary>
    public static WebhookDeliveryResult Permanent(string errorMessage, PermanentFailureReason reason) {
        return new PermanentFailure(errorMessage, reason);
    }

    /// <summary>Creates a <see cref="PermanentFailure"/> result with a status code and classification reason.</summary>
    public static WebhookDeliveryResult Permanent(string errorMessage, int? statusCode, PermanentFailureReason reason) {
        return new PermanentFailure(errorMessage, statusCode, reason);
    }

    /// <summary>Creates a <see cref="PermanentFailure"/> result specifically caused by an intercepted webhook execution loop.</summary>
    public static WebhookDeliveryResult LoopDetected(string errorMessage) {
        return new PermanentFailure(errorMessage, PermanentFailureReason.LoopDetected);
    }
}