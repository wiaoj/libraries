using Microsoft.Extensions.DependencyInjection;

namespace Wiaoj.Mediator.Internal;

internal enum BehaviorCategory {
    All,
    Command,
    Query,
    Stream
}

internal sealed record BehaviorRegistryItem(Type Type, BehaviorCategory Category, ServiceLifetime Lifetime);

internal sealed class MediatorBuilder(IServiceCollection services) : IMediatorBuilder {
    public List<Type> AssemblyMarkers { get; } = [];
    public List<Type> ManualHandlers { get; } = [];
    public List<Type> ManualPreProcessors { get; } = [];
    public List<Type> ManualPostProcessors { get; } = [];
    public List<BehaviorRegistryItem> Behaviors { get; } = [];

    public ServiceLifetime DefaultLifetime { get; private set; } = ServiceLifetime.Scoped;
    public bool IsTracingEnabled { get; private set; } = false;

    // ── Lifetime / Diagnostics ──────────────────────────────────────────────

    public IMediatorBuilder WithDefaultLifetime(ServiceLifetime lifetime) {
        this.DefaultLifetime = lifetime;
        return this;
    }

    public IMediatorBuilder WithOpenTelemetry() {
        this.IsTracingEnabled = true;
        return this;
    }

    // ── Handler Registration ────────────────────────────────────────────────

    public IMediatorBuilder RegisterHandlersFromAssemblyContaining<TMarker>() {
        this.AssemblyMarkers.Add(typeof(TMarker));
        return this;
    }

    public IMediatorBuilder RegisterHandler<THandler>() {
        this.ManualHandlers.Add(typeof(THandler));
        return this;
    }

    // ── Processor Registration ──────────────────────────────────────────────

    public IMediatorBuilder RegisterPreProcessor<TPreProcessor>() {
        this.ManualPreProcessors.Add(typeof(TPreProcessor));
        return this;
    }

    public IMediatorBuilder RegisterPostProcessor<TPostProcessor>() {
        this.ManualPostProcessors.Add(typeof(TPostProcessor));
        return this;
    }

    // ── Behavior Registration ───────────────────────────────────────────────

    public IMediatorBuilder AddOpenBehavior(Type behaviorType, ServiceLifetime lifetime = ServiceLifetime.Scoped) {
        this.Behaviors.Add(new BehaviorRegistryItem(behaviorType, BehaviorCategory.All, lifetime));
        return this;
    }

    public IMediatorBuilder AddOpenBehavior<TBehavior>(ServiceLifetime lifetime = ServiceLifetime.Scoped) {
        return AddOpenBehavior(typeof(TBehavior), lifetime);
    }

    public IMediatorBuilder AddCommandBehavior(Type behaviorType, ServiceLifetime lifetime = ServiceLifetime.Scoped) {
        this.Behaviors.Add(new BehaviorRegistryItem(behaviorType, BehaviorCategory.Command, lifetime));
        return this;
    }

    public IMediatorBuilder AddCommandBehavior<TBehavior>(ServiceLifetime lifetime = ServiceLifetime.Scoped) {
        return AddCommandBehavior(typeof(TBehavior), lifetime);
    }

    public IMediatorBuilder AddQueryBehavior(Type behaviorType, ServiceLifetime lifetime = ServiceLifetime.Scoped) {
        this.Behaviors.Add(new BehaviorRegistryItem(behaviorType, BehaviorCategory.Query, lifetime));
        return this;
    }

    public IMediatorBuilder AddQueryBehavior<TBehavior>(ServiceLifetime lifetime = ServiceLifetime.Scoped) {
        return AddQueryBehavior(typeof(TBehavior), lifetime);
    }

    public IMediatorBuilder AddStreamBehavior(Type behaviorType, ServiceLifetime lifetime = ServiceLifetime.Scoped) {
        this.Behaviors.Add(new BehaviorRegistryItem(behaviorType, BehaviorCategory.Stream, lifetime));
        return this;
    }

    public IMediatorBuilder AddStreamBehavior<TBehavior>(ServiceLifetime lifetime = ServiceLifetime.Scoped) {
        return AddStreamBehavior(typeof(TBehavior), lifetime);
    }

    // ── Introspection ────────────────────────────────────────────────────────

    public bool HasBehavior(Type behaviorType) {
        return this.Behaviors.Any(b => b.Type == behaviorType);
    }

    public bool HasHandler(Type handlerType) {
        return this.ManualHandlers.Contains(handlerType)
                                                     || this.AssemblyMarkers.Any(m => m.Assembly == handlerType.Assembly);
    }

    public bool HasPreProcessor(Type processorType) {
        return this.ManualPreProcessors.Contains(processorType)
                                                            || this.AssemblyMarkers.Any(m => m.Assembly == processorType.Assembly);
    }

    public bool HasPostProcessor(Type processorType) {
        return this.ManualPostProcessors.Contains(processorType)
                                                             || this.AssemblyMarkers.Any(m => m.Assembly == processorType.Assembly);
    }
}