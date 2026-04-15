using Microsoft.AspNetCore.Builder;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Wiaoj.Modulith;
#pragma warning restore IDE0130 // Namespace does not match folder structure
/// <summary>
/// Extends <see cref="IModule"/> with ASP.NET Core middleware and endpoint configuration.
/// <para>
/// Implement this interface (instead of or alongside <see cref="IModule"/>) when your
/// module needs to register middleware, map endpoints, or configure routing.
/// </para>
/// <para>
/// <see cref="ConfigureAsync"/> is called by <c>app.UseModulithAsync()</c> after the host is built,
/// in topological dependency order.
/// </para>
/// </summary>
/// <example>
/// <code>
/// [DependsOn(typeof(CoreModule))]
/// public sealed class OrdersModule : IWebModule {
///     public string Name => "Orders";
///
///     public void Register(IServiceCollection services, IConfiguration configuration) {
///         services.AddScoped&lt;IOrderService, OrderService&gt;();
///     }
///
///     public Task ConfigureAsync(IApplicationBuilder app) {
///         if (app is WebApplication web)
///             web.MapGroup("/orders").MapOrderEndpoints();
///     }
/// }
/// </code>
/// </example>
public interface IWebModule : IModule {

    /// <summary>
    /// Configures the module's middleware pipeline or endpoint mappings.
    /// Called once after the <see cref="WebApplication"/> has been built.
    /// </summary>
    /// <param name="app">
    /// The application builder. Cast to <see cref="WebApplication"/> to access
    /// endpoint routing APIs.
    /// </param>
    Task ConfigureAsync(IApplicationBuilder app);
}