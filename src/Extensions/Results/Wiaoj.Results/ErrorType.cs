namespace Wiaoj.Results;   
/// <summary>
/// Defines the category of an error to facilitate appropriate handling (e.g., correct HTTP status codes).
/// </summary>
public enum ErrorType {
    /// <summary>A generic failure.</summary>
    Failure = 0,
    /// <summary>A validation failure (HTTP 400).</summary>
    Validation = 1,
    /// <summary>Resource not found (HTTP 404).</summary>
    NotFound = 2,
    /// <summary>Resource conflict, e.g., duplicate entry (HTTP 409).</summary>
    Conflict = 3,
    /// <summary>Authentication required (HTTP 401).</summary>
    Unauthorized = 4,
    /// <summary>Access denied (HTTP 403).</summary>
    Forbidden = 5,
    /// <summary>An unexpected system error (HTTP 500).</summary>
    Unexpected = 6
}