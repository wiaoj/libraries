using Microsoft.Extensions.DependencyInjection;

namespace Wiaoj.Mediator;
/// <summary>
/// Extension methods for safely adding behaviors and handlers to the Mediator builder.
/// </summary>
public static class MediatorBuilderExtensions {

    // =================================================================================================
    // OPEN BEHAVIORS (ALL REQUESTS)
    // =================================================================================================

    /// <summary>
    /// Adds a pipeline behavior that applies to ALL request types (Command, Query, Stream), 
    /// only if it hasn't been registered yet.
    /// </summary>
    /// <typeparam name="TBehavior">The type of the behavior to add.</typeparam>
    /// <param name="builder">The mediator builder.</param>
    /// <param name="lifetime">The service lifetime for the behavior.</param>
    /// <returns>The builder instance.</returns>
    public static IMediatorBuilder TryAddOpenBehavior<TBehavior>(this IMediatorBuilder builder, ServiceLifetime lifetime) {
        if(!builder.HasBehavior(typeof(TBehavior))) {
            builder.AddOpenBehavior<TBehavior>(lifetime);
        }
        return builder;
    }

    /// <inheritdoc cref="TryAddOpenBehavior{TBehavior}(IMediatorBuilder, ServiceLifetime)"/>
    /// <remarks>
    /// Registers with <see cref="ServiceLifetime.Scoped"/> by default.
    /// </remarks>
    public static IMediatorBuilder TryAddOpenBehavior<TBehavior>(this IMediatorBuilder builder) {
        return builder.TryAddOpenBehavior<TBehavior>(ServiceLifetime.Scoped);
    }

    // =================================================================================================
    // COMMAND BEHAVIORS
    // =================================================================================================

    /// <summary>
    /// Adds a pipeline behavior that applies ONLY to <see cref="ICommand{TResponse}"/> types,
    /// only if it hasn't been registered yet.
    /// </summary>
    /// <typeparam name="TBehavior">The type of the behavior to add.</typeparam>
    /// <param name="builder">The mediator builder.</param>
    /// <param name="lifetime">The service lifetime for the behavior.</param>
    /// <returns>The builder instance.</returns>
    public static IMediatorBuilder TryAddCommandBehavior<TBehavior>(this IMediatorBuilder builder, ServiceLifetime lifetime) {
        if(!builder.HasBehavior(typeof(TBehavior))) {
            builder.AddCommandBehavior<TBehavior>(lifetime);
        }
        return builder;
    }

    /// <inheritdoc cref="TryAddCommandBehavior{TBehavior}(IMediatorBuilder, ServiceLifetime)"/>
    /// <remarks>
    /// Registers with <see cref="ServiceLifetime.Scoped"/> by default.
    /// </remarks>
    public static IMediatorBuilder TryAddCommandBehavior<TBehavior>(this IMediatorBuilder builder) {
        return builder.TryAddCommandBehavior<TBehavior>(ServiceLifetime.Scoped);
    }

    // =================================================================================================
    // QUERY BEHAVIORS
    // =================================================================================================

    /// <summary>
    /// Adds a pipeline behavior that applies ONLY to <see cref="IQuery{TResponse}"/> types,
    /// only if it hasn't been registered yet.
    /// </summary>
    /// <typeparam name="TBehavior">The type of the behavior to add.</typeparam>
    /// <param name="builder">The mediator builder.</param>
    /// <param name="lifetime">The service lifetime for the behavior.</param>
    /// <returns>The builder instance.</returns>
    public static IMediatorBuilder TryAddQueryBehavior<TBehavior>(this IMediatorBuilder builder, ServiceLifetime lifetime) {
        if(!builder.HasBehavior(typeof(TBehavior))) {
            builder.AddQueryBehavior<TBehavior>(lifetime);
        }
        return builder;
    }

    /// <inheritdoc cref="TryAddQueryBehavior{TBehavior}(IMediatorBuilder, ServiceLifetime)"/>
    /// <remarks>
    /// Registers with <see cref="ServiceLifetime.Scoped"/> by default.
    /// </remarks>
    public static IMediatorBuilder TryAddQueryBehavior<TBehavior>(this IMediatorBuilder builder) {
        return builder.TryAddQueryBehavior<TBehavior>(ServiceLifetime.Scoped);
    }

    // =================================================================================================
    // STREAM BEHAVIORS
    // =================================================================================================

    /// <summary>
    /// Adds a pipeline behavior that applies ONLY to <see cref="IStreamRequest{TResponse}"/> types,
    /// only if it hasn't been registered yet.
    /// </summary>
    /// <typeparam name="TBehavior">The type of the behavior to add.</typeparam>
    /// <param name="builder">The mediator builder.</param>
    /// <param name="lifetime">The service lifetime for the behavior.</param>
    /// <returns>The builder instance.</returns>
    public static IMediatorBuilder TryAddStreamBehavior<TBehavior>(this IMediatorBuilder builder, ServiceLifetime lifetime) {
        if(!builder.HasBehavior(typeof(TBehavior))) {
            builder.AddStreamBehavior<TBehavior>(lifetime);
        }
        return builder;
    }

    /// <inheritdoc cref="TryAddStreamBehavior{TBehavior}(IMediatorBuilder, ServiceLifetime)"/>
    /// <remarks>
    /// Registers with <see cref="ServiceLifetime.Scoped"/> by default.
    /// </remarks>
    public static IMediatorBuilder TryAddStreamBehavior<TBehavior>(this IMediatorBuilder builder) {
        return builder.TryAddStreamBehavior<TBehavior>(ServiceLifetime.Scoped);
    }

    // =================================================================================================
    // HANDLER REGISTRATION
    // =================================================================================================

    /// <summary>
    /// Registers the handler manually only if it hasn't been registered yet.
    /// </summary>
    /// <typeparam name="THandler">The type of the handler to register.</typeparam>
    /// <param name="builder">The mediator builder.</param>
    /// <returns>The builder instance.</returns>
    public static IMediatorBuilder TryRegisterHandler<THandler>(this IMediatorBuilder builder) {
        if(!builder.HasHandler(typeof(THandler))) {
            builder.RegisterHandler<THandler>();
        }
        return builder;
    }
}