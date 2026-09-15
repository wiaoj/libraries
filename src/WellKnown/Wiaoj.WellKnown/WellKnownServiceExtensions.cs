using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Wiaoj.Preconditions;
using Wiaoj.WellKnown;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Microsoft.Extensions.DependencyInjection;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Registers the OAuth 2.0 protected resources (RFC 9728) and authorization servers (RFC 8414) whose metadata the
/// application publishes.
/// </summary>
public static class WellKnownServiceExtensions {
    /// <summary>
    /// Registers the application's protected resource, configured elsewhere — through configuration binding or
    /// <c>services.Configure&lt;OAuthProtectedResourceOptions&gt;(...)</c>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddOAuthProtectedResource(this IServiceCollection services) {
        return services.AddOAuthProtectedResource(Microsoft.Extensions.Options.Options.DefaultName, static _ => { });
    }

    /// <summary>
    /// Registers the application's protected resource.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Describes the resource. <see cref="OAuthProtectedResourceOptions.Resource"/> is required.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <example>
    /// <code>
    /// services.AddOAuthProtectedResource(resource =&gt; {
    ///     resource.Resource = "https://api.example.com";
    ///     resource.AuthorizationServers.Add("https://auth.example.com");
    ///     resource.ResourceName = "Example API";
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddOAuthProtectedResource(this IServiceCollection services, Action<OAuthProtectedResourceOptions> configure) {
        return services.AddOAuthProtectedResource(Microsoft.Extensions.Options.Options.DefaultName, configure);
    }

    /// <summary>
    /// Registers one of several protected resources the application serves, under <paramref name="name"/>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="name">The name the resource is referred to by — for its scopes, and for a challenge that advertises it.</param>
    /// <param name="configure">Describes the resource. <see cref="OAuthProtectedResourceOptions.Resource"/> is required.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <remarks>
    /// Each resource gets its own document at the path derived from its identifier, so <c>https://api.example.com/v1</c>
    /// and <c>https://api.example.com/admin</c> can share a host.
    /// </remarks>
    public static IServiceCollection AddOAuthProtectedResource(
        this IServiceCollection services,
        string name,
        Action<OAuthProtectedResourceOptions> configure) {

        Preca.ThrowIfNull(services);
        Preca.ThrowIfNull(name);
        Preca.ThrowIfNull(configure);

        ProtectedResourceRegistry.For(services).Add(name);
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<OAuthProtectedResourceOptions>, OAuthProtectedResourceOptionsValidator>());
        services.AddOptions<OAuthProtectedResourceOptions>(name).Configure(configure).ValidateOnStart();

        return services;
    }

    /// <summary>
    /// Registers the application's authorization server, configured elsewhere — through configuration binding or
    /// <c>services.Configure&lt;OAuthAuthorizationServerOptions&gt;(...)</c>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddOAuthAuthorizationServer(this IServiceCollection services) {
        return services.AddOAuthAuthorizationServer(Microsoft.Extensions.Options.Options.DefaultName, static _ => { });
    }

    /// <summary>
    /// Registers the application's authorization server, whose RFC 8414 metadata <c>MapOAuthAuthorizationServer</c> serves.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">
    /// Describes the server. <see cref="OAuthAuthorizationServerOptions.Issuer"/> and
    /// <see cref="OAuthAuthorizationServerOptions.ResponseTypesSupported"/> are required, and the endpoints the supported
    /// grant types use.
    /// </param>
    /// <returns>The service collection for chaining.</returns>
    /// <example>
    /// <code>
    /// services.AddOAuthAuthorizationServer(server =&gt; {
    ///     server.Issuer = "https://auth.example.com";
    ///     server.TokenEndpoint = "https://auth.example.com/connect/token";
    ///     server.DeviceAuthorizationEndpoint = "https://auth.example.com/connect/device";
    ///     server.GrantTypesSupported.AddRange(["client_credentials", "urn:ietf:params:oauth:grant-type:device_code"]);
    ///     server.ResponseTypesSupported.Add("code");
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddOAuthAuthorizationServer(this IServiceCollection services, Action<OAuthAuthorizationServerOptions> configure) {
        return services.AddOAuthAuthorizationServer(Microsoft.Extensions.Options.Options.DefaultName, configure);
    }

    /// <summary>
    /// Registers one of several authorization servers — issuers — the application serves, under <paramref name="name"/>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="name">The name the server's options are registered under.</param>
    /// <param name="configure">Describes the server.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <remarks>
    /// Each issuer gets its own document at the path derived from it, so <c>https://auth.example.com/tenant1</c> and
    /// <c>https://auth.example.com/tenant2</c> can share a host.
    /// </remarks>
    public static IServiceCollection AddOAuthAuthorizationServer(
        this IServiceCollection services,
        string name,
        Action<OAuthAuthorizationServerOptions> configure) {

        Preca.ThrowIfNull(services);
        Preca.ThrowIfNull(name);
        Preca.ThrowIfNull(configure);

        AuthorizationServerRegistry.For(services).Add(name);
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<OAuthAuthorizationServerOptions>, OAuthAuthorizationServerOptionsValidator>());
        services.AddOptions<OAuthAuthorizationServerOptions>(name).Configure(configure).ValidateOnStart();

        return services;
    }

    /// <summary>
    /// Registers the application's RFC 9116 <c>security.txt</c>, configured elsewhere — typically bound from configuration
    /// with <c>services.Configure&lt;SecurityTxtOptions&gt;(configuration.GetSection("SecurityTxt"))</c>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddSecurityTxt(this IServiceCollection services) {
        return services.AddSecurityTxt(static _ => { });
    }

    /// <summary>
    /// Registers the application's RFC 9116 <c>security.txt</c>, which <c>MapSecurityTxt</c> serves.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">
    /// Describes the file. <see cref="SecurityTxtOptions.Contact"/> and <see cref="SecurityTxtOptions.Expires"/> are required.
    /// </param>
    /// <returns>The service collection for chaining.</returns>
    /// <remarks>
    /// The options are validated at startup. The current time comes from the registered <see cref="TimeProvider"/>, or
    /// <see cref="TimeProvider.System"/> when there is none.
    /// </remarks>
    /// <example>
    /// <code>
    /// services.AddSecurityTxt(file =&gt; {
    ///     file.Contact.Add("mailto:security@example.com");
    ///     file.Expires = new DateTimeOffset(2027, 6, 30, 0, 0, 0, TimeSpan.Zero);
    ///     file.Policy.Add("https://example.com/security/policy");
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddSecurityTxt(this IServiceCollection services, Action<SecurityTxtOptions> configure) {
        Preca.ThrowIfNull(services);
        Preca.ThrowIfNull(configure);

        services.TryAddSingleton<SecurityTxtRegistration>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<SecurityTxtOptions>, SecurityTxtOptionsValidator>(
            static sp => new SecurityTxtOptionsValidator(sp.GetService<TimeProvider>() ?? TimeProvider.System)));
        services.AddOptions<SecurityTxtOptions>().Configure(configure).ValidateOnStart();

        return services;
    }

    /// <summary>
    /// Adds scopes to the application's protected resource, so each module can publish the scopes it defines.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="scopes">The scopes; blank entries are ignored and surrounding whitespace is trimmed.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddProtectedResourceScopes(this IServiceCollection services, params IEnumerable<string> scopes) {
        return services.AddProtectedResourceScopes(Microsoft.Extensions.Options.Options.DefaultName, scopes);
    }

    /// <summary>
    /// Adds scopes to the protected resource registered under <paramref name="name"/>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="name">The resource's name.</param>
    /// <param name="scopes">The scopes; blank entries are ignored and surrounding whitespace is trimmed.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <remarks>
    /// <paramref name="scopes"/> is deliberately not <c>params</c>. If it were, <c>AddProtectedResourceScopes("a:read", "a:write")</c>
    /// would bind here and publish <c>a:write</c> on a resource named <c>a:read</c>, instead of both scopes on the
    /// application's resource. Pass a collection: <c>AddProtectedResourceScopes("admin", ["users:manage"])</c>.
    /// </remarks>
    public static IServiceCollection AddProtectedResourceScopes(this IServiceCollection services, string name, IEnumerable<string> scopes) {
        Preca.ThrowIfNull(services);
        Preca.ThrowIfNull(name);
        Preca.ThrowIfNull(scopes);

        string[] copy = [.. scopes.Where(scope => !string.IsNullOrWhiteSpace(scope)).Select(scope => scope.Trim())];

        services.Configure<OAuthProtectedResourceOptions>(name, options => {
            foreach(string scope in copy) {
                options.Scopes.Add(scope);
            }
        });

        return services;
    }
}
