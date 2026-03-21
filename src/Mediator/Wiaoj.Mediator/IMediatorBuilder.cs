using Microsoft.Extensions.DependencyInjection;

namespace Wiaoj.Mediator;
/// <summary>
/// Defines a builder for configuring the Mediator, including handlers, behaviors, and diagnostic options.
/// </summary>
public interface IMediatorBuilder {
    /// <summary>
    /// Sets the default service lifetime for registered handlers.
    /// <br/>
    /// The default value is <see cref="ServiceLifetime.Scoped"/>.
    /// </summary>
    IMediatorBuilder WithDefaultLifetime(ServiceLifetime lifetime);

    /// <summary>
    /// Enables OpenTelemetry tracing via <see cref="System.Diagnostics.ActivitySource"/>.
    /// </summary>
    IMediatorBuilder WithOpenTelemetry();

    // --- HANDLER REGISTRATION ---

    /// <summary>
    /// Scans the assembly containing the specified marker type for implementations of <see cref="IRequestHandler{TRequest,TResponse}"/> 
    /// and <see cref="IStreamRequestHandler{TRequest,TResponse}"/>, and registers them.
    /// </summary>
    IMediatorBuilder RegisterHandlersFromAssemblyContaining<TMarker>();

    /// <summary>
    /// Manually registers a specific handler type. 
    /// </summary>
    IMediatorBuilder RegisterHandler<THandler>();

    // --- BEHAVIOR REGISTRATION ---

    /// <summary>
    /// Adds a pipeline behavior that applies to ALL request types (Command, Query, Stream), provided generic constraints are met.
    /// </summary>
    IMediatorBuilder AddOpenBehavior(Type behaviorType, ServiceLifetime lifetime = ServiceLifetime.Scoped);

    /// <summary>
    /// Adds a pipeline behavior that applies to ALL request types (Command, Query, Stream).
    /// </summary>
    IMediatorBuilder AddOpenBehavior<TBehavior>(ServiceLifetime lifetime = ServiceLifetime.Scoped);

    /// <summary>
    /// Adds a pipeline behavior that applies ONLY to <see cref="ICommand{TResponse}"/> types.
    /// </summary>
    IMediatorBuilder AddCommandBehavior(Type behaviorType, ServiceLifetime lifetime = ServiceLifetime.Scoped);

    /// <summary>
    /// Adds a pipeline behavior that applies ONLY to <see cref="ICommand{TResponse}"/> types.
    /// </summary>
    IMediatorBuilder AddCommandBehavior<TBehavior>(ServiceLifetime lifetime = ServiceLifetime.Scoped);

    /// <summary>
    /// Adds a pipeline behavior that applies ONLY to <see cref="IQuery{TResponse}"/> types.
    /// </summary>
    IMediatorBuilder AddQueryBehavior(Type behaviorType, ServiceLifetime lifetime = ServiceLifetime.Scoped);

    /// <summary>
    /// Adds a pipeline behavior that applies ONLY to <see cref="IQuery{TResponse}"/> types.
    /// </summary>
    IMediatorBuilder AddQueryBehavior<TBehavior>(ServiceLifetime lifetime = ServiceLifetime.Scoped);

    /// <summary>
    /// Adds a pipeline behavior that applies ONLY to <see cref="IStreamRequest{TResponse}"/> types.
    /// </summary>
    IMediatorBuilder AddStreamBehavior(Type behaviorType, ServiceLifetime lifetime = ServiceLifetime.Scoped);

    /// <summary>
    /// Adds a pipeline behavior that applies ONLY to <see cref="IStreamRequest{TResponse}"/> types.
    /// </summary>
    IMediatorBuilder AddStreamBehavior<TBehavior>(ServiceLifetime lifetime = ServiceLifetime.Scoped);

    bool HasBehavior(Type behaviorType);
    bool HasHandler(Type handlerType);
}