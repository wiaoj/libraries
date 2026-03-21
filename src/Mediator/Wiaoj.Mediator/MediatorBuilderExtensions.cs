using Microsoft.Extensions.DependencyInjection;

namespace Wiaoj.Mediator;
/// <summary>
/// Extension methods for safely registering behaviors, handlers, and processors
/// without creating duplicates (useful in modular monolith / library scenarios).
/// </summary>
public static class MediatorBuilderExtensions {
    // ═════════════════════════════════════════════════════════════════════════
    // OPEN BEHAVIORS (all requests)
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Adds an open behavior only if it has not been registered yet.
    /// </summary>
    public static IMediatorBuilder TryAddOpenBehavior<TBehavior>(
        this IMediatorBuilder builder, ServiceLifetime lifetime = ServiceLifetime.Scoped) {
        if(!builder.HasBehavior(typeof(TBehavior)))
            builder.AddOpenBehavior<TBehavior>(lifetime);
        return builder;
    }

    // ═════════════════════════════════════════════════════════════════════════
    // COMMAND BEHAVIORS
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Adds a command behavior only if it has not been registered yet.
    /// </summary>
    public static IMediatorBuilder TryAddCommandBehavior<TBehavior>(
        this IMediatorBuilder builder, ServiceLifetime lifetime = ServiceLifetime.Scoped) {
        if(!builder.HasBehavior(typeof(TBehavior)))
            builder.AddCommandBehavior<TBehavior>(lifetime);
        return builder;
    }

    // ═════════════════════════════════════════════════════════════════════════
    // QUERY BEHAVIORS
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Adds a query behavior only if it has not been registered yet.
    /// </summary>
    public static IMediatorBuilder TryAddQueryBehavior<TBehavior>(
        this IMediatorBuilder builder, ServiceLifetime lifetime = ServiceLifetime.Scoped) {
        if(!builder.HasBehavior(typeof(TBehavior)))
            builder.AddQueryBehavior<TBehavior>(lifetime);
        return builder;
    }

    // ═════════════════════════════════════════════════════════════════════════
    // STREAM BEHAVIORS
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Adds a stream behavior only if it has not been registered yet.
    /// </summary>
    public static IMediatorBuilder TryAddStreamBehavior<TBehavior>(
        this IMediatorBuilder builder, ServiceLifetime lifetime = ServiceLifetime.Scoped) {
        if(!builder.HasBehavior(typeof(TBehavior)))
            builder.AddStreamBehavior<TBehavior>(lifetime);
        return builder;
    }

    // ═════════════════════════════════════════════════════════════════════════
    // HANDLERS
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Registers a handler only if it has not been registered yet.
    /// </summary>
    public static IMediatorBuilder TryRegisterHandler<THandler>(this IMediatorBuilder builder) {
        if(!builder.HasHandler(typeof(THandler)))
            builder.RegisterHandler<THandler>();
        return builder;
    }

    // ═════════════════════════════════════════════════════════════════════════
    // PRE-PROCESSORS
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Registers a pre-processor only if it has not been registered yet.
    /// </summary>
    public static IMediatorBuilder TryRegisterPreProcessor<TPreProcessor>(this IMediatorBuilder builder) {
        if(!builder.HasPreProcessor(typeof(TPreProcessor)))
            builder.RegisterPreProcessor<TPreProcessor>();
        return builder;
    }

    // ═════════════════════════════════════════════════════════════════════════
    // POST-PROCESSORS
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Registers a post-processor only if it has not been registered yet.
    /// </summary>
    public static IMediatorBuilder TryRegisterPostProcessor<TPostProcessor>(this IMediatorBuilder builder) {
        if(!builder.HasPostProcessor(typeof(TPostProcessor)))
            builder.RegisterPostProcessor<TPostProcessor>();
        return builder;
    }
}