using Wiaoj.Abstractions;

namespace Wiaoj.ObjectPool.Policies;
/// <summary>
/// A policy that uses an external IAsyncFactory<T> for creation,
/// and delegates reset logic to a lambda or IResettable.
/// </summary>
internal sealed class AsyncFactoryPooledObjectPolicy<T> : IAsyncPoolPolicy<T> where T : notnull {
    private readonly IAsyncFactory<T> _factory;
    private readonly Func<T, ValueTask<bool>> _resetter;

    public AsyncFactoryPooledObjectPolicy(IAsyncFactory<T> factory, Func<T, ValueTask<bool>> resetter) {
        this._factory = factory;
        this._resetter = resetter;
    }

    public ValueTask<T> CreateAsync(CancellationToken cancellationToken) {
        // IAsyncFactory Task döner, biz ValueTask'e çeviriyoruz.
        return new ValueTask<T>(this._factory.CreateAsync(cancellationToken));
    }

    public ValueTask<bool> TryResetAsync(T obj) {
        return this._resetter(obj);
    }
}