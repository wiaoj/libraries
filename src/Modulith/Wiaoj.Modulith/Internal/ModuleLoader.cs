using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Wiaoj.Modulith.Internal;

internal record SkippedModuleInfo(Type ModuleType, string Reason);

internal sealed record ModuleLoadResult(
    IReadOnlyList<ModuleDescriptor> ActiveDescriptors,
    IReadOnlyList<SkippedModuleInfo> SkippedModules);

internal static class ModuleLoader {

    public static ModuleLoadResult LoadActive(
        IReadOnlyList<Type> candidateTypes,
        IConfiguration configuration,
        IHostEnvironment environment,
        ModulithOptions options) {

        List<ModuleDescriptor> active = [];
        List<SkippedModuleInfo> skipped = [];

        foreach(Type type in candidateTypes) {
            if(!typeof(IModule).IsAssignableFrom(type) || type.IsAbstract || type.IsInterface)
                continue;

            ModuleDescriptor descriptor = new(type);

            if(!PassesEnvironmentFilter(descriptor, environment)) {
                skipped.Add(new(type, $"Not active in environment '{environment.EnvironmentName}'."));
                continue;
            }

            if(!PassesFeatureFlagFilter(descriptor, configuration, options)) {
                skipped.Add(new(type, $"Feature flag '{descriptor.FeatureFlag!.Key}' is disabled or missing."));
                continue;
            }

            active.Add(descriptor);
        }

        // Zincirleme kontrol: Eğer bağımlı olduğu modül kapatılmışsa, bu modülü de atla
        bool changed;
        do {
            changed = false;
            HashSet<Type> activeTypes = active.Select(d => d.Type).ToHashSet();

            for(int i = active.Count - 1; i >= 0; i--) {
                ModuleDescriptor current = active[i];
                Type? disabledDep = current.Dependencies.FirstOrDefault(dep =>
                    skipped.Any(s => s.ModuleType == dep));

                if(disabledDep is not null) {
                    active.RemoveAt(i);
                    skipped.Add(new(current.Type, $"Required dependency '{disabledDep.Name}' was disabled or skipped."));
                    changed = true;
                }
            }
        } while(changed);

        return new ModuleLoadResult(active, skipped);
    }

    private static bool PassesEnvironmentFilter(
        ModuleDescriptor descriptor, IHostEnvironment environment) {
        if(descriptor.RequiresEnvironment is null) return true;

        return descriptor.RequiresEnvironment.Environments
            .Any(e => string.Equals(e, environment.EnvironmentName, StringComparison.OrdinalIgnoreCase));
    }

    private static bool PassesFeatureFlagFilter(
        ModuleDescriptor descriptor, IConfiguration configuration, ModulithOptions options) {
        if(descriptor.FeatureFlag is null) return true;

        string? value = configuration[descriptor.FeatureFlag.Key];
        if(value is null)
            return descriptor.FeatureFlag.LoadWhenMissing || !options.SkipModulesWithMissingFeatureFlag;

        return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
    }
}