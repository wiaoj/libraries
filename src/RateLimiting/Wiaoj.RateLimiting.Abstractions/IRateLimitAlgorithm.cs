namespace Wiaoj.RateLimiting;

/// <summary>
/// Represents a rate limiting algorithm capable of deciding whether an operation identified
/// by a given key is allowed to proceed.
/// </summary>
/// <remarks>
/// <para>
/// This contract is deliberately direction- and transport-agnostic: it does not know whether the
/// caller is protecting an inbound endpoint (e.g. an ASP.NET Core middleware) or throttling an
/// outbound call (e.g. a webhook delivery or a third-party API client). Both directions call the
/// same <see cref="TryAcquireAsync"/> method with a domain-specific key.
/// </para>
/// <para>
/// Implementations are expected to be safe for concurrent use across multiple callers and,
/// for distributed implementations, across multiple process instances sharing the same backing store.
/// </para>
/// </remarks>
public interface IRateLimitAlgorithm {
    /// <summary>
    /// Attempts to acquire permission to perform an operation identified by <paramref name="key"/>.
    /// </summary>
    /// <param name="key">
    /// The identifier the limit is tracked against (e.g. an endpoint id, a client id, an IP address).
    /// Callers are responsible for building a sufficiently unique and stable key.
    /// </param>
    /// <param name="cost">
    /// The number of units this operation consumes from the limit. Defaults to <c>1</c>.
    /// Use a higher value for operations that are disproportionately expensive relative to a typical request.
    /// </param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    /// A <see cref="RateLimitDecision"/> indicating whether the operation is allowed, and if not,
    /// how long the caller should wait before retrying.
    /// </returns>
    ValueTask<RateLimitDecision> TryAcquireAsync(string key, int cost = 1, CancellationToken cancellationToken = default);
}