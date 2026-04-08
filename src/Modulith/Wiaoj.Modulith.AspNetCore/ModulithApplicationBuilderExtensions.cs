using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Wiaoj.Modulith.AspNetCore.Internal;

#pragma warning disable IDE0130
namespace Microsoft.AspNetCore.Builder;
#pragma warning restore IDE0130

/// <summary>
/// Extension methods for configuring the Modulith middleware pipeline
/// in an ASP.NET Core application.
/// </summary>
public static class ModulithApplicationBuilderExtensions { 
    /// <summary>
    /// Invokes <c>ConfigureAsync(IApplicationBuilder)</c> on all active
    /// <see cref="Wiaoj.Modulith.AspNetCore.IWebModule"/> implementations
    /// in topological boot order.
    /// <para>
    /// Call this after <c>app.Build()</c> in your <c>Program.cs</c>.
    /// Modules that only implement <see cref="Wiaoj.Modulith.IModule"/>
    /// (no web layer) are silently skipped.
    /// </para>
    /// </summary>
    /// <example>
    /// <code>
    /// var app = builder.Build();
    /// app.UseModulithAsync();
    /// app.Run();
    /// </code>
    /// </example>
    public static async ValueTask<IApplicationBuilder> UseModulith(this IApplicationBuilder app) {
        WebModuleRegistry registry = app.ApplicationServices
            .GetRequiredService<WebModuleRegistry>();
        await registry.ConfigureAsync(app);
        return app;
    }

    /// <inheritdoc cref="UseModulith(IApplicationBuilder)"/>
    public static async ValueTask<WebApplication> UseModulithAsync(this WebApplication app) {
        await ((IApplicationBuilder)app).UseModulith();
        return app;
    }
}