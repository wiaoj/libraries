#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Wiaoj.Mediator.Behaviors;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Thrown when a request exceeds its configured rate limit.
/// </summary>
public sealed class RateLimitExceededException : Exception {
    /// <summary>The request type that was rate-limited.</summary>
    public Type RequestType { get; }

    /// <summary>The limit that was exceeded.</summary>
    public int Limit { get; }

    /// <summary>The window in which the limit applies.</summary>
    public RateLimitWindow Window { get; }

    internal RateLimitExceededException(Type requestType, int limit, RateLimitWindow window)
        : base($"Rate limit exceeded for '{requestType.Name}': {limit} requests per {window}.") {
        this.RequestType = requestType;
        this.Limit = limit;
        this.Window = window;
    }
}
