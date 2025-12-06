using Microsoft.Extensions.ObjectPool;
using Wiaoj.ObjectPool.Abstractions;

namespace Wiaoj.ObjectPool.Internal;
/// <summary>
/// An internal adapter that wraps our custom <see cref="IPoolPolicy{T}"/>
/// to make it compatible with the underlying Microsoft <see cref="IPooledObjectPolicy{T}"/>.
/// This is a private implementation detail.
/// </summary>
internal sealed class MicrosoftPooledObjectPolicyAdapter<T>(IPoolPolicy<T> underlyingPolicy) : IPooledObjectPolicy<T> where T : notnull {
    private readonly IPoolPolicy<T> _underlyingPolicy = underlyingPolicy;

    /// <inheritdoc/>
    public T Create() {
        return this._underlyingPolicy.Create();
    }

    /// <inheritdoc/>
    public bool Return(T obj) {
        return this._underlyingPolicy.TryReset(obj);
    }
}