using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Wiaoj.Endpoints; 
/// <summary>
/// Defines a self-contained group of related endpoints.
/// <para>
/// Implement this interface to declare endpoints for a single feature or bounded context.
/// All implementations are discovered and registered automatically by
/// <see cref="EndpointRouteBuilderExtensions.MapEndpoints"/>.
/// </para>
/// <para>
/// When <see cref="RoutePrefixAttribute"/> is applied to the implementing class,
/// the <paramref name="app"/> passed to <see cref="Map"/> is a
/// <see cref="RouteGroupBuilder"/> scoped to that prefix.
/// Without the attribute, the root <see cref="IEndpointRouteBuilder"/> is passed directly.
/// </para>
/// </summary>
/// <example>
/// <code>
/// [RoutePrefix("/orders")]
/// public sealed class OrderEndpoints : IEndpoint {
///     public void Map(IEndpointRouteBuilder app) {
///         app.MapGet("/",      GetAll);
///         app.MapGet("/{id}", GetById);
///         app.MapPost("/",    Create);
///     }
/// }
/// </code>
/// </example>
public interface IEndpoint {

    /// <summary>
    /// Registers this module's endpoints on <paramref name="app"/>.
    /// </summary>
    /// <param name="app">
    /// The endpoint route builder. When <see cref="RoutePrefixAttribute"/> is present,
    /// this is a <see cref="RouteGroupBuilder"/> pre-scoped to the declared prefix.
    /// </param>
    void Map(IEndpointRouteBuilder app);
}