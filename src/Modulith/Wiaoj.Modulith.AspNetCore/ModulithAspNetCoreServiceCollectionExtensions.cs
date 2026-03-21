using Wiaoj.Modulith.AspNetCore.Internal;
using Wiaoj.Modulith.Internal;

#pragma warning disable IDE0130
namespace Microsoft.Extensions.DependencyInjection;
#pragma warning restore IDE0130

/// <summary>
/// Extension methods for integrating Wiaoj.Modulith with ASP.NET Core.
/// </summary>
public static class ModulithAspNetCoreServiceCollectionExtensions {

    /// <summary>
    /// Adds ASP.NET Core web module support on top of an existing
    /// <c>AddModulith()</c> registration.
    /// <para>
    /// Call <c>AddModulith()</c> first to register modules and the core
    /// infrastructure, then call this method to enable
    /// <see cref="Wiaoj.Modulith.AspNetCore.IWebModule.Configure"/> orchestration
    /// via <c>app.UseModulith()</c>.
    /// </para>
    /// </summary>
    /// <example>
    /// <code>
    /// // Program.cs (ASP.NET Core WebApplication)
    /// builder.Services
    ///     .AddModulith(cfg => cfg.AddModulesFromAssemblyContaining&lt;Program&gt;())
    ///     .AddModulithAspNetCore();
    ///
    /// var app = builder.Build();
    /// app.UseModulith();
    /// app.Run();
    /// </code>
    /// </example>
    public static IServiceCollection AddModulithAspNetCore(this IServiceCollection services) {
        services.AddSingleton<WebModuleRegistry>(sp => {
            ModuleRegistry core = sp.GetRequiredService<ModuleRegistry>();
            return new WebModuleRegistry(core);
        });

        return services;
    }
}