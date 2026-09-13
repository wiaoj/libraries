using Wiaoj.WellKnown;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Microsoft.Extensions.DependencyInjection;
#pragma warning restore IDE0130 // Namespace does not match folder structure

public static class WellKnownServiceExtensions {
    /// <summary>
    /// Protected Resource Metadata servisini DI konteynerine kaydeder.
    /// </summary>
    public static IServiceCollection AddOAuthProtectedResource(this IServiceCollection services) {
        return services.AddOAuthProtectedResource(_ => { });
    }

    /// <summary>
    /// Protected Resource Metadata servisini DI konteynerine kaydeder.
    /// </summary>
    public static IServiceCollection AddOAuthProtectedResource(this IServiceCollection services, Action<OAuthProtectedResourceOptions> configure) {
        if(configure is not null) {
            services.Configure(configure);
        }

        return services;
    }

    /// <summary>
    /// Herhangi bir modülün merkezi metadata havuzuna kendi scope'larını eklemesini sağlar.
    /// </summary>
    public static IServiceCollection AddProtectedResourceScopes(this IServiceCollection services, params IEnumerable<string> scopes) {

        services.Configure<OAuthProtectedResourceOptions>(options => {
            foreach(string scope in scopes) {
                if(!string.IsNullOrWhiteSpace(scope)) {
                    options.Scopes.Add(scope.Trim());
                }
            }
        });

        return services;
    }
}