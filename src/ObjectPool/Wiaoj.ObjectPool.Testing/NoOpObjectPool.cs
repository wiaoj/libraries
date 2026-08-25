using Wiaoj.Preconditions;

namespace Wiaoj.ObjectPool.Testing;

/// <summary>
/// A lightweight no-operation implementation of <see cref="IObjectPool{T}"/> for test scenarios
/// that simply require a valid pool reference without lifecycle tracking.
/// </summary>
public sealed class NoOpObjectPool<T> : IObjectPool<T> where T : class {
    private readonly Func<T> _factory;

    public NoOpObjectPool(Func<T> factory) {
        Preca.ThrowIfNull(factory);
        this._factory = factory;
    }

    public NoOpObjectPool() : this(() => Activator.CreateInstance<T>()) { }

    /// <inheritdoc/>
    public PooledObject<T> Lease() {
        return new PooledObject<T>(this._factory(), this);
    }

    /// <inheritdoc/>
    public T Get() => this._factory();

    /// <inheritdoc/>
    public void Return(T obj) {
        // No-op by design
    }
}