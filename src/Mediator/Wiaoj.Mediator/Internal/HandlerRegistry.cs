using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Diagnostics;

namespace Wiaoj.Mediator.Internal; 
[DebuggerStepThrough, DebuggerNonUserCode]
internal sealed class HandlerRegistry {
    // Registration phase (Mutable) - Used only during startup.
    private Dictionary<Type, object>? _mutableRequestHandlers = [];
    private Dictionary<Type, object>? _mutableStreamHandlers;

    // Runtime phase (Immutable) - Concrete types avoid interface virtual dispatch.
    private FrozenDictionary<Type, object>? _requestHandlers;
    private FrozenDictionary<Type, object>? _streamHandlers;

    // Filled at runtime for polymorphic dispatch (derived-type requests).
    private readonly ConcurrentDictionary<Type, object> _polymorphicCache = new();

    public void Register(Type requestType, object handlerDelegate) {
        if(this._mutableRequestHandlers is null)
            throw new InvalidOperationException("Registry is already frozen.");

        this._mutableRequestHandlers[requestType] = handlerDelegate;
    }

    public void RegisterStream(Type requestType, object handlerDelegate) {
        if(this._mutableRequestHandlers is null)
            throw new InvalidOperationException("Registry is already frozen.");

        this._mutableStreamHandlers ??= [];
        this._mutableStreamHandlers[requestType] = handlerDelegate;
    }

    public void ToFrozen() {
        if(this._mutableRequestHandlers is null)
            return;

        this._requestHandlers = this._mutableRequestHandlers.ToFrozenDictionary();
        this._streamHandlers = this._mutableStreamHandlers?.ToFrozenDictionary();

        // Release mutable dictionaries to GC after freezing.
        this._mutableRequestHandlers = null;
        this._mutableStreamHandlers = null;
    }

    public Func<IServiceProvider, object, CancellationToken, Task<TResponse>> GetRequestHandler<TResponse>(Type requestType) {
        // 1. Exact match (Fastest path: concrete FrozenDictionary call)
        if(this._requestHandlers!.TryGetValue(requestType, out object? handler))
            return (Func<IServiceProvider, object, CancellationToken, Task<TResponse>>)handler;

        // 2. Polymorphic cache (Fast path: concrete ConcurrentDictionary call)
        if(this._polymorphicCache.TryGetValue(requestType, out object? cached))
            return (Func<IServiceProvider, object, CancellationToken, Task<TResponse>>)cached;

        // 3. Polymorphic scan (First call only - avoids LINQ allocations)
        Type? baseMatch = null;
        foreach(Type k in this._requestHandlers.Keys) {
            if(k.IsAssignableFrom(requestType)) {
                baseMatch = k;
                break;
            }
        }

        if(baseMatch is not null) {
            object baseHandler = this._requestHandlers[baseMatch];
            this._polymorphicCache.TryAdd(requestType, baseHandler);
            return (Func<IServiceProvider, object, CancellationToken, Task<TResponse>>)baseHandler;
        }

        throw new InvalidOperationException(
            $"No request handler registered for: {requestType.FullName}");
    }

    public Func<IServiceProvider, object, CancellationToken, IAsyncEnumerable<TResponse>> GetStreamHandler<TResponse>(Type requestType) {
        if(this._streamHandlers is not null && this._streamHandlers.TryGetValue(requestType, out object? handler))
            return (Func<IServiceProvider, object, CancellationToken, IAsyncEnumerable<TResponse>>)handler;

        throw new InvalidOperationException(
            $"No stream handler registered for: {requestType.FullName}");
    }
}