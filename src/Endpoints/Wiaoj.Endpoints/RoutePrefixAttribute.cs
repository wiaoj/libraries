using Microsoft.AspNetCore.Builder;

namespace Wiaoj.Endpoints; 
/// <summary>
/// Declares the route prefix for all endpoints defined in an <see cref="IEndpoint"/>.
/// <para>
/// When present, <see cref="EndpointRouteBuilderExtensions.MapEndpoints"/> creates a
/// <see cref="Microsoft.AspNetCore.Routing.RouteGroupBuilder"/> scoped to <see cref="Prefix"/>
/// and passes it to <see cref="IEndpoint.Map"/>. The module's individual
/// <c>MapGet</c> / <c>MapPost</c> / etc. calls are then relative to that prefix.
/// </para>
/// <para>
/// Without this attribute, the root <see cref="Microsoft.AspNetCore.Routing.IEndpointRouteBuilder"/>
/// is passed directly and the module is responsible for its own full route paths.
/// </para>
/// </summary>
/// <example>
/// <code>
/// // GET /orders, GET /orders/{id}, POST /orders
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
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class RoutePrefixAttribute : Attribute {

    /// <summary>The route prefix applied to all endpoints in the module.</summary>
    public string Prefix { get; }

    /// <param name="prefix">
    /// The route prefix. Should start with <c>/</c>.
    /// Example: <c>"/orders"</c>, <c>"/api/products"</c>.
    /// </param>
    public RoutePrefixAttribute(string prefix) {
        ArgumentException.ThrowIfNullOrWhiteSpace(prefix);
        Prefix = prefix;
    }
}