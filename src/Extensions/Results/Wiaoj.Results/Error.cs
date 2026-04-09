namespace Wiaoj.Results;
/// <summary>
/// Represents a rich, structured, immutable error.
/// </summary>
public readonly record struct Error {

    /// <summary>Machine-readable code, e.g. <c>"User.NotFound"</c>.</summary>
    public string Code { get; }

    /// <summary>Human-readable description.</summary>
    public string Description { get; }

    /// <summary>Category of the error — see <see cref="ErrorType"/> for built-ins or define custom types.</summary>
    public ErrorType Type { get; }

    /// <summary>Optional contextual metadata. <c>null</c> when no metadata has been attached.</summary>
    public IReadOnlyDictionary<string, object?>? Metadata { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="Error"/> record.
    /// </summary>
    /// <param name="code">Machine-readable code, e.g. <c>"User.NotFound"</c>.</param>
    /// <param name="description">Human-readable description.</param>
    /// <param name="type">Category of the error — see <see cref="ErrorType"/> for built-ins or custom types.</param>
    /// <param name="metadata">Optional contextual metadata. <c>null</c> when no metadata has been attached.</param>
    public Error(string code, string description, ErrorType type, IReadOnlyDictionary<string, object?>? metadata) {
        this.Code = code;
        this.Description = description;
        this.Type = type;
        this.Metadata = metadata;
    }

    // ── Built-in factory methods ──────────────────────────────────────────────

    /// <summary>A general failure error.</summary>
    public static Error Failure(
        string code = "General.Failure",
        string description = "A failure has occurred.") {
        return new(code, description, ErrorType.Failure, null);
    }

    /// <summary>An unexpected system error. Use for unhandled exceptions or system faults.</summary>
    public static Error Unexpected(
        string code = "General.Unexpected",
        string description = "An unexpected error occurred.") {
        return new(code, description, ErrorType.Unexpected, null);
    }

    /// <summary>A validation error, e.g., invalid input format.</summary>
    public static Error Validation(string code, string description) {
        return new(code, description, ErrorType.Validation, null);
    }

    /// <summary>A not-found error.</summary>
    public static Error NotFound(
        string code = "Resource.NotFound",
        string description = "Resource not found.") {
        return new(code, description, ErrorType.NotFound, null);
    }

    /// <summary>A not-found error with a specific resource name and identifier.</summary>
    public static Error NotFound(string resourceName, object id) {
        return new($"{resourceName}.NotFound",
                                                                                $"{resourceName} with id '{id}' was not found.",
                                                                                ErrorType.NotFound,
                                                                                null);
    }

    /// <summary>A conflict error, e.g., duplicate unique key (HTTP 409).</summary>
    public static Error Conflict(
        string code = "Resource.Conflict",
        string description = "A conflict has occurred.") {
        return new(code, description, ErrorType.Conflict, null);
    }

    /// <summary>An unauthorized error — authentication required (HTTP 401).</summary>
    public static Error Unauthorized(
        string code = "Auth.Unauthorized",
        string description = "Unauthorized access.") {
        return new(code, description, ErrorType.Unauthorized, null);
    }

    /// <summary>A forbidden error — authenticated but lacks permission (HTTP 403).</summary>
    public static Error Forbidden(
        string code = "Auth.Forbidden",
        string description = "Access forbidden.") {
        return new(code, description, ErrorType.Forbidden, null);
    }

    /// <summary>
    /// Creates a rate limit exceeded error (HTTP 429).
    /// Use when a caller has sent too many requests in a given time window.
    /// </summary>
    public static Error RateLimitExceeded(
        string code = "RateLimit.Exceeded",
        string description = "Too many requests. Please try again later.") {
        return new(code, description, ErrorType.RateLimit, null);
    }

    /// <summary>
    /// Creates a timeout error (HTTP 408 / 504).
    /// Use when an operation did not complete within the allowed time.
    /// </summary>
    public static Error Timeout(
        string code = "Request.Timeout",
        string description = "The operation timed out.") {
        return new(code, description, ErrorType.Timeout, null);
    }

    /// <summary>
    /// Creates a service unavailable error (HTTP 503).
    /// Use when a downstream dependency is temporarily unreachable.
    /// </summary>
    public static Error ServiceUnavailable(
        string code = "Service.Unavailable",
        string description = "The service is temporarily unavailable.") {
        return new(code, description, ErrorType.Unavailable, null);
    }

    /// <summary>
    /// Creates a gone error (HTTP 410).
    /// Use when a resource has been permanently removed.
    /// Unlike <see cref="NotFound(string, string)"/>, Gone signals the resource
    /// will never return.
    /// </summary>
    public static Error Gone(
        string code = "Resource.Gone",
        string description = "The resource has been permanently removed.") {
        return new(code, description, ErrorType.Gone, null);
    }

    /// <summary>
    /// Creates an unprocessable entity error (HTTP 422).
    /// Use for domain rule violations where the request is syntactically valid
    /// but semantically incorrect.
    /// Unlike <see cref="Validation(string, string)"/> which covers format/schema
    /// errors, this covers business rule violations.
    /// </summary>
    public static Error UnprocessableEntity(string code, string description) {
        return new(code, description, ErrorType.UnprocessableEntity, null);
    }

    /// <summary>
    /// Converts an <see cref="Exception"/> to an <see cref="Error"/>.
    /// <para>
    /// This is the default exception handler used by
    /// <see cref="Result.Try{T}(Func{T}, Func{Exception, Error}?)"/> and
    /// <see cref="Result.TryAsync{T}(Func{CancellationToken, Task{T}}, Func{Exception, Error}?, CancellationToken)"/>.
    /// </para>
    /// <list type="bullet">
    ///   <item><description><see cref="TimeoutException"/> → <see cref="ErrorType.Timeout"/></description></item>
    ///   <item><description><see cref="UnauthorizedAccessException"/> → <see cref="ErrorType.Unauthorized"/></description></item>
    ///   <item><description><see cref="ArgumentException"/> → <see cref="ErrorType.Validation"/></description></item>
    ///   <item><description>All others → <see cref="ErrorType.Unexpected"/></description></item>
    /// </list>
    /// </summary>
    public static Error FromException(Exception exception) {
        ArgumentNullException.ThrowIfNull(exception);

        return exception switch {
            TimeoutException => Timeout("Exception.Timeout", exception.Message),
            UnauthorizedAccessException => Unauthorized("Exception.Unauthorized", exception.Message),
            ArgumentException => Validation("Exception.Argument", exception.Message),
            _ => Unexpected($"Exception.{exception.GetType().Name}", exception.Message)
        };
    }

    /// <summary>
    /// Converts an <see cref="Exception"/> to an <see cref="Error"/> and optionally
    /// attaches the exception type name as metadata under the key
    /// <c>"ExceptionType"</c>. Use when you want to preserve the exception type
    /// for diagnostics without exposing the full stack trace.
    /// </summary>
    public static Error FromException(Exception exception, bool includeType) {
        Error error = FromException(exception);
        return includeType
            ? error.WithMetadata("ExceptionType",
                exception.GetType().FullName ?? exception.GetType().Name)
            : error;
    }

    /// <summary>
    /// Creates an error with a custom <see cref="ErrorType"/>.
    /// Use this when the built-in factory methods do not cover your domain.
    /// </summary>
    /// <example>
    /// <code>
    /// public static class AppErrorTypes {
    ///     public static readonly ErrorType RateLimit = new("RateLimit");
    /// }
    ///
    /// Error.Custom(AppErrorTypes.RateLimit, "RateLimit.Exceeded", "Too many requests.");
    /// </code>
    /// </example>
    public static Error Custom(ErrorType type, string code, string description) {
        return new(code, description, type, null);
    }
 
    // ── No-op sentinel ────────────────────────────────────────────────────────

    /// <summary>
    /// A sentinel "no error" value.
    /// <para>
    /// <b>Do not use <see cref="None"/> to represent success.</b>
    /// Use <see cref="Result{TValue}"/> for that. <see cref="None"/> is intended
    /// for scenarios where an <see cref="Error"/> slot must be filled but there is
    /// nothing meaningful to report — e.g., a default field in a struct, a placeholder
    /// in a collection before errors are populated, or a test fixture.
    /// </para>
    /// </summary>
    public static readonly Error None = new(
        code: "None",
        description: "No error.",
        type: ErrorType.Failure,
        metadata: null);

    // ── Fluent metadata ───────────────────────────────────────────────────────

    /// <summary>
    /// Returns a new <see cref="Error"/> with an additional metadata entry.
    /// Does not mutate the current instance.
    /// </summary>
    public Error WithMetadata(string key, object value) {
        Dictionary<string, object?> newMeta = this.Metadata is null
            ? new(1)
            : new(this.Metadata);
        newMeta[key] = value;
        return new Error(this.Code, this.Description, this.Type, newMeta);
    }
}