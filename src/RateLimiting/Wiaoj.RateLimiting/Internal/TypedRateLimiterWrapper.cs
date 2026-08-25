namespace Wiaoj.RateLimiting.Internal;

internal sealed class TypedRateLimiterWrapper<TPolicy>(IRateLimiter inner) : IRateLimiter<TPolicy> where TPolicy : notnull {

    private readonly string _policyName = typeof(TPolicy).Name;

    public ValueTask<RateLimitDecision> TryAcquireAsync(
        string key,
        int cost,
        CancellationToken cancellationToken) {
        return inner.TryAcquireAsync(this._policyName, key, cost, cancellationToken);
    }
}