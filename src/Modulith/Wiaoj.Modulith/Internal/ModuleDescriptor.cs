using System.Reflection;

namespace Wiaoj.Modulith.Internal;
/// <summary>
/// Holds compile-time metadata about a discovered module type.
/// Built once by <see cref="ModuleLoader"/> at startup; immutable thereafter.
/// </summary>
internal sealed record ModuleDescriptor {

    /// <summary>The concrete module type.</summary>
    public Type Type { get; init; }

    /// <summary>Module types this module depends on (from <see cref="DependsOnAttribute"/>).</summary>
    public IReadOnlyList<Type> Dependencies { get; init; }

    /// <summary>The <see cref="FeatureFlagAttribute"/> applied to this type, if any.</summary>
    public FeatureFlagAttribute? FeatureFlag { get; init; }

    /// <summary>The <see cref="RequiresEnvironmentAttribute"/> applied to this type, if any.</summary>
    public RequiresEnvironmentAttribute? RequiresEnvironment { get; init; }

    public ModuleDescriptor(Type type) {
        this.Type = type;

        DependsOnAttribute? dep = type.GetCustomAttribute<DependsOnAttribute>(inherit: false);
        this.Dependencies = dep?.Dependencies ?? [];

        this.FeatureFlag = type.GetCustomAttribute<FeatureFlagAttribute>(inherit: false);
        this.RequiresEnvironment = type.GetCustomAttribute<RequiresEnvironmentAttribute>(inherit: false);
    }
}