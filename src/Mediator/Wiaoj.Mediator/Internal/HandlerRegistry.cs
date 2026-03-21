using System.Collections.Concurrent;
using System.Collections.Frozen;

namespace Wiaoj.Mediator.Internal; 
internal sealed class HandlerRegistry {
    // Populated once at startup, then frozen for lock-free O(1) reads.
    private IDictionary<Type, object> _requestHandlers = new Dictionary<Type, object>();
    private IDictionary<Type, object>? _streamHandlers;

    // Filled at runtime for polymorphic dispatch (derived-type requests).
    private readonly ConcurrentDictionary<Type, object> _polymorphicCache = new();

    public void Register(Type requestType, object handlerDelegate) {
        this._requestHandlers[requestType] = handlerDelegate;
    }

    public void RegisterStream(Type requestType, object handlerDelegate) {
        this._streamHandlers ??= new Dictionary<Type, object>();
        this._streamHandlers[requestType] = handlerDelegate;
    }

    public void ToFrozen() {
        this._requestHandlers = this._requestHandlers.ToFrozenDictionary();
        this._streamHandlers = this._streamHandlers?.ToFrozenDictionary();
    }

    public Func<IServiceProvider, object, CancellationToken, Task<TResponse>>
        GetRequestHandler<TResponse>(Type requestType) {

        // 1. Exact match (fastest path — FrozenDictionary O(1))
        if(this._requestHandlers.TryGetValue(requestType, out object? handler))
            return (Func<IServiceProvider, object, CancellationToken, Task<TResponse>>)handler;

        // 2. Polymorphic cache (subsequent calls for derived types)
        if(this._polymorphicCache.TryGetValue(requestType, out object? cached))
            return (Func<IServiceProvider, object, CancellationToken, Task<TResponse>>)cached;

        // 3. Polymorphic scan (first call only — result is cached above)
        Type? baseMatch = this._requestHandlers.Keys
            .FirstOrDefault(k => k.IsAssignableFrom(requestType));

        if(baseMatch is not null) {
            object baseHandler = this._requestHandlers[baseMatch];
            this._polymorphicCache.TryAdd(requestType, baseHandler);
            return (Func<IServiceProvider, object, CancellationToken, Task<TResponse>>)baseHandler;
        }

        throw new InvalidOperationException(
            $"No request handler registered for: {requestType.FullName}");
    }

    public Func<IServiceProvider, object, CancellationToken, IAsyncEnumerable<TResponse>>
        GetStreamHandler<TResponse>(Type requestType) {

        if(this._streamHandlers is not null && this._streamHandlers.TryGetValue(requestType, out object? handler))
            return (Func<IServiceProvider, object, CancellationToken, IAsyncEnumerable<TResponse>>)handler;

        throw new InvalidOperationException(
            $"No stream handler registered for: {requestType.FullName}");
    }
}