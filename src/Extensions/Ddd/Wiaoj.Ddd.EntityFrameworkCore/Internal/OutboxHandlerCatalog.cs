using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;
using System.Reflection;
using Wiaoj.Ddd.DomainEvents;
using Wiaoj.Ddd.EntityFrameworkCore.Outbox;

namespace Wiaoj.Ddd.EntityFrameworkCore.Internal;

/// <summary>
/// Answers "which post-commit handlers exist for this event type", and dispatches to one of them by alias.
/// </summary>
/// <remarks>
/// The outbox writes one row per (event, handler), so it needs the handler set at enqueue time and a single
/// handler at dispatch time. Both go through here, keyed by the alias that gets persisted.
/// </remarks>
internal sealed class OutboxHandlerCatalog(IOutboxAliasRegistry aliases) {
    private static readonly Type HandlerDefinition = typeof(IPostDomainEventHandler<>);

    private readonly ConcurrentDictionary<Type, string[]> _aliasesByEventType = new();
    private readonly ConcurrentDictionary<Type, MethodInfo> _handleMethods = new();

    /// <summary>
    /// Returns the aliases of every post-commit handler registered for <paramref name="eventType"/>.
    /// </summary>
    /// <remarks>
    /// Resolved from the container once per event type and cached. The handler set is therefore read at the
    /// first event of a type and fixed for the life of the process, which matches how the rows behave: a
    /// handler added by a later deployment does not retroactively gain rows for events already enqueued.
    /// </remarks>
    public string[] GetHandlerAliases(IServiceProvider serviceProvider, Type eventType) {
        return this._aliasesByEventType.GetOrAdd(eventType, static (type, state) => {
            Type handlerType = HandlerDefinition.MakeGenericType(type);

            return [.. state.Provider.GetServices(handlerType)
                .Where(handler => handler is not null)
                .Select(handler => state.Aliases.GetHandlerAlias(handler!.GetType()))
                .Distinct(StringComparer.Ordinal)];
        }, (Provider: serviceProvider, Aliases: aliases));
    }

    /// <summary>
    /// Invokes the single handler identified by <paramref name="handlerAlias"/>.
    /// </summary>
    /// <returns>
    /// <see langword="false"/> when no registered handler carries that alias — the handler was removed or
    /// renamed since the row was written, which is a dead-letter, not a retry.
    /// </returns>
    public async Task<bool> TryDispatchAsync(
        IServiceProvider serviceProvider,
        IDomainEvent domainEvent,
        string handlerAlias,
        CancellationToken cancellationToken) {

        Type eventType = domainEvent.GetType();
        Type handlerType = HandlerDefinition.MakeGenericType(eventType);

        foreach(object? handler in serviceProvider.GetServices(handlerType)) {
            if(handler is null || !string.Equals(aliases.GetHandlerAlias(handler.GetType()), handlerAlias, StringComparison.Ordinal)) {
                continue;
            }

            MethodInfo handle = this._handleMethods.GetOrAdd(handlerType, static type =>
                type.GetMethod(nameof(IPostDomainEventHandler<IDomainEvent>.Handle))!);

            object? result = handle.Invoke(handler, [domainEvent, cancellationToken]);

            if(result is ValueTask task) {
                await task.ConfigureAwait(false);
            }

            return true;
        }

        return false;
    }
}
