using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Wiaoj.Preconditions;
using Wiaoj.WellKnown;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Microsoft.Extensions.DependencyInjection;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Registers OAuth 2.0 protected resources whose RFC 9728 metadata the application publishes.
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
