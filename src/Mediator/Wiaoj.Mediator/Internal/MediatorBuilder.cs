using Microsoft.Extensions.DependencyInjection;

namespace Wiaoj.Mediator.Internal;
internal enum BehaviorCategory { All, Command, Query, Stream }
internal sealed record BehaviorRegistryItem(Type Type, BehaviorCategory Category, ServiceLifetime Lifetime);

internal sealed class MediatorBuilder(IServiceCollection services) : IMediatorBuilder {
    public List<Type> AssemblyMarkers { get; } = [];
    public List<Type> ManualHandlers { get; } = [];

    public List<BehaviorRegistryItem> Behaviors { get; } = [];

    public ServiceLifetime DefaultLifetime { get; private set; } = ServiceLifetime.Scoped;
    public bool IsTracingEnabled { get; private set; } = false;

    public IMediatorBuilder WithDefaultLifetime(ServiceLifetime lifetime) {
        this.DefaultLifetime = lifetime;
        return this;
    }

    public IMediatorBuilder WithOpenTelemetry() {
        this.IsTracingEnabled = true;
        return this;
    }

    public IMediatorBuilder RegisterHandlersFromAssemblyContaining<TMarker>() {
        this.AssemblyMarkers.Add(typeof(TMarker));
        return this;
    }

    public IMediatorBuilder RegisterHandler<THandler>() {
        this.ManualHandlers.Add(typeof(THandler));
        return this;
    }

    // --- BEHAVIOR IMPLEMENTATIONS ---

    public IMediatorBuilder AddOpenBehavior(Type behaviorType, ServiceLifetime lifetime) {
        this.Behaviors.Add(new BehaviorRegistryItem(behaviorType, BehaviorCategory.All, lifetime));
        return this;
    }

    public IMediatorBuilder AddOpenBehavior<TBehavior>(ServiceLifetime lifetime) {
        return AddOpenBehavior(typeof(TBehavior), lifetime);
    }

    public IMediatorBuilder AddCommandBehavior(Type behaviorType, ServiceLifetime lifetime) {
        this.Behaviors.Add(new BehaviorRegistryItem(behaviorType, BehaviorCategory.Command, lifetime));
        return this;
    }

    public IMediatorBuilder AddCommandBehavior<TBehavior>(ServiceLifetime lifetime) {
        return AddCommandBehavior(typeof(TBehavior), lifetime);
    }

    public IMediatorBuilder AddQueryBehavior(Type behaviorType, ServiceLifetime lifetime) {
        this.Behaviors.Add(new BehaviorRegistryItem(behaviorType, BehaviorCategory.Query, lifetime));
        return this;
    }

    public IMediatorBuilder AddQueryBehavior<TBehavior>(ServiceLifetime lifetime) {
        return AddQueryBehavior(typeof(TBehavior), lifetime);
    }

    public IMediatorBuilder AddStreamBehavior(Type behaviorType, ServiceLifetime lifetime) {
        this.Behaviors.Add(new BehaviorRegistryItem(behaviorType, BehaviorCategory.Stream, lifetime));
        return this;
    }

    public IMediatorBuilder AddStreamBehavior<TBehavior>(ServiceLifetime lifetime) {
        return AddStreamBehavior(typeof(TBehavior), lifetime);
    }

    public bool HasBehavior(Type behaviorType) {
        return Behaviors.Any(b => b.Type == behaviorType);
    }

    public bool HasHandler(Type handlerType) {
        return ManualHandlers.Contains(handlerType)
               || AssemblyMarkers.Any(marker => marker.Assembly == handlerType.Assembly);
    }
}