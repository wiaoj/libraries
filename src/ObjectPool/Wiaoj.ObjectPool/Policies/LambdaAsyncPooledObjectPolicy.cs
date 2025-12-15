namespace Wiaoj.ObjectPool.Policies;
internal sealed class LambdaAsyncPooledObjectPolicy<T>(
    Func<CancellationToken, ValueTask<T>> factory,
    Func<T, ValueTask<bool>> resetter) : IAsyncPoolPolicy<T> where T : notnull {
    public ValueTask<T> CreateAsync(CancellationToken cancellationToken) {
        return factory(cancellationToken);
    }

    public ValueTask<bool> TryResetAsync(T obj) {
        return resetter(obj);
    }
}