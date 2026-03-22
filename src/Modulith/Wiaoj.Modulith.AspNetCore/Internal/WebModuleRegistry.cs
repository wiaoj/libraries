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
    /// Invokes <see cref="IWebModule.Configure"/> on all web modules in boot order.
    /// Called by <c>UseModulith()</c>.
    /// </summary>
    public void Configure(IApplicationBuilder app) {
        foreach(IWebModule module in this._webModules)
            module.Configure(app);
    }
}