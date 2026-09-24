using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wiaoj.Net;
using Wiaoj.Serialization;
using Wiaoj.Serialization.DependencyInjection;
using Wiaoj.Webhooks;
using Wiaoj.Webhooks.Internal;
using Wiaoj.Webhooks.Security;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Microsoft.Extensions.DependencyInjection;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Extension methods for setting up Wiaoj Webhook services in an <see cref="IServiceCollection"/>.
/// </summary>
public static class WebhookServiceCollectionExtensions {
    /// <summary>
    /// Adds Wiaoj Webhook core engine services with default configuration.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <returns>An <see cref="IWebhookBuilder"/> for chaining additional configurations.</returns>
    public static IWebhookBuilder AddWiaojWebhooks(this IServiceCollection services) {
        Preca.ThrowIfNull(services);
        services.TryAddSingleton<TimeProvider>(TimeProvider.System);

        services.AddSingleton<IValidateOptions<WebhookSecurityOptions>, WebhookSecurityOptionsValidator>();
        services.AddOptions<WebhookSecurityOptions>().ValidateOnStart();
        services.AddOptions<WebhookOptions>().ValidateOnStart();

        services.AddHttpClient<HttpWebhookSender>((sp, client) => {
            WebhookSecurityOptions options = sp.GetRequiredService<IOptions<WebhookSecurityOptions>>().Value;
            client.Timeout = options.RequestTimeout;
        })
         //.RemoveAllLoggers()
         .ConfigurePrimaryHttpMessageHandler(sp => {
             WebhookSecurityOptions options = sp.GetRequiredService<IOptions<WebhookSecurityOptions>>().Value;
             options.Validate();
             SocketsHttpHandler handler = new() {
                 PooledConnectionLifetime = options.PooledConnectionLifetime,
                 ConnectTimeout = options.ConnectTimeout,
                 AllowAutoRedirect = false
             };

             if(options.Proxy is not null) {
                 handler.Proxy = options.Proxy;
                 handler.UseProxy = true;
             }
             else {
                 // Direct connections: each host is resolved and connected to only through an address the policy
                 // allows (Wiaoj.Net), so DNS cannot answer differently between the check and the connection.
                 handler.UseOutboundNetworkPolicy(options.EffectiveNetworkPolicy, sp.GetService<DnsResolver>() ?? DnsResolver.System);
             }

             return handler;
         })
         .AddHttpMessageHandler(sp => {
             // Through a proxy the connection-time policy sees the proxy, not the destination, so destinations are checked
             // before they are sent (Wiaoj.Net). Direct connections are already checked where the socket opens.
             WebhookSecurityOptions options = sp.GetRequiredService<IOptions<WebhookSecurityOptions>>().Value;
             return options.Proxy is not null && !options.AllowPrivateNetworks
                 ? new ProxiedDestinationCheckHandler(options.EffectiveNetworkPolicy, sp.GetService<DnsResolver>() ?? DnsResolver.System)
                 : new PassThroughHandler();
         });

        services.AddWiaojSerializer(serialization => {
            serialization.TryUseSystemTextJson<WebhookSerializerKey>();
        });

        services.AddOptions<WebhookEventRegistryOptions>();
        services.TryAddSingleton<IWebhookEventRegistry>(static sp => {
            IOptions<WebhookEventRegistryOptions> options = sp.GetRequiredService<IOptions<WebhookEventRegistryOptions>>();
            return new WebhookEventRegistry(options.Value);
        });

        services.TryAddSingleton<IWebhookStore, InMemoryWebhookStore>();
        services.TryAddSingleton<IWebhookEndpointResolver, InMemoryWebhookEndpointStore>();
        services.TryAddTransient<IWebhookDeliverer, HttpWebhookDeliverer>();

        services.TryAddTransient<WebhookPipelineRunner>(static sp => {
            IWebhookMiddleware[] middleware = [.. sp.GetServices<IWebhookMiddleware>()];
            IWebhookDeliverer deliverer = sp.GetRequiredService<IWebhookDeliverer>();
            TimeProvider timeProvider = sp.GetRequiredService<TimeProvider>();
            ILogger<WebhookPipelineRunner> logger = sp.GetRequiredService<ILogger<WebhookPipelineRunner>>();
            return new WebhookPipelineRunner(middleware, deliverer, timeProvider, logger);
        });

        services.TryAddSingleton<WebhookJobExecutionGuard>();
        services.TryAddTransient<IWebhookJobHandler, WebhookJobHandler>();
        services.TryAddSingleton<IWebhookDispatcher, WebhookDispatcher>();

        return new WebhookBuilder(services);
    }

    /// <summary>
    /// Adds Wiaoj Webhook core engine services configured via a delegate.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="configure">The delegate used to configure the webhook engine.</param>
    /// <returns>The original <see cref="IServiceCollection"/>.</returns>
    public static IServiceCollection AddWiaojWebhooks(this IServiceCollection services, Action<IWebhookBuilder> configure) {
        Preca.ThrowIfNull(services);
        Preca.ThrowIfNull(configure);

        IWebhookBuilder builder = services.AddWiaojWebhooks();
        configure(builder);

        return services;
    }

    /// <summary>
    /// Adds Webhook core engine services with default configuration. Alias for <see cref="AddWiaojWebhooks(IServiceCollection)"/>.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <returns>An <see cref="IWebhookBuilder"/> for chaining additional configurations.</returns>
    public static IWebhookBuilder AddWebhooks(this IServiceCollection services) {
        return AddWiaojWebhooks(services);
    }

    /// <summary>
    /// Adds Webhook core engine services configured via a delegate. Alias for <see cref="AddWiaojWebhooks(IServiceCollection, Action{IWebhookBuilder})"/>.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="configure">The delegate used to configure the webhook engine.</param>
    /// <returns>The original <see cref="IServiceCollection"/>.</returns>
    public static IServiceCollection AddWebhooks(this IServiceCollection services, Action<IWebhookBuilder> configure) {
        return AddWiaojWebhooks(services, configure);
    }
}