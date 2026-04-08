using Microsoft.AspNetCore.Builder;
using Wiaoj.Modulith.Internal;

namespace Wiaoj.Modulith.AspNetCore.Internal;
/// <summary>
/// Wraps <see cref="ModuleRegistry"/> and adds web-specific <see cref="IWebModule"/>
/// configuration orchestration.
/// Registered as a singleton by <c>AddModulithAspNetCore()</c>.
/// </summary>
internal sealed class WebModuleRegistry(ModuleRegistry coreRegistry) {

    private readonly IReadOnlyList<IWebModule> _webModules = [.. coreRegistry.Modules.OfType<IWebModule>()];

    /// <summary>
    /// Invokes <see cref="IWebModule.ConfigureAsync"/> on all web modules in boot order.
    /// Called by <c>UseModulithAsync()</c>.
    /// </summary>
    public async Task ConfigureAsync(IApplicationBuilder app) {
        foreach(IWebModule module in this._webModules)
           await module.ConfigureAsync(app);
    }
}