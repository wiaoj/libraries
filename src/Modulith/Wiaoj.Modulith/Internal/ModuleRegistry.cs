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

    /// <summary>Active modules in topological boot order.</summary>
    public IReadOnlyList<IModule> Modules { get; }

    /// <summary>Active lifecycle-aware modules in boot order.</summary>
    public IReadOnlyList<IModuleLifecycle> LifecycleModules { get; }

    public ModuleRegistry(IReadOnlyList<IModule> modules) {
        this.Modules = modules;
        this.LifecycleModules = modules.OfType<IModuleLifecycle>().ToList();
    }
}