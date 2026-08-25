using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Reflection;
using Wiaoj.Webhooks.Internal;

#pragma warning disable IDE0130
namespace Wiaoj.Webhooks;
#pragma warning restore IDE0130

/// <summary>
/// Core builder extensions for registering stores, deliverers, endpoint resolvers, and event topologies.
/// </summary>
public static partial class WebhookBuilderCoreExtensions {

    // ── STORE REGISTRATIONS ──────────────────────────────────────────────────

    /// <summary>
    /// Configures the default in-memory webhook store.
    /// </summary>
    /// <param name="builder">The webhook builder being configured.</param>
    /// <returns>The <see cref="IWebhookBuilder"/> instance for fluent method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> is <see langword="null"/>.</exception>
    public static IWebhookBuilder UseInMemoryStore(this IWebhookBuilder builder) {
        Preca.ThrowIfNull(builder);
        builder.Services.RemoveAll<IWebhookStore>();
        builder.Services.AddSingleton<IWebhookStore, InMemoryWebhookStore>();
        return builder;
    }

    /// <summary>
    /// Configures a custom webhook store type.
    /// </summary>
    /// <typeparam name="TStore">The type implementing <see cref="IWebhookStore"/>.</typeparam>
    /// <param name="builder">The webhook builder being configured.</param>
    /// <returns>The <see cref="IWebhookBuilder"/> instance for fluent method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> is <see langword="null"/>.</exception>
    public static IWebhookBuilder UseStore<TStore>(this IWebhookBuilder builder) where TStore : class, IWebhookStore {
        Preca.ThrowIfNull(builder);
        builder.Services.RemoveAll<IWebhookStore>();
        builder.Services.AddSingleton<IWebhookStore, TStore>();
        return builder;
    }

    /// <summary>
    /// Configures a specific singleton instance of the webhook store.
    /// </summary>
    /// <param name="builder">The webhook builder being configured.</param>
    /// <param name="store">The webhook store instance.</param>
    /// <returns>The <see cref="IWebhookBuilder"/> instance for fluent method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> or <paramref name="store"/> is <see langword="null"/>.</exception>
    public static IWebhookBuilder UseStore(this IWebhookBuilder builder, IWebhookStore store) {
        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNull(store);
        builder.Services.RemoveAll<IWebhookStore>();
        builder.Services.AddSingleton(store);
        return builder;
    }

    /// <summary>
    /// Configures a webhook store using a factory delegate.
    /// </summary>
    /// <param name="builder">The webhook builder being configured.</param>
    /// <param name="implementationFactory">The factory delegate used to resolve the store.</param>
    /// <returns>The <see cref="IWebhookBuilder"/> instance for fluent method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> or <paramref name="implementationFactory"/> is <see langword="null"/>.</exception>
    public static IWebhookBuilder UseStore(this IWebhookBuilder builder, Func<IServiceProvider, IWebhookStore> implementationFactory) {
        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNull(implementationFactory);
        builder.Services.RemoveAll<IWebhookStore>();
        builder.Services.AddSingleton(implementationFactory);
        return builder;
    }

    /// <summary>
    /// Disables persistent job auditing by using <see cref="NullWebhookStore"/>.
    /// </summary>
    /// <param name="builder">The webhook builder being configured.</param>
    /// <returns>The <see cref="IWebhookBuilder"/> instance for fluent method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> is <see langword="null"/>.</exception>
    public static IWebhookBuilder UseNullStore(this IWebhookBuilder builder) {
        Preca.ThrowIfNull(builder);
        builder.Services.RemoveAll<IWebhookStore>();
        builder.Services.AddSingleton<IWebhookStore>(NullWebhookStore.Instance);
        return builder;
    }

    // ── ENDPOINT RESOLVER REGISTRATIONS ──────────────────────────────────────

    /// <summary>
    /// Configures a custom endpoint resolver type.
    /// </summary>
    /// <typeparam name="TResolver">The type implementing <see cref="IWebhookEndpointResolver"/>.</typeparam>
    /// <param name="builder">The webhook builder being configured.</param>
    /// <returns>The <see cref="IWebhookBuilder"/> instance for fluent method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> is <see langword="null"/>.</exception>
    public static IWebhookBuilder UseEndpointResolver<TResolver>(this IWebhookBuilder builder) where TResolver : class, IWebhookEndpointResolver {
        Preca.ThrowIfNull(builder);
        builder.Services.RemoveAll<IWebhookEndpointResolver>();
        builder.Services.AddSingleton<IWebhookEndpointResolver, TResolver>();
        return builder;
    }

    /// <summary>
    /// Configures a specific singleton instance of the endpoint resolver.
    /// </summary>
    /// <param name="builder">The webhook builder being configured.</param>
    /// <param name="resolver">The endpoint resolver instance.</param>
    /// <returns>The <see cref="IWebhookBuilder"/> instance for fluent method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> or <paramref name="resolver"/> is <see langword="null"/>.</exception>
    public static IWebhookBuilder UseEndpointResolver(this IWebhookBuilder builder, IWebhookEndpointResolver resolver) {
        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNull(resolver);
        builder.Services.RemoveAll<IWebhookEndpointResolver>();
        builder.Services.AddSingleton(resolver);
        return builder;
    }

    /// <summary>
    /// Configures an endpoint resolver using a factory delegate.
    /// </summary>
    /// <param name="builder">The webhook builder being configured.</param>
    /// <param name="implementationFactory">The factory delegate used to resolve the endpoint resolver.</param>
    /// <returns>The <see cref="IWebhookBuilder"/> instance for fluent method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> or <paramref name="implementationFactory"/> is <see langword="null"/>.</exception>
    public static IWebhookBuilder UseEndpointResolver(this IWebhookBuilder builder, Func<IServiceProvider, IWebhookEndpointResolver> implementationFactory) {
        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNull(implementationFactory);
        builder.Services.RemoveAll<IWebhookEndpointResolver>();
        builder.Services.AddSingleton(implementationFactory);
        return builder;
    }

    /// <summary>
    /// Configures a lightweight inline delegate for resolving endpoints.
    /// </summary>
    /// <param name="builder">The webhook builder being configured.</param>
    /// <param name="resolveDelegate">The resolution function.</param>
    /// <returns>The <see cref="IWebhookBuilder"/> instance for fluent method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> or <paramref name="resolveDelegate"/> is <see langword="null"/>.</exception>
    public static IWebhookBuilder UseEndpointResolver(
        this IWebhookBuilder builder,
        Func<WebhookEndpointId, CancellationToken, ValueTask<WebhookEndpoint?>> resolveDelegate) {
        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNull(resolveDelegate);
        return builder.UseEndpointResolver(new DelegateWebhookEndpointResolver(resolveDelegate));
    }

    /// <summary>
    /// Configures the default in-memory endpoint resolver.
    /// </summary>
    /// <param name="builder">The webhook builder.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    public static IWebhookBuilder UseInMemoryEndpoints(this IWebhookBuilder builder) {
        Preca.ThrowIfNull(builder);
        builder.Services.RemoveAll<IWebhookEndpointResolver>();
        builder.Services.AddSingleton<IWebhookEndpointResolver, InMemoryWebhookEndpointStore>();
        return builder;
    }

    /// <summary>
    /// Configures the in-memory endpoint resolver with pre-registered endpoints.
    /// </summary>
    /// <param name="builder">The webhook builder.</param>
    /// <param name="endpoints">The collection of endpoints to preload.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    public static IWebhookBuilder UseInMemoryEndpoints(this IWebhookBuilder builder, params WebhookEndpoint[] endpoints) {
        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNull(endpoints);
        builder.Services.RemoveAll<IWebhookEndpointResolver>();
        builder.Services.AddSingleton<IWebhookEndpointResolver>(new InMemoryWebhookEndpointStore(endpoints));
        return builder;
    }

    // ── TERMINAL DELIVERER REGISTRATIONS ─────────────────────────────────────

    /// <summary>
    /// Configures a custom terminal <see cref="IWebhookDeliverer"/> type (e.g. gRPC or custom HTTP sender).
    /// </summary>
    /// <typeparam name="TDeliverer">The type implementing <see cref="IWebhookDeliverer"/>.</typeparam>
    /// <param name="builder">The webhook builder being configured.</param>
    /// <returns>The <see cref="IWebhookBuilder"/> instance for fluent method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> is <see langword="null"/>.</exception>
    public static IWebhookBuilder UseDeliverer<TDeliverer>(this IWebhookBuilder builder) where TDeliverer : class, IWebhookDeliverer {
        Preca.ThrowIfNull(builder);
        builder.Services.RemoveAll<IWebhookDeliverer>();
        builder.Services.AddTransient<IWebhookDeliverer, TDeliverer>();
        return builder;
    }

    /// <summary>
    /// Configures a custom terminal <see cref="IWebhookDeliverer"/> instance.
    /// </summary>
    /// <param name="builder">The webhook builder being configured.</param>
    /// <param name="deliverer">The webhook deliverer instance.</param>
    /// <returns>The <see cref="IWebhookBuilder"/> instance for fluent method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> or <paramref name="deliverer"/> is <see langword="null"/>.</exception>
    public static IWebhookBuilder UseDeliverer(this IWebhookBuilder builder, IWebhookDeliverer deliverer) {
        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNull(deliverer);
        builder.Services.RemoveAll<IWebhookDeliverer>();
        builder.Services.AddSingleton(deliverer);
        return builder;
    }

    /// <summary>
    /// Configures a terminal <see cref="IWebhookDeliverer"/> using a factory delegate.
    /// </summary>
    /// <param name="builder">The webhook builder being configured.</param>
    /// <param name="implementationFactory">The factory delegate used to resolve the deliverer.</param>
    /// <returns>The <see cref="IWebhookBuilder"/> instance for fluent method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> or <paramref name="implementationFactory"/> is <see langword="null"/>.</exception>
    public static IWebhookBuilder UseDeliverer(this IWebhookBuilder builder, Func<IServiceProvider, IWebhookDeliverer> implementationFactory) {
        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNull(implementationFactory);
        builder.Services.RemoveAll<IWebhookDeliverer>();
        builder.Services.AddTransient(implementationFactory);
        return builder;
    }

    // ── EVENT TOPOLOGY REGISTRATIONS ─────────────────────────────────────────

    /// <summary>
    /// Explicitly registers an event type with an optional wire-format name override.
    /// </summary>
    /// <typeparam name="TEvent">The event type implementing <see cref="IWebhookEvent"/>.</typeparam>
    /// <param name="builder">The webhook builder being configured.</param>
    /// <param name="eventName">The wire-format name override. When <see langword="null"/>, resolves via attribute or class convention.</param>
    /// <returns>The <see cref="IWebhookBuilder"/> instance for fluent method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> is <see langword="null"/>.</exception>
    public static IWebhookBuilder RegisterEvent<TEvent>(this IWebhookBuilder builder, string? eventName = null)
        where TEvent : class, IWebhookEvent {
        Preca.ThrowIfNull(builder);

        string resolvedName = !string.IsNullOrWhiteSpace(eventName)
            ? eventName
            : WebhookEventRegistry.ResolveConventionName(typeof(TEvent));

        builder.Services.Configure<WebhookEventRegistryOptions>(opts => {
            opts.Mappings[typeof(TEvent)] = resolvedName;
        });

        return builder;
    }

    /// <summary>
    /// Automatically discovers and registers all <see cref="IWebhookEvent"/> types in the specified assembly.
    /// </summary>
    /// <param name="builder">The webhook builder being configured.</param>
    /// <param name="assembly">The assembly to scan for event types.</param>
    /// <returns>The <see cref="IWebhookBuilder"/> instance for fluent method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> or <paramref name="assembly"/> is <see langword="null"/>.</exception>
    public static IWebhookBuilder RegisterEventsFromAssembly(this IWebhookBuilder builder, Assembly assembly) {
        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNull(assembly);

        Type eventInterface = typeof(IWebhookEvent);
        Type[] eventTypes = assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && eventInterface.IsAssignableFrom(t))
            .ToArray();

        builder.Services.Configure<WebhookEventRegistryOptions>(opts => {
            foreach(Type t in eventTypes) {
                opts.Mappings[t] = WebhookEventRegistry.ResolveConventionName(t);
            }
        });

        return builder;
    }

    private sealed class DelegateWebhookEndpointResolver(
        Func<WebhookEndpointId, CancellationToken, ValueTask<WebhookEndpoint?>> resolveDelegate)
        : IWebhookEndpointResolver {

        public ValueTask<WebhookEndpoint?> ResolveAsync(WebhookEndpointId endpointId, CancellationToken cancellationToken = default) {
            return resolveDelegate(endpointId, cancellationToken);
        }
    }
}