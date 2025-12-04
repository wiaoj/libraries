using Microsoft.Extensions.DependencyInjection;
using Wiaoj.Ddd;
using Wiaoj.Ddd.Abstractions;
using Wiaoj.Ddd.Abstractions.DomainEvents;

namespace Wiaoj.Ddd.Internal;
public sealed class InMemoryDomainEventDispatcher(IServiceProvider serviceProvider) : IDomainEventDispatcher {
    public async ValueTask DispatchPreCommitAsync<TDomainEvent>( TDomainEvent @event, CancellationToken cancellationToken)
        where TDomainEvent : IDomainEvent {
        // Pre-commit handler'ları çöz ve çalıştır
        IEnumerable<IPreDomainEventHandler<TDomainEvent>> handlers = serviceProvider.GetServices<IPreDomainEventHandler<TDomainEvent>>();
        foreach (IPreDomainEventHandler<TDomainEvent> handler in handlers) {
            await handler.Handle(@event, cancellationToken);
        }
    }

    public async ValueTask DispatchPostCommitAsync<TDomainEvent>( TDomainEvent @event, CancellationToken cancellationToken)
        where TDomainEvent : IDomainEvent {
        // Post-commit handler'ları çöz ve çalıştır
        // Post-commit işlemleri genellikle ana işlemden bağımsız olduğu için yeni bir scope'ta çalıştırılması tavsiye edilir.
        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();
        IEnumerable<IPostDomainEventHandler<TDomainEvent>> handlers = scope.ServiceProvider.GetServices<IPostDomainEventHandler<TDomainEvent>>();
        foreach (IPostDomainEventHandler<TDomainEvent> handler in handlers) {
            await handler.Handle(@event, cancellationToken);
        }
    }
}