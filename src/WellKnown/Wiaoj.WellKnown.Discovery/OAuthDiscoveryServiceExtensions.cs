using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Wiaoj.Preconditions;
using Wiaoj.WellKnown.Discovery;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Microsoft.Extensions.DependencyInjection;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Registers <see cref="OAuthDiscoveryClient"/> as a typed HttpClient.
/// </summary>
public static class OAuthDiscoveryServiceExtensions {
    /// <summary>
    /// Registers <see cref="OAuthDiscoveryClient"/>, with a document cache shared by every instance the container creates.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Configures the options; for example, the trusted authorization servers.</param>
    /// <returns>The HttpClient builder, to add handlers or configure the client further.</returns>
    /// <remarks>
    /// The primary handler does not follow redirects, so the client can refuse one to another origin before it is
    /// followed. A primary handler configured on the returned builder replaces it; a response such a handler reached
    /// through another origin is still refused.
    /// </remarks>
    /// <example>
    /// <code>
    /// services.AddOAuthDiscoveryClient(options =&gt; options.TrustedAuthorizationServers.Add("https://vaultex.example.com"));
    ///
    /// // later, with an injected OAuthDiscoveryClient:
    /// OAuthDiscoveryResult? discovery = await discovery.DiscoverAsync(unauthorizedResponse);
    /// string? tokenEndpoint = discovery?.AuthorizationServer.TokenEndpoint;
    /// </code>
    /// </example>
    public static IHttpClientBuilder AddOAuthDiscoveryClient(this IServiceCollection services, Action<OAuthDiscoveryOptions>? configure = null) {
        Preca.ThrowIfNull(services);

        OptionsBuilder<OAuthDiscoveryOptions> options = services.AddOptions<OAuthDiscoveryOptions>();
        if(configure is not null) {
            options.Configure(configure);
        }

        services.TryAddSingleton(provider => new DiscoveryDocumentCache(
            provider.GetRequiredService<IOptions<OAuthDiscoveryOptions>>().Value,
            provider.GetService<TimeProvider>() ?? TimeProvider.System));

        return services.AddHttpClient(nameof(OAuthDiscoveryClient))
            .ConfigurePrimaryHttpMessageHandler(static () => new SocketsHttpHandler { AllowAutoRedirect = false })
            .AddTypedClient(static (http, provider) => new OAuthDiscoveryClient(http, provider.GetRequiredService<DiscoveryDocumentCache>()));
    }
}
