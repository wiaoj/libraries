namespace Wiaoj.ObjectPool.Policies;
/// <summary>
/// Default policy for creating and resetting pooled objects.
/// </summary>
internal sealed class DefaultPoolPolicy<T> : IPoolPolicy<T> where T : class, new() {
    public T Create() {
        return new();
    }

    public bool TryReset(T obj) {
        return true;
    }
}