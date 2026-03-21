using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Wiaoj.Modulith.Internal;

internal static class ModuleLoader {

    public static IReadOnlyList<ModuleDescriptor> LoadActive(
        IReadOnlyList<Type> candidateTypes,
        IConfiguration configuration,
        IHostEnvironment environment,
        ModulithOptions options,
        ILogger? logger) {

        List<ModuleDescriptor> active = [];

        foreach(Type type in candidateTypes) {
            if(!typeof(IModule).IsAssignableFrom(type) || type.IsAbstract || type.IsInterface)
                continue;

            ModuleDescriptor descriptor = new(type);

            if(!PassesEnvironmentFilter(descriptor, environment)) {
                if(options.LogSkippedModules)
                    logger?.LogInformation(
                        "[Modulith] Skipping {Module} — not active in environment '{Env}'.",
                        type.Name, environment.EnvironmentName);
                continue;
            }

            if(!PassesFeatureFlagFilter(descriptor, configuration, options)) {
                if(options.LogSkippedModules)
                    logger?.LogInformation(
                        "[Modulith] Skipping {Module} — feature flag '{Key}' is disabled.",
                        type.Name, descriptor.FeatureFlag!.Key);
                continue;
            }

            active.Add(descriptor);
        }

        return active;
    }

    private static bool PassesEnvironmentFilter(
        ModuleDescriptor descriptor, IHostEnvironment environment) {

        if(descriptor.RequiresEnvironment is null)
            return true;

        return descriptor.RequiresEnvironment.Environments
            .Any(e => string.Equals(e, environment.EnvironmentName, StringComparison.OrdinalIgnoreCase));
    }

    private static bool PassesFeatureFlagFilter(
        ModuleDescriptor descriptor, IConfiguration configuration, ModulithOptions options) {

        if(descriptor.FeatureFlag is null)
            return true;

        string? value = configuration[descriptor.FeatureFlag.Key];

        if(value is null)
            return descriptor.FeatureFlag.LoadWhenMissing || !options.SkipModulesWithMissingFeatureFlag;

        return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
    }
}