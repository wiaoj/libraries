using Microsoft.AspNetCore.Builder;
namespace Wiaoj.Modulith.AspNetCore;
/// <summary>
/// Extends <see cref="IModule"/> with ASP.NET Core middleware and endpoint configuration.
/// <para>
/// Implement this interface (instead of or alongside <see cref="IModule"/>) when your
/// module needs to register middleware, map endpoints, or configure routing.
/// </para>
/// <para>
/// <see cref="Configure"/> is called by <c>app.UseModulith()</c> after the host is built,
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
///     public void Configure(IApplicationBuilder app) {
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
    void Configure(IApplicationBuilder app);
}