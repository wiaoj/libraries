using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Wiaoj.Modulith;
/// <summary>
/// Runtime configuration for the Modulith host.
/// <para>
/// Configure via <c>AddModulith(cfg => ..., opt => ...)</c>.
/// All options are validated at startup via <see cref="IValidateOptions{T}"/>.
/// </para>
/// </summary>
public sealed class ModulithOptions {

    /// <summary>
    /// Service lifetime used when registering module instances in the DI container.
    /// <br/>Default: <see cref="ServiceLifetime.Singleton"/>.
    /// <para>
    /// Modules are typically singletons — they hold no per-request state.
    /// Change this only if your module implementation needs a different scope
    /// (e.g. integration tests that swap modules per test run).
    /// </para>
    /// </summary>
    public ServiceLifetime ModuleLifetime { get; set; } = ServiceLifetime.Singleton;

    /// <summary>
    /// When <c>true</c>, a module whose <see cref="FeatureFlagAttribute"/> key is missing
    /// from configuration is treated as disabled (skipped).
    /// <br/>Default: <c>true</c>.
    /// </summary>
    public bool SkipModulesWithMissingFeatureFlag { get; set; } = true;

    /// <summary>
    /// When <c>true</c>, logs a warning for each module that was skipped due to a
    /// feature flag or environment restriction, instead of silently ignoring it.
    /// <br/>Default: <c>true</c>.
    /// </summary>
    public bool LogSkippedModules { get; set; } = true;

    /// <summary>
    /// Maximum allowed time for a single module's <c>OnStarting</c> lifecycle hook
    /// before the host startup is considered hung and a <see cref="TimeoutException"/>
    /// is thrown.
    /// <br/>Set to <see cref="Timeout.InfiniteTimeSpan"/> to disable.
    /// <br/>Default: 30 seconds.
    /// </summary>
    public TimeSpan StartupHookTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Maximum allowed time for a single module's <c>OnStopping</c> lifecycle hook.
    /// Exceeded hooks are logged and skipped — shutdown always proceeds.
    /// <br/>Set to <see cref="Timeout.InfiniteTimeSpan"/> to disable.
    /// <br/>Default: 10 seconds.
    /// </summary>
    public TimeSpan ShutdownHookTimeout { get; set; } = TimeSpan.FromSeconds(10);
}

/// <summary>
/// Validates <see cref="ModulithOptions"/> at application startup.
/// Registered automatically by <c>AddModulith()</c>.
/// </summary>
internal sealed class ModulithOptionsValidator : IValidateOptions<ModulithOptions> {
    public ValidateOptionsResult Validate(string? name, ModulithOptions options) {
        List<string> errors = [];

        if(options.ModuleLifetime is not (
            ServiceLifetime.Singleton or
            ServiceLifetime.Scoped or
            ServiceLifetime.Transient))
            errors.Add($"{nameof(options.ModuleLifetime)} has an invalid value: {options.ModuleLifetime}.");

        if(options.StartupHookTimeout <= TimeSpan.Zero && options.StartupHookTimeout != Timeout.InfiniteTimeSpan)
            errors.Add($"{nameof(options.StartupHookTimeout)} must be positive or Timeout.InfiniteTimeSpan.");

        if(options.ShutdownHookTimeout <= TimeSpan.Zero && options.ShutdownHookTimeout != Timeout.InfiniteTimeSpan)
            errors.Add($"{nameof(options.ShutdownHookTimeout)} must be positive or Timeout.InfiniteTimeSpan.");

        return errors.Count > 0
            ? ValidateOptionsResult.Fail(errors)
            : ValidateOptionsResult.Success;
    }
}