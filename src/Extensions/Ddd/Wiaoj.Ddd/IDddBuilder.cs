using Microsoft.Extensions.DependencyInjection;
using Wiaoj.Ddd.DomainEvents;

namespace Wiaoj.Ddd;
public interface IDddBuilder {
    IServiceCollection Services { get; }

    IDddBuilder AddPreDomainEventHandler<TDomainEvent, THandler>(ServiceLifetime serviceLifetime)
        where TDomainEvent : IDomainEvent
        where THandler : class, IPreDomainEventHandler<TDomainEvent>;

    IDddBuilder AddPostDomainEventHandler<TDomainEvent, THandler>(ServiceLifetime serviceLifetime)
        where TDomainEvent : IDomainEvent
        where THandler : class, IPostDomainEventHandler<TDomainEvent>;
}