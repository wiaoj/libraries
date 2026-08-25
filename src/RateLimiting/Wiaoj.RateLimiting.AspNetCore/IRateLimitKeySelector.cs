namespace Wiaoj.RateLimiting.AspNetCore;

/// <summary>
/// Defines a strategy for extracting a rate limiting identity partition key from an incoming execution context.
/// </summary>
/// <typeparam name="TContext">The type of the contextual object (e.g. <c>HttpContext</c>, gRPC call context).</typeparam>
public interface IRateLimitKeySelector<in TContext> {
    /// <summary>
    /// Extracts the rate limiting partition key from the specified context.
    /// </summary>
    /// <param name="context">The contextual execution object.</param>
    /// <returns>The extracted partition key string.</returns>
    string GetKey(TContext context);
}