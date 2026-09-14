namespace Wiaoj.Modulith.Internal;

/// <summary>
/// Singleton that holds the sorted, active module instances.
/// Built once by <c>AddModulith()</c> and registered in DI.
/// <para>
/// Intentionally public and in the root namespace — the AspNetCore integration
/// package resolves it from DI to build <c>WebModuleRegistry</c>.
/// </para>
/// </summary>  
public sealed class ModuleRegistry {

    public IReadOnlyList<IModule> Modules { get; }
    public IReadOnlyList<IModuleLifecycle> LifecycleModules { get; }
    internal IReadOnlyList<SkippedModuleInfo> SkippedModules { get; }

    internal ModuleRegistry(IReadOnlyList<IModule> modules, IReadOnlyList<SkippedModuleInfo> skippedModules) {
        this.Modules = modules;
        this.LifecycleModules = modules.OfType<IModuleLifecycle>().ToList();
        this.SkippedModules = skippedModules;
    }
}