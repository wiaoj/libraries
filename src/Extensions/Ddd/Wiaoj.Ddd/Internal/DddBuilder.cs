using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Wiaoj.Ddd.Abstractions;
using Wiaoj.Ddd.Abstractions.DomainEvents;

namespace Wiaoj.Ddd.Internal;

internal sealed class DddBuilder(IServiceCollection services) : IDddBuilder {
    public IServiceCollection Services { get; } = services;
      
    public IDddBuilder AddPreDomainEventHandler<TDomainEvent, THandler>(ServiceLifetime serviceLifetime)
      where TDomainEvent : IDomainEvent
      where THandler : class, IPreDomainEventHandler<TDomainEvent> {
        Services.TryAddEnumerable(new ServiceDescriptor(typeof(IPreDomainEventHandler<TDomainEvent>), typeof(THandler), serviceLifetime));
        return this;
    }

    public IDddBuilder AddPostDomainEventHandler<TDomainEvent, THandler>(ServiceLifetime serviceLifetime)
        where TDomainEvent : IDomainEvent
        where THandler : class, IPostDomainEventHandler<TDomainEvent> {
        Services.TryAddEnumerable(new ServiceDescriptor(typeof(IPostDomainEventHandler<TDomainEvent>), typeof(THandler), serviceLifetime));
        return this;
    }
} 