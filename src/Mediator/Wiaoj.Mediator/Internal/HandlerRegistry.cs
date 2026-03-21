using System.Collections.Concurrent;
using System.Collections.Frozen;

namespace Wiaoj.Mediator.Internal;

internal sealed class HandlerRegistry {
    // Exact Match (Tam eşleşme) - Startup'ta dolar ve donar.
    private IDictionary<Type, object> _requestHandlers = new Dictionary<Type, object>();
    private IDictionary<Type, object>? _streamHandlers = null;

    // Polymorphic Cache - Runtime'da dolar (O yüzden Concurrent ve Mutable kalmalı)
    private readonly ConcurrentDictionary<Type, object> _polymorphicCache = new();

    public void Register(Type requestType, object handlerDelegate) {
        this._requestHandlers[requestType] = handlerDelegate;
    }

    public void RegisterStream(Type requestType, object handlerDelegate) {
        this._streamHandlers ??= new Dictionary<Type, object>();
        this._streamHandlers[requestType] = handlerDelegate;
    }

    public void ToFrozen() {
        // Sadece başlangıçta bulunan handler setini donduruyoruz.
        this._requestHandlers = this._requestHandlers.ToFrozenDictionary();
        this._streamHandlers = this._streamHandlers?.ToFrozenDictionary();
    }

    public Func<IServiceProvider, object, CancellationToken, Task<TResponse>> GetRequestHandler<TResponse>(Type requestType) {
        // 1. TAM EŞLEŞME (En Hızlı)
        if(this._requestHandlers.TryGetValue(requestType, out object? handler))
            return (Func<IServiceProvider, object, CancellationToken, Task<TResponse>>)handler;

        // 2. CACHE KONTROLÜ (Daha önce türetilmiş bir tip geldiyse buradan döner)
        if(this._polymorphicCache.TryGetValue(requestType, out object? cachedHandler))
            return (Func<IServiceProvider, object, CancellationToken, Task<TResponse>>)cachedHandler;

        // 3. POLİMORFİK ARAMA (Yavaş işlem - Sadece ilk seferde çalışır)
        // Request tipinin implemente ettiği interface'lere veya base class'lara bak
        Type? baseTypeMatch = this._requestHandlers.Keys.FirstOrDefault(k => k.IsAssignableFrom(requestType));

        if(baseTypeMatch != null) {
            object baseHandler = this._requestHandlers[baseTypeMatch];

            // Bulunan eşleşmeyi cache'e at
            this._polymorphicCache.TryAdd(requestType, baseHandler);

            return (Func<IServiceProvider, object, CancellationToken, Task<TResponse>>)baseHandler;
        }

        throw new InvalidOperationException($"Request handler not found for: {requestType.Name}");
    }

    public Func<IServiceProvider, object, CancellationToken, IAsyncEnumerable<TResponse>> GetStreamHandler<TResponse>(Type requestType) {
        // Stream için polymorphic destek genelde gerekmez, o yüzden sadece exact match
        if(this._streamHandlers != null && this._streamHandlers.TryGetValue(requestType, out object? handler))
            return (Func<IServiceProvider, object, CancellationToken, IAsyncEnumerable<TResponse>>)handler;

        throw new InvalidOperationException($"Stream handler not found for: {requestType.Name}");
    }
}