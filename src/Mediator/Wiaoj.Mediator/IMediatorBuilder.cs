using Microsoft.Extensions.DependencyInjection;

namespace Wiaoj.Mediator;
/// <summary>
/// Defines a builder for configuring the Mediator, including handlers, behaviors,
/// processors, and diagnostic options.
/// </summary>
public interface IMediatorBuilder {

    // ─────────────────────────────────────────────────────────────────────────
    // Lifetime &amp; Diagnostics
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Sets the default service lifetime for registered handlers and processors.
    /// <br/>Default: <see cref="ServiceLifetime.Scoped"/>.
    /// </summary>
    IMediatorBuilder WithDefaultLifetime(ServiceLifetime lifetime);

    /// <summary>
    /// Enables OpenTelemetry tracing via <see cref="System.Diagnostics.ActivitySource"/>.
    /// Registers <c>TracingMediator</c> instead of the default <c>Mediator</c> (zero overhead when disabled).
    /// </summary>
    IMediatorBuilder WithOpenTelemetry();

    // ─────────────────────────────────────────────────────────────────────────
    // Handler Registration
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Scans the assembly containing <typeparamref name="TMarker"/> for all
    /// <see cref="IRequestHandler{TRequest,TResponse}"/>, <see cref="IStreamRequestHandler{TRequest,TResponse}"/>,
    /// <see cref="IRequestPreProcessor{TRequest,TResponse}"/>, and
    /// <see cref="IRequestPostProcessor{TRequest,TResponse}"/> implementations and registers them.
    /// </summary>
    IMediatorBuilder RegisterHandlersFromAssemblyContaining<TMarker>();

    /// <summary>Manually registers a specific handler type.</summary>
    IMediatorBuilder RegisterHandler<THandler>();

    // ─────────────────────────────────────────────────────────────────────────
    // Processor Registration (manual, for when assembly scanning is not used)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Manually registers a specific pre-processor type.</summary>
    IMediatorBuilder RegisterPreProcessor<TPreProcessor>();

    /// <summary>Manually registers a specific post-processor type.</summary>
    IMediatorBuilder RegisterPostProcessor<TPostProcessor>();

    // ─────────────────────────────────────────────────────────────────────────
    // Behavior Registration
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Adds a behavior that applies to ALL request types (Command, Query, Stream).</summary>
    IMediatorBuilder AddOpenBehavior(Type behaviorType, ServiceLifetime lifetime = ServiceLifetime.Scoped);

    /// <inheritdoc cref="AddOpenBehavior(Type, ServiceLifetime)"/>
    IMediatorBuilder AddOpenBehavior<TBehavior>(ServiceLifetime lifetime = ServiceLifetime.Scoped);

    /// <summary>Adds a behavior that applies ONLY to <see cref="ICommand{TResponse}"/> types.</summary>
    IMediatorBuilder AddCommandBehavior(Type behaviorType, ServiceLifetime lifetime = ServiceLifetime.Scoped);

    /// <inheritdoc cref="AddCommandBehavior(Type, ServiceLifetime)"/>
    IMediatorBuilder AddCommandBehavior<TBehavior>(ServiceLifetime lifetime = ServiceLifetime.Scoped);

    /// <summary>Adds a behavior that applies ONLY to <see cref="IQuery{TResponse}"/> types.</summary>
    IMediatorBuilder AddQueryBehavior(Type behaviorType, ServiceLifetime lifetime = ServiceLifetime.Scoped);

    /// <inheritdoc cref="AddQueryBehavior(Type, ServiceLifetime)"/>
    IMediatorBuilder AddQueryBehavior<TBehavior>(ServiceLifetime lifetime = ServiceLifetime.Scoped);

    /// <summary>Adds a behavior that applies ONLY to <see cref="IStreamRequest{TResponse}"/> types.</summary>
    IMediatorBuilder AddStreamBehavior(Type behaviorType, ServiceLifetime lifetime = ServiceLifetime.Scoped);

    /// <inheritdoc cref="AddStreamBehavior(Type, ServiceLifetime)"/>
    IMediatorBuilder AddStreamBehavior<TBehavior>(ServiceLifetime lifetime = ServiceLifetime.Scoped);

    // ─────────────────────────────────────────────────────────────────────────
    // Introspection (used by TryAdd extension methods)
    // ─────────────────────────────────────────────────────────────────────────

    bool HasBehavior(Type behaviorType);
    bool HasHandler(Type handlerType);
    bool HasPreProcessor(Type processorType);
    bool HasPostProcessor(Type processorType);
}