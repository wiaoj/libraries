using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using Wiaoj.Ddd;
using Wiaoj.Ddd.DomainEvents;
using Wiaoj.Preconditions;

namespace Wiaoj.Ddd.Extensions;
public static class DomainEventDispatcherExtensions {
    // Delegate Tanımı: Dispatcher, Event ve Token alır -> ValueTask döner
    private delegate ValueTask DispatchDelegate(
        IDomainEventDispatcher dispatcher, 
        IDomainEvent @event, 
        CancellationToken cancellationToken);

    // Cache: Event Tipi -> Derlenmiş Metot (Pre ve Post için ayrı)
    private static readonly ConcurrentDictionary<Type, DispatchDelegate> _preCommitCache = new();
    private static readonly ConcurrentDictionary<Type, DispatchDelegate> _postCommitCache = new();

    /// <summary>
    /// Uses cached Expression Trees to invoke DispatchPreCommitAsync without 'dynamic'.
    /// </summary>
    public static ValueTask DispatchPreCommitCompiledAsync(this IDomainEventDispatcher dispatcher,
                                                           IDomainEvent @event,
                                                           CancellationToken cancellationToken) {
        Type eventType = @event.GetType();

        // Cache'ten delegemizi alıyoruz, yoksa oluşturuyoruz
        DispatchDelegate handler = _preCommitCache.GetOrAdd(eventType, t => BuildDelegate(t, nameof(IDomainEventDispatcher.DispatchPreCommitAsync)));

        return handler(dispatcher, @event, cancellationToken);
    }

    /// <summary>
    /// Uses cached Expression Trees to invoke DispatchPostCommitAsync without 'dynamic'.
    /// </summary>
    public static ValueTask DispatchPostCommitCompiledAsync(this IDomainEventDispatcher dispatcher,
                                                            IDomainEvent @event,
                                                            CancellationToken cancellationToken) {
        Type eventType = @event.GetType();

        DispatchDelegate handler = _postCommitCache.GetOrAdd(eventType, t => BuildDelegate(t, nameof(IDomainEventDispatcher.DispatchPostCommitAsync)));

        return handler(dispatcher, @event, cancellationToken);
    }

    // --- Expression Tree Oluşturucu ---
    private static DispatchDelegate BuildDelegate(Type eventType, string methodName) {
        // Parametreler: (IDomainEventDispatcher dispatcher, IDomainEvent @event, CancellationToken cancellationToken)
        ParameterExpression dispatcherParam = Expression.Parameter(typeof(IDomainEventDispatcher), "dispatcher");
        ParameterExpression eventParam = Expression.Parameter(typeof(IDomainEvent), "@event");
        ParameterExpression ctParam = Expression.Parameter(typeof(CancellationToken), "cancellationToken");

        // 1. Generic Metodu Bul: DispatchPreCommitAsync<T>(...)
        MethodInfo? genericMethod = typeof(IDomainEventDispatcher)
            .GetMethods()
            .FirstOrDefault(m => m.Name == methodName && m.IsGenericMethod);

        Preca.ThrowIfNull(
            genericMethod,
            (methodName) => new InvalidOperationException($"Method '{methodName}' not found on IDomainEventDispatcher."),
            methodName);

        // 2. Metodu Event Tipiyle Özelleştir: DispatchPreCommitAsync<VisitorCreatedDomainEvent>(...)
        MethodInfo concreteMethod = genericMethod.MakeGenericMethod(eventType);

        // 3. Event'i Cast Et: (VisitorCreatedDomainEvent)@event
        UnaryExpression castedEvent = Expression.Convert(eventParam, eventType);

        // 4. Metodu Çağır: dispatcher.Metot(castedEvent, cancellationToken)
        MethodCallExpression call = Expression.Call(dispatcherParam, concreteMethod, castedEvent, ctParam);

        // 5. Lambda Oluştur ve Derle: (d, e, c) => d.Metot((T)e, c)
        return Expression.Lambda<DispatchDelegate>(call, dispatcherParam, eventParam, ctParam).Compile();
    }
}