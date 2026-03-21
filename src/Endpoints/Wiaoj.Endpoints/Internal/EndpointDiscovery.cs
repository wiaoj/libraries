using Microsoft.AspNetCore.Routing;
using System.Reflection;

namespace Wiaoj.Endpoints.Internal; 
/// <summary>
/// Scans assemblies for non-abstract <see cref="IEndpoint"/> implementations.
/// Results are used by <see cref="EndpointRouteBuilderExtensions.MapEndpoints"/>.
/// </summary>
internal static class EndpointDiscovery {

    /// <summary>
    /// Returns all concrete <see cref="IEndpoint"/> types in <paramref name="assembly"/>.
    /// </summary>
    public static IEnumerable<Type> FindIn(Assembly assembly)
        => assembly
            .GetTypes()
            .Where(t => t.IsClass
                     && !t.IsAbstract
                     && typeof(IEndpoint).IsAssignableFrom(t));

    /// <summary>
    /// Instantiates <paramref name="moduleType"/> via its public parameterless constructor.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown if the type has no public parameterless constructor.
    /// </exception>
    public static IEndpoint CreateInstance(Type moduleType) {
        try {
            return (IEndpoint)(Activator.CreateInstance(moduleType)
                ?? throw new InvalidOperationException(
                    $"Failed to create an instance of '{moduleType.FullName}'. " +
                    "Ensure the type has a public parameterless constructor."));
        }
        catch(MissingMethodException ex) {
            throw new InvalidOperationException(
                $"Failed to create an instance of '{moduleType.FullName}'. " +
                "Ensure the type has a public parameterless constructor.", ex);
        }
    }
}