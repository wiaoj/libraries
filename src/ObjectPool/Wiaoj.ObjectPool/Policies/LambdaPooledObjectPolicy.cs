namespace Wiaoj.ObjectPool.Policies;
/// <summary>
/// An internal implementation of <see cref="IPoolPolicy{T}"/> that uses
/// delegates (lambdas) to create and reset objects.
/// </summary>
/// <typeparam name="T">The type of object to pool.</typeparam>
internal sealed class LambdaPooledObjectPolicy<T>(Func<T> factory, Predicate<T> resetter) : IPoolPolicy<T> where T : notnull {
    private readonly Func<T> _factory = factory;
    private readonly Predicate<T> _resetter = resetter;

    /// <inheritdoc/>
    public T Create() {
        return this._factory();
    }

    /// <inheritdoc/>
    public bool TryReset(T obj) {
        return this._resetter(obj);
    }
}