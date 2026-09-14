using Microsoft.Extensions.DependencyInjection;

namespace Wiaoj.WellKnown;

/// <summary>
/// The names of the authorization servers an application registered, in registration order.
/// </summary>
/// <remarks>
/// Options are keyed by name, and an options type cannot enumerate its names. Mapping every document with one call, and
/// validating only the names that were actually registered, both need the list.
/// </remarks>
public sealed class AuthorizationServerRegistry {
    private readonly List<string> _names = [];

    internal AuthorizationServerRegistry() { }

    /// <summary>Gets the registered authorization server names; the unnamed server is <see cref="string.Empty"/>.</summary>
    public IReadOnlyList<string> Names => this._names;

    internal bool Contains(string name) => this._names.Contains(name, StringComparer.Ordinal);

    internal void Add(string name) {
        if(!this.Contains(name)) {
            this._names.Add(name);
        }
    }

    internal static AuthorizationServerRegistry For(IServiceCollection services) {
        foreach(ServiceDescriptor descriptor in services) {
            if(descriptor.ServiceType == typeof(AuthorizationServerRegistry) && descriptor.ImplementationInstance is AuthorizationServerRegistry existing) {
                return existing;
            }
        }

        AuthorizationServerRegistry registry = new();
        services.AddSingleton(registry);
        return registry;
    }
}
