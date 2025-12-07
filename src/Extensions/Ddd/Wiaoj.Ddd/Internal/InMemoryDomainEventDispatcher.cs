using Microsoft.Extensions.DependencyInjection;
using Wiaoj.Ddd;
using Wiaoj.Ddd.Abstractions;
using Wiaoj.Ddd.Abstractions.DomainEvents;

namespace Wiaoj.Ddd.Internal;
public sealed class InMemoryDomainEventDispatcher(IServiceProvider serviceProvider) : IDomainEventDispatcher {
    public async ValueTask DispatchPreCommitAsync<TDomainEvent>( TDomainEvent @event, CancellationToken cancellationToken)
        where TDomainEvent : IDomainEvent {
        IEnumerable<IPreDomainEventHandler<TDomainEvent>> handlers = serviceProvider.GetServices<IPreDomainEventHandler<TDomainEvent>>();
        
        foreach (IPreDomainEventHandler<TDomainEvent> handler in handlers) {
            await handler.Handle(@event, cancellationToken);
        }
    }

    public async ValueTask DispatchPostCommitAsync<TDomainEvent>( TDomainEvent @event, CancellationToken cancellationToken)
        where TDomainEvent : IDomainEvent {
        IEnumerable<IPostDomainEventHandler<TDomainEvent>> handlers = 
            serviceProvider.GetServices<IPostDomainEventHandler<TDomainEvent>>();

        foreach (IPostDomainEventHandler<TDomainEvent> handler in handlers) {
            await handler.Handle(@event, cancellationToken);
        }
    }
}