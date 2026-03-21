using System.Reflection;

namespace Wiaoj.Modulith;
/// <summary>
/// Fluent builder for declaring which modules to load.
/// <para>
/// This builder is intentionally scoped to <em>discovery only</em> — it answers
/// "which modules exist". Runtime behaviour (lifetimes, timeouts, logging) is
/// configured separately via <see cref="ModulithOptions"/>.
/// </para>
/// </summary>
public interface IModulithBuilder {

    // ── Assembly scanning ─────────────────────────────────────────────────────

    /// <summary>
    /// Scans the assembly containing <typeparamref name="TMarker"/> for all
    /// non-abstract <see cref="IModule"/> implementations.
    /// </summary>
    IModulithBuilder AddModulesFromAssemblyContaining<TMarker>();

    /// <summary>
    /// Scans <paramref name="assembly"/> for all non-abstract <see cref="IModule"/>
    /// implementations.
    /// </summary>
    IModulithBuilder AddModulesFromAssembly(Assembly assembly);

    // ── Manual registration ───────────────────────────────────────────────────

    /// <summary>Manually registers a specific module type.</summary>
    IModulithBuilder AddModule<TModule>() where TModule : IModule;

    /// <summary>Manually registers a specific module type.</summary>
    IModulithBuilder AddModule(Type moduleType);

    // ── Guard ─────────────────────────────────────────────────────────────────

    /// <summary>Returns <c>true</c> if <paramref name="moduleType"/> is already registered.</summary>
    bool IsRegistered(Type moduleType);
}