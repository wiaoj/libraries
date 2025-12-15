using Wiaoj.Abstractions;

namespace Wiaoj.ObjectPool.Policies;
/// <summary>
/// A policy that handles objects implementing IAsyncResettable.
/// Can optionally use an IAsyncFactory for creation.
/// </summary>
internal sealed class AsyncResettableObjectPolicy<T> : IAsyncPoolPolicy<T>
    where T : class, IAsyncResettable {
    private readonly IAsyncFactory<T>? _factory;

    // Constructor 1: Factory varsa onu kullan
    public AsyncResettableObjectPolicy(IAsyncFactory<T>? factory = null) {
        _factory = factory;
    }

    public async ValueTask<T> CreateAsync(CancellationToken cancellationToken) {
        if (_factory is not null) {
            return await _factory.CreateAsync(cancellationToken);
        }

        // Factory yoksa new() kısıtlaması ile oluştur.
        // Not: Bu satırın çalışması için class tanımına 'new()' eklememiz gerekir
        // ama IAsyncFactory opsiyonel olduğu için reflection veya activator kullanmak 
        // yerine burada "FastActivator" mantığı veya constraint ayrımı gerekir.
        // Basitlik adına: Eğer Factory yoksa T'nin new() constraint'i olduğunu varsayan
        // ayrı bir sınıf daha temiz olur. Ama şimdilik Activator.CreateInstance (güvenli mod) kullanalım
        // ya da generic constraint'i zorlayalım.

        return Activator.CreateInstance<T>();
    }

    public ValueTask<bool> TryResetAsync(T obj) {
        return obj.TryResetAsync();
    }
}