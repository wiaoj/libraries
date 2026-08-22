using Microsoft.Extensions.DependencyInjection;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Wiaoj.Webhooks;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Fluent builder interface for configuring Wiaoj Webhook engine services, transports, signing algorithms, and middleware pipelines.
/// </summary>
/// <remarks>
/// All middleware registered via this builder is resolved from the root <see cref="IServiceProvider"/>
/// when the delivery pipeline runner is instantiated. Middleware types must therefore either be
/// stateless or safe to use as singletons; do not inject scoped services (e.g. EF Core DbContext) directly
/// into middleware constructors. If a middleware requires scoped dependencies, resolve them explicitly
/// via <see cref="IServiceScopeFactory"/> inside <c>InvokeAsync</c> instead.
/// </remarks>
public interface IWebhookBuilder {
    /// <summary>
    /// Gets the application service collection being configured.
    /// </summary>
    IServiceCollection Services { get; }

    /// <summary>
    /// Adds a custom middleware type to the delivery pipeline.
    /// Automatically registers <typeparamref name="TMiddleware"/> as a singleton if not already present in DI.
    /// </summary>
    /// <typeparam name="TMiddleware">The middleware type.</typeparam>
    /// <returns>The builder instance for fluent chaining.</returns>
    IWebhookBuilder AddMiddleware<TMiddleware>() where TMiddleware : class, IWebhookMiddleware;

    /// <summary>
    /// Adds a singleton instance of custom middleware to the delivery pipeline.
    /// </summary>
    /// <param name="middleware">The middleware instance.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    IWebhookBuilder AddMiddleware(IWebhookMiddleware middleware);

    /// <summary>
    /// Adds a custom middleware to the delivery pipeline using a factory delegate.
    /// </summary>
    /// <typeparam name="TMiddleware">The middleware type.</typeparam>
    /// <param name="implementationFactory">The factory used to create the middleware instance.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    IWebhookBuilder AddMiddleware<TMiddleware>(Func<IServiceProvider, TMiddleware> implementationFactory) where TMiddleware : class, IWebhookMiddleware;
}