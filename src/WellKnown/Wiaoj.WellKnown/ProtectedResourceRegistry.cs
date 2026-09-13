using Microsoft.Extensions.DependencyInjection;

namespace Wiaoj.WellKnown;

/// <summary>
/// The names of the protected resources an application registered, in registration order.
/// </summary>
/// <remarks>
/// Options are keyed by name, and an options type cannot enumerate its names. Mapping every document with one call, and
/// validating only the names that were actually registered, both need the list.
/// </remarks>
public sealed class ProtectedResourceRegistry {
    private readonly List<string> _names = [];

    internal ProtectedResourceRegistry() { }

    /// <summary>Gets the registered resource names; the unnamed resource is <see cref="string.Empty"/>.</summary>
    public IReadOnlyList<string> Names => this._names;

    internal bool Contains(string name) => this._names.Contains(name, StringComparer.Ordinal);

    internal void Add(string name) {
        if(!this.Contains(name)) {
            this._names.Add(name);
        }
    }

    internal static ProtectedResourceRegistry For(IServiceCollection services) {
        foreach(ServiceDescriptor descriptor in services) {
            if(descriptor.ServiceType == typeof(ProtectedResourceRegistry) && descriptor.ImplementationInstance is ProtectedResourceRegistry existing) {
                return existing;
            }
        }

        ProtectedResourceRegistry registry = new();
        services.AddSingleton(registry);
        return registry;
    }
}
