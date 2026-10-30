using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json.Serialization;

namespace Wiaoj.Results;

/// <summary>
/// Represents a structured, immutable error with optional contextual metadata.
/// </summary>
[JsonConverter(typeof(ErrorJsonConverter))]
public readonly record struct Error : IEquatable<Error> {

    /// <summary>Gets the machine-readable error code, e.g., <c>"User.NotFound"</c>.</summary>
    public string Code { get; }

    /// <summary>Gets the human-readable description.</summary>
    public string Description { get; }

    /// <summary>Gets the category of the error.</summary>
    public ErrorType Type { get; }

    /// <summary>Gets the optional contextual metadata. <see langword="null"/> when no metadata is attached.</summary>
    public IReadOnlyDictionary<string, object?>? Metadata { get; }

    /// <summary>
    /// Gets the <see cref="Exception"/> this error was produced from, when it came from
    /// <see cref="FromException(Exception)"/> or <see cref="WithCause"/>. <see langword="null"/> otherwise.
    /// </summary>
    /// <remarks>
    /// The cause keeps the stack trace for logging (<c>logger.LogError(error.Cause, "...")</c>). It is not part of
    /// <see cref="Equals(Error)"/>, and it is never serialized: <c>ErrorJsonConverter</c> writes only code,
    /// description, type and metadata, so a stack trace cannot reach a response body by accident.
    /// </remarks>
    public Exception? Cause { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="Error"/> record struct.
    /// </summary>
    public Error(string code, string description, ErrorType type, IReadOnlyDictionary<string, object?>? metadata = null)
        : this(code, description, type, metadata, cause: null) {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Error"/> record struct, keeping <paramref name="cause"/>
    /// for logging. Kept as a separate overload so the four-argument constructor stays binary-compatible.
    /// </summary>
    public Error(string code, string description, ErrorType type, IReadOnlyDictionary<string, object?>? metadata, Exception? cause) {
        this.Code = code;
        this.Description = description;
        this.Type = type;
        this.Metadata = metadata;
        this.Cause = cause;
    }

    // ── Built-in factory methods ──────────────────────────────────────────────

    /// <summary>Creates a general failure error (HTTP 400).</summary>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Error Failure(
        string code = "General.Failure",
        string description = "A failure has occurred.") =>
        new(code, description, ErrorType.Failure);

    /// <summary>Creates an unexpected system error.</summary>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Error Unexpected(
        string code = "General.Unexpected",
        string description = "An unexpected error occurred.") =>
        new(code, description, ErrorType.Unexpected);

    /// <summary>Creates a validation error.</summary>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Error Validation(string code, string description) =>
        new(code, description, ErrorType.Validation);

    /// <summary>Creates a not found error.</summary>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Error NotFound(
        string code = "Resource.NotFound",
        string description = "Resource not found.") =>
        new(code, description, ErrorType.NotFound);

    /// <summary>Creates a not found error with a resource name and identifier.</summary>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Error NotFound(string resourceName, object id) =>
        new($"{resourceName}.NotFound", $"{resourceName} with id '{id}' was not found.", ErrorType.NotFound);

    /// <summary>Creates a conflict error (HTTP 409).</summary>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Error Conflict(
        string code = "Resource.Conflict",
        string description = "A conflict has occurred.") =>
        new(code, description, ErrorType.Conflict);

    /// <summary>Creates an unauthorized error (HTTP 401).</summary>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Error Unauthorized(
        string code = "Auth.Unauthorized",
        string description = "Unauthorized access.") =>
        new(code, description, ErrorType.Unauthorized);

    /// <summary>Creates a forbidden error (HTTP 403).</summary>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Error Forbidden(
        string code = "Auth.Forbidden",
        string description = "Access forbidden.") =>
        new(code, description, ErrorType.Forbidden);

    /// <summary>Creates a rate limit exceeded error (HTTP 429).</summary>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Error RateLimitExceeded(
        string code = "RateLimit.Exceeded",
        string description = "Too many requests. Please try again later.") =>
        new(code, description, ErrorType.RateLimit);

    /// <summary>Creates a timeout error (HTTP 408).</summary>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Error Timeout(
        string code = "Request.Timeout",
        string description = "The operation timed out.") =>
        new(code, description, ErrorType.Timeout);

    /// <summary>Creates a cancelled error (HTTP 499).</summary>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Error Cancelled(
        string code = "Request.Cancelled",
        string description = "The operation was cancelled.") =>
        new(code, description, ErrorType.Cancelled);

    /// <summary>Creates a service unavailable error (HTTP 503).</summary>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Error ServiceUnavailable(
        string code = "Service.Unavailable",
        string description = "The service is temporarily unavailable.") =>
        new(code, description, ErrorType.Unavailable);

    /// <summary>Creates a gone error (HTTP 410).</summary>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Error Gone(
        string code = "Resource.Gone",
        string description = "The resource has been permanently removed.") =>
        new(code, description, ErrorType.Gone);

    /// <summary>Creates an unprocessable entity error (HTTP 422).</summary>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Error UnprocessableEntity(string code, string description) =>
        new(code, description, ErrorType.UnprocessableEntity);

    /// <summary>
    /// Creates a conditional-request precondition failure (HTTP 412) for an <c>If-Match</c>,
    /// <c>If-Unmodified-Since</c> or <c>If-None-Match</c> condition the client sent that did not hold.
    /// </summary>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Error PreconditionFailed(
        string code = "Request.PreconditionFailed",
        string description = "A precondition given in the request headers was not met by the current resource state.") =>
        new(code, description, ErrorType.PreconditionFailed);

    /// <summary>
    /// Creates a missing-precondition error (HTTP 428, RFC 6585) for a resource that requires the request to be
    /// conditional and got a plain, unconditional one.
    /// </summary>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Error PreconditionRequired(
        string code = "Request.PreconditionRequired",
        string description = "The request must be conditional; send it with an If-Match or If-Unmodified-Since precondition.") =>
        new(code, description, ErrorType.PreconditionRequired);

    /// <summary>Creates an unsupported media type error (HTTP 415) for a payload the resource cannot read.</summary>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Error UnsupportedMediaType(
        string code = "Request.UnsupportedMediaType",
        string description = "The media type of the request payload is not supported by the resource.") =>
        new(code, description, ErrorType.UnsupportedMediaType);

    /// <summary>
    /// Creates a method-not-allowed error (HTTP 405) for a verb the server knows but this resource does not offer.
    /// The response must also carry an <c>Allow</c> header listing the accepted methods; setting it is the
    /// endpoint's job, not this factory's.
    /// </summary>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Error MethodNotAllowed(
        string code = "Request.MethodNotAllowed",
        string description = "The request method is known by the server but is not allowed for the requested resource.") =>
        new(code, description, ErrorType.MethodNotAllowed);

    /// <summary>
    /// Creates a not-implemented error (HTTP 501) for functionality the server does not support at all. Use
    /// <see cref="ServiceUnavailable"/> (HTTP 503) instead when the functionality exists but is temporarily down.
    /// </summary>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Error NotImplemented(
        string code = "Service.NotImplemented",
        string description = "The server does not support the functionality required to fulfill the request.") =>
        new(code, description, ErrorType.NotImplemented);

    /// <summary>Converts an <see cref="Exception"/> to an <see cref="Error"/>, keeping the exception as its cause.</summary>
    [Pure]
    public static Error FromException(Exception exception) {
        ArgumentNullException.ThrowIfNull(exception);

        Error error = exception switch {
            OperationCanceledException => Cancelled("Exception.Cancelled", exception.Message),
            TimeoutException => Timeout("Exception.Timeout", exception.Message),
            UnauthorizedAccessException => Unauthorized("Exception.Unauthorized", exception.Message),
            ArgumentException => Validation("Exception.Argument", exception.Message),
            _ => Unexpected($"Exception.{exception.GetType().Name}", exception.Message)
        };

        return error.WithCause(exception);
    }

    /// <summary>Converts an <see cref="Exception"/> to an <see cref="Error"/> attaching exception type metadata.</summary>
    [Pure]
    public static Error FromException(Exception exception, bool includeType) {
        Error error = FromException(exception);
        return includeType
            ? error.WithMetadata("ExceptionType", exception.GetType().FullName ?? exception.GetType().Name)
            : error;
    }

    /// <summary>Creates an error with a custom <see cref="ErrorType"/>.</summary>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Error Custom(ErrorType type, string code, string description) =>
        new(code, description, type);

    // ── Sentinel ──────────────────────────────────────────────────────────────

    /// <summary>A sentinel "no error" instance.</summary>
    public static readonly Error None = new(
        code: "None",
        description: "No error.",
        type: ErrorType.Failure);

    /// <summary>A sentinel error representing an uninitialized default struct state.</summary>
    public static readonly Error Uninitialized = new(
        code: "Result.Uninitialized",
        description: "The result is in an uninitialized default state.",
        type: ErrorType.Unexpected);

    // ── Fluent Metadata & Cause ───────────────────────────────────────────────

    /// <summary>
    /// Returns a new <see cref="Error"/> carrying <paramref name="cause"/> as its originating exception.
    /// </summary>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Error WithCause(Exception cause) {
        ArgumentNullException.ThrowIfNull(cause);

        return new Error(this.Code, this.Description, this.Type, this.Metadata, cause);
    }

    /// <summary>
    /// Returns a new <see cref="Error"/> with an additional metadata entry.
    /// </summary>
    [Pure]
    public Error WithMetadata(string key, object? value) {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        Dictionary<string, object?> newMetadata = this.Metadata is null
            ? new(1)
            : new(this.Metadata);

        newMetadata[key] = value;
        return new Error(this.Code, this.Description, this.Type, newMetadata, this.Cause);
    }

    // ── Equality & HashCode ───────────────────────────────────────────────────

    /// <inheritdoc/>
    [Pure]
    public bool Equals(Error other) {
        if(this.Code != other.Code || this.Description != other.Description || this.Type != other.Type)
            return false;

        if(ReferenceEquals(this.Metadata, other.Metadata))
            return true;

        if(this.Metadata is null || other.Metadata is null)
            return false;

        if(this.Metadata.Count != other.Metadata.Count)
            return false;

        foreach(var (key, value) in this.Metadata) {
            if(!other.Metadata.TryGetValue(key, out object? otherValue))
                return false;

            if(!Equals(value, otherValue))
                return false;
        }

        return true;
    }

    /// <summary>
    /// Supplies the members the record-struct <see cref="object.ToString()"/> prints.
    /// <see cref="Cause"/> is excluded deliberately: an exception's <c>ToString</c> is its type, message and
    /// stack trace, and an <see cref="Error"/> gets interpolated into log messages and diagnostics far more
    /// often than it is handed to a logger as an exception. Log the cause with the <c>ILogger</c> overloads.
    /// </summary>
    private bool PrintMembers(StringBuilder builder) {
        builder.Append("Code = ").Append(this.Code)
               .Append(", Description = ").Append(this.Description)
               .Append(", Type = ").Append(this.Type.Name)
               .Append(", Metadata = ");

        if(this.Metadata is null) {
            builder.Append("null");
        } else {
            builder.AppendJoin(", ", this.Metadata.Select(m => $"{m.Key} = {m.Value}"));
        }

        return true;
    }

    /// <inheritdoc/>
    [Pure]
    public override int GetHashCode() {
        HashCode hash = new();
        hash.Add(this.Code);
        hash.Add(this.Description);
        hash.Add(this.Type);

        if(this.Metadata is not null) {
            int metadataHash = 0;
            foreach(var (key, value) in this.Metadata) {
                unchecked {
                    metadataHash += HashCode.Combine(key, value);
                }
            }
            hash.Add(metadataHash);
        }

        return hash.ToHashCode();
    }
}