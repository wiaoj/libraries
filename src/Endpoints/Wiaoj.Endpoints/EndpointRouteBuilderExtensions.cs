using Microsoft.AspNetCore.Builder;
using System.Reflection;
using Wiaoj.Endpoints;
using Wiaoj.Endpoints.Internal;

#pragma warning disable IDE0130
namespace Microsoft.AspNetCore.Routing;
#pragma warning restore IDE0130

/// <summary>
/// Extension methods for discovering and mapping <see cref="Wiaoj.Endpoints.IEndpoint"/>
/// implementations onto an <see cref="IEndpointRouteBuilder"/>.
/// </summary>
public static class EndpointRouteBuilderExtensions {

    /// <summary>
    /// Scans the assembly containing <typeparamref name="TMarker"/> for all
    /// <see cref="Wiaoj.Endpoints.IEndpoint"/> implementations and maps their endpoints.
    /// </summary>
    /// <typeparam name="TMarker">Any type in the target assembly.</typeparam>
    /// <param name="app">The endpoint route builder (e.g. <c>WebApplication</c>).</param>
    /// <returns>The same <see cref="IEndpointRouteBuilder"/> for chaining.</returns>
    /// <example>
    /// <code>
    /// app.MapEndpoints&lt;Program&gt;();
    /// </code>
    /// </example>
    public static IEndpointRouteBuilder MapEndpoints<TMarker>(this IEndpointRouteBuilder app) {
        return app.MapEndpoints(typeof(TMarker).Assembly);
    }

    /// <summary>
    /// Scans <paramref name="assembly"/> for all <see cref="Wiaoj.Endpoints.IEndpoint"/>
    /// implementations and maps their endpoints.
    /// </summary>
    /// <param name="app">The endpoint route builder.</param>
    /// <param name="assembly">The assembly to scan.</param>
    /// <returns>The same <see cref="IEndpointRouteBuilder"/> for chaining.</returns>
    public static IEndpointRouteBuilder MapEndpoints(
        this IEndpointRouteBuilder app, Assembly assembly) {

        foreach(Type moduleType in EndpointDiscovery.FindIn(assembly)) {
            IEndpoint module = EndpointDiscovery.CreateInstance(moduleType);
            MapModule(app, module, moduleType);
        }

        return app;
    }

    /// <summary>
    /// Maps endpoints from a pre-instantiated <see cref="Wiaoj.Endpoints.IEndpoint"/>.
    /// Useful when the module is resolved from DI or created manually.
    /// </summary>
    /// <param name="app">The endpoint route builder.</param>
    /// <param name="module">The endpoint module instance to map.</param>
    /// <returns>The same <see cref="IEndpointRouteBuilder"/> for chaining.</returns>
    public static IEndpointRouteBuilder MapEndpoints(
        this IEndpointRouteBuilder app, IEndpoint module) {

        MapModule(app, module, module.GetType());
        return app;
    }

    // ── Core mapping ──────────────────────────────────────────────────────────

    private static void MapModule(
        IEndpointRouteBuilder app,
        IEndpoint module,
        Type moduleType) {

        RoutePrefixAttribute? prefix =
            moduleType.GetCustomAttribute<RoutePrefixAttribute>(inherit: false);

        if(prefix is not null) {
            RouteGroupBuilder group = app.MapGroup(prefix.Prefix);
            module.Map(group);
        }
        else {
            module.Map(app);
        }
    }
}