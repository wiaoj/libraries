using Wiaoj.Abstractions;

namespace Wiaoj.ObjectPool.Policies;

/// <summary>
/// A policy that handles objects implementing <see cref="IAsyncResettable"/>.
/// </summary>
internal sealed class AsyncResettableObjectPolicy<T> : IAsyncPoolPolicy<T> where T : class, IAsyncResettable {
    private readonly IAsyncFactory<T>? _factory;
    private readonly Func<T> _fallbackFactory;

    public AsyncResettableObjectPolicy(IAsyncFactory<T>? factory = null, Func<T>? fallbackFactory = null) {
        this._factory = factory;
        this._fallbackFactory = fallbackFactory ?? Activator.CreateInstance<T>;
    }

    public async ValueTask<T> CreateAsync(CancellationToken cancellationToken) {
        if(this._factory is not null) {
            return await this._factory.CreateAsync(cancellationToken).ConfigureAwait(false);
        }

        return this._fallbackFactory();
    }

    public ValueTask<bool> TryResetAsync(T obj) {
        return obj.TryResetAsync();
    }
}