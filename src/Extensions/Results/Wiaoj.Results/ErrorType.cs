using System.Text.Json.Serialization;

namespace Wiaoj.Results;

/// <summary>
/// Categorizes an <see cref="Error"/> to facilitate appropriate handling
/// (e.g., mapping to HTTP status codes, logging severity).
/// <para>
/// Unlike a plain <c>enum</c>, <see cref="ErrorType"/> is an extensible value type.
/// Define domain-specific types by declaring static fields in your own project:
/// </para>
/// <code>
/// public static class AppErrorTypes {
///     public static readonly ErrorType RateLimit    = new("RateLimit");
///     public static readonly ErrorType Maintenance  = new("Maintenance");
///     public static readonly ErrorType Timeout      = new("Timeout");
/// }
/// </code>
/// Predefined types match the original enum values for easy migration.
/// </summary>
[JsonConverter(typeof(ErrorTypeJsonConverter))]
public readonly record struct ErrorType {

    /// <summary>The unique name of this error type.</summary>
    public string Name { get; }

    /// <summary>
    /// Creates a custom error type with the specified name.
    /// </summary>
    /// <param name="name">A unique, human-readable name. Example: <c>"RateLimit"</c>.</param>
    public ErrorType(string name) {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        this.Name = name;
    }

    /// <inheritdoc/>
    public override string ToString() {
        return this.Name;
    }

    // ── Built-in types ────────────────────────────────────────────────────────

    /// <summary>A generic failure the producer declares. Maps to HTTP 400; use <see cref="Unexpected"/> for a server fault (500).</summary>
    public static readonly ErrorType Failure = new(nameof(Failure));

    /// <summary>Input validation failure (HTTP 400).</summary>
    public static readonly ErrorType Validation = new(nameof(Validation));

    /// <summary>Resource not found (HTTP 404).</summary>
    public static readonly ErrorType NotFound = new(nameof(NotFound));

    /// <summary>Resource conflict, e.g., duplicate entry (HTTP 409).</summary>
    public static readonly ErrorType Conflict = new(nameof(Conflict));

    /// <summary>Authentication required (HTTP 401).</summary>
    public static readonly ErrorType Unauthorized = new(nameof(Unauthorized));

    /// <summary>Access denied (HTTP 403).</summary>
    public static readonly ErrorType Forbidden = new(nameof(Forbidden));

    /// <summary>Unexpected system error (HTTP 500).</summary>
    public static readonly ErrorType Unexpected = new(nameof(Unexpected));

    /// <summary>Rate limit exceeded (HTTP 429).</summary>
    public static readonly ErrorType RateLimit = new(nameof(RateLimit));

    /// <summary>Operation timed out (HTTP 408). A gateway or proxy timeout is 504: register it, or use <see cref="Unavailable"/>.</summary>
    public static readonly ErrorType Timeout = new(nameof(Timeout));

    /// <summary>
    /// The operation was cancelled before it completed (HTTP 499).
    /// Different from <see cref="Timeout"/>: nobody waited too long, the caller went away.
    /// 499 is the nginx "Client Closed Request" convention - the client is already gone, so the
    /// status exists to keep this out of the 5xx error budget. Register 400 if you would rather
    /// answer with a registered code.
    /// </summary>
    public static readonly ErrorType Cancelled = new(nameof(Cancelled));

    /// <summary>Service temporarily unavailable (HTTP 503).</summary>
    public static readonly ErrorType Unavailable = new(nameof(Unavailable));

    /// <summary>
    /// Resource permanently gone (HTTP 410).
    /// Different from <see cref="NotFound"/>: Gone means the resource will never return.
    /// </summary>
    public static readonly ErrorType Gone = new(nameof(Gone));

    /// <summary>
    /// Semantically invalid request (HTTP 422).
    /// Use for domain/business rule violations where the payload is syntactically valid.
    /// Different from <see cref="Validation"/>: Validation is for format/schema errors.
    /// </summary>
    public static readonly ErrorType UnprocessableEntity = new(nameof(UnprocessableEntity));

    /// <summary>
    /// A conditional-request precondition the client sent did not hold (HTTP 412).
    /// Use when an <c>If-Match</c>, <c>If-Unmodified-Since</c> or <c>If-None-Match</c> header the client
    /// supplied evaluates false against the current resource state - typically a lost optimistic-concurrency
    /// race against an ETag the client read earlier.
    /// Different from <see cref="PreconditionRequired"/>: here the precondition exists and failed; there the
    /// client sent no conditional header at all.
    /// </summary>
    public static readonly ErrorType PreconditionFailed = new(nameof(PreconditionFailed));

    /// <summary>
    /// The server requires the request to be conditional, and it was not (HTTP 428, RFC 6585).
    /// Use when a resource refuses to act on a request that carries no <c>If-Match</c>/<c>If-Unmodified-Since</c>
    /// precondition at all, so a blind write cannot silently overwrite a concurrent change.
    /// Different from <see cref="PreconditionFailed"/>: 412 is about a precondition that failed, 428 is about a
    /// precondition that was missing. The two blame different omissions, so the two need different codes.
    /// </summary>
    public static readonly ErrorType PreconditionRequired = new(nameof(PreconditionRequired));

    /// <summary>
    /// The payload's <c>Content-Type</c> is not one the resource accepts (HTTP 415).
    /// Use when the media type itself is unsupported - a <c>text/plain</c> body sent to an endpoint that only
    /// reads <c>application/json</c>.
    /// Different from <see cref="Validation"/>, which says the body was parsed and rejected, and from
    /// <see cref="UnprocessableEntity"/>, which says the parsed content broke a business rule.
    /// </summary>
    public static readonly ErrorType UnsupportedMediaType = new(nameof(UnsupportedMediaType));

    /// <summary>
    /// The method is known to the server but is not allowed for this resource (HTTP 405).
    /// Use when the route exists - <c>DELETE /orders/1</c> is understood - but that verb is not offered for it,
    /// as opposed to <see cref="NotFound"/>, which says no such resource exists at all.
    /// A 405 response must carry an <c>Allow</c> header listing the methods the resource does accept. That is
    /// the endpoint's responsibility, not the mapper's: a status-code lookup has no way to know which verbs a
    /// given route offers, so the endpoint has to set <c>Allow</c> itself.
    /// </summary>
    public static readonly ErrorType MethodNotAllowed = new(nameof(MethodNotAllowed));

    /// <summary>
    /// The server does not support the functionality required to fulfill the request (HTTP 501).
    /// Use for a verb or capability that is not implemented anywhere, such as answering a <c>PUT</c> on a
    /// read-only resource model.
    /// Different from <see cref="Unavailable"/> (HTTP 503): 503 says "supported, but not right now" and
    /// invites a retry, while 501 says "never, as deployed" - retrying a 501 does not help.
    /// </summary>
    public static readonly ErrorType NotImplemented = new(nameof(NotImplemented));
}