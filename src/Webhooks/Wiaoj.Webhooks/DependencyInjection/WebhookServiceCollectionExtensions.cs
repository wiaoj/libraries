using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Sockets;
using Wiaoj.Serialization.DependencyInjection;
using Wiaoj.Webhooks;
using Wiaoj.Webhooks.Exceptions;
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

        services.AddHttpClient<HttpWebhookSender>((sp, client) => {
            WebhookSecurityOptions options = sp.GetRequiredService<IOptions<WebhookSecurityOptions>>().Value;
            client.Timeout = options.RequestTimeout;
        })
         .RemoveAllLoggers()
         .ConfigurePrimaryHttpMessageHandler(sp => {
             WebhookSecurityOptions options = sp.GetRequiredService<IOptions<WebhookSecurityOptions>>().Value;
             SocketsHttpHandler handler = new() {
                 PooledConnectionLifetime = TimeSpan.FromMinutes(15),
                 ConnectTimeout = options.ConnectTimeout,
                 AllowAutoRedirect = false
             };

             if(options.Proxy is not null) {
                 handler.Proxy = options.Proxy;
                 handler.UseProxy = true;
             }
             else {
                 // Direct socket connection with SSRF filtering
                 handler.ConnectCallback = async (context, cancellationToken) => {
                     IPAddress[] addresses = await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, cancellationToken).ConfigureAwait(false);

                     IPAddress targetIp = addresses.FirstOrDefault(ip => WebhookIpFilter.IsAllowed(ip, options.AllowPrivateNetworks))
                         ?? throw new WebhookSsrfBlockedException($"All resolved IP addresses for '{context.DnsEndPoint.Host}' are in prohibited private or link-local ranges.");

                     Socket socket = new(targetIp.AddressFamily, SocketType.Stream, ProtocolType.Tcp) {
                         NoDelay = true
                     };

                     try {
                         await socket.ConnectAsync(new IPEndPoint(targetIp, context.DnsEndPoint.Port), cancellationToken).ConfigureAwait(false);
                         return new NetworkStream(socket, ownsSocket: true);
                     }
                     catch {
                         socket.Dispose();
                         throw;
                     }
                 };
             }

             return handler;
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

        services.TryAddTransient<IWebhookDeliverer, HttpWebhookDeliverer>();

        services.TryAddTransient<WebhookPipelineRunner>(static sp => {
            IWebhookMiddleware[] middleware = sp.GetServices<IWebhookMiddleware>().ToArray();
            IWebhookDeliverer deliverer = sp.GetRequiredService<IWebhookDeliverer>();
            TimeProvider timeProvider = sp.GetRequiredService<TimeProvider>();
            ILogger<WebhookPipelineRunner> logger = sp.GetRequiredService<ILogger<WebhookPipelineRunner>>();
            return new WebhookPipelineRunner(middleware, deliverer, timeProvider, logger);
        });

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