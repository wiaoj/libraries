using Wiaoj.Modulith.Internal;

namespace Wiaoj.Modulith;
/// <summary>
/// Declares that this module depends on one or more other modules.
/// <para>
/// The <see cref="ModuleRegistry"/> uses these declarations to build a dependency graph
/// and boot modules in topological order. A module's <c>Register</c> is always called
/// after all its dependencies have been registered.
/// </para>
/// </summary>
/// <example>
/// <code>
/// [DependsOn(typeof(CoreModule), typeof(InfraModule))]
/// public sealed class OrdersModule : IModule { ... }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class DependsOnAttribute : Attribute {

    /// <summary>The module types this module depends on.</summary>
    public IReadOnlyList<Type> Dependencies { get; }

    /// <param name="dependencies">One or more <see cref="IModule"/> implementation types.</param>
    public DependsOnAttribute(params Type[] dependencies) {
        foreach(Type t in dependencies) {
            if(typeof(IModule).IsAssignableFrom(t))
                continue;

            throw new ArgumentException(
                $"'{t.Name}' does not implement IModule and cannot be used as a dependency.",
                nameof(dependencies));
        }
        this.Dependencies = dependencies;
    }
}

/// <summary>
/// Conditionally loads this module based on a configuration flag.
/// <para>
/// The module is loaded only when <c>IConfiguration[<see cref="Key"/>]</c>
/// equals <c>"true"</c> (case-insensitive). If the key is absent, the module
/// is skipped by default. Set <see cref="LoadWhenMissing"/> to <c>true</c>
/// to invert this behavior.
/// </para>
/// </summary>
/// <example>
/// <code>
/// // appsettings.json: { "Features": { "Billing": true } }
/// [FeatureFlag("Features:Billing")]
/// public sealed class BillingModule : IModule { ... }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class FeatureFlagAttribute : Attribute {

    /// <summary>The configuration key to check (supports colon-delimited paths).</summary>
    public string Key { get; }

    /// <summary>
    /// When <c>true</c>, the module is loaded when the key is missing from configuration.
    /// Default: <c>false</c> (module is skipped when key is missing).
    /// </summary>
    public bool LoadWhenMissing { get; init; } = false;

    /// <param name="key">Configuration key, e.g. <c>"Features:Billing"</c>.</param>
    public FeatureFlagAttribute(string key) {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        this.Key = key;
    }
}

/// <summary>
/// Restricts this module to one or more named environments.
/// <para>
/// The module is loaded only when <c>IHostEnvironment.EnvironmentName</c>
/// matches one of the specified <see cref="Environments"/> (case-insensitive).
/// </para>
/// </summary>
/// <example>
/// <code>
/// [RequiresEnvironment("Development", "Staging")]
/// public sealed class SeedDataModule : IModule { ... }
///
/// [RequiresEnvironment("Production")]
/// public sealed class TelemetryModule : IModule { ... }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class RequiresEnvironmentAttribute : Attribute {

    /// <summary>The environment names in which this module is active.</summary>
    public IReadOnlyList<string> Environments { get; }

    /// <param name="environments">One or more environment names, e.g. <c>"Production"</c>.</param>
    public RequiresEnvironmentAttribute(params string[] environments) {
        ArgumentOutOfRangeException.ThrowIfZero(environments.Length);
        this.Environments = environments;
    }
}