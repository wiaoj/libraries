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

    /// <summary>A generic failure. Default when no specific type applies.</summary>
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

    /// <summary>Operation timed out (HTTP 408 / 504).</summary>
    public static readonly ErrorType Timeout = new(nameof(Timeout));

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
}