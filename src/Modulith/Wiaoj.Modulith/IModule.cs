using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Wiaoj.Modulith; 
/// <summary>
/// Core contract for a self-contained application module.
/// <para>
/// A module encapsulates a bounded context's service registrations.
/// Modules are discovered, filtered, and booted in topological dependency order.
/// </para>
/// <para>
/// This interface is intentionally generic-host-only — it carries no ASP.NET Core
/// dependency. If your module needs to configure middleware or map endpoints,
/// implement <c>IWebModule</c> from <c>Wiaoj.Modulith.AspNetCore</c> instead.
/// </para>
/// </summary>
/// <example>
/// <code>
/// [DependsOn(typeof(CoreModule))]
/// [FeatureFlag("Orders:Enabled")]
/// public sealed class OrdersModule : IModule {
///     public string Name => "Orders";
///
///     public void Register(IServiceCollection services, IConfiguration configuration) {
///         services.AddScoped&lt;IOrderService, OrderService&gt;();
///         services.AddDbContext&lt;OrdersDbContext&gt;(opt =>
///             opt.UseSqlServer(configuration.GetConnectionString("Orders")));
///     }
/// }
/// </code>
/// </example>
public interface IModule {

    /// <summary>
    /// Human-readable name of the module. Used in logs and diagnostic output.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Registers the module's services into the DI container.
    /// Called once at startup, before the host is built, in topological dependency order.
    /// </summary>
    void Register(IServiceCollection services, IConfiguration configuration);
}