using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Wiaoj.Modulith;
using Wiaoj.Modulith.Internal;

#pragma warning disable IDE0130
namespace Microsoft.Extensions.DependencyInjection;
#pragma warning restore IDE0130

public static class ModulithServiceCollectionExtensions {

    /// <summary>
    /// Registers the Modulith infrastructure and all active modules.
    /// </summary>
    /// <param name="services">The application service collection.</param>
    /// <param name="configuration">
    /// The application configuration — used to evaluate <see cref="Wiaoj.Modulith.FeatureFlagAttribute"/>.
    /// Pass <c>builder.Configuration</c> in a WebApplication host.
    /// </param>
    /// <param name="environment">
    /// The host environment — used to evaluate <see cref="Wiaoj.Modulith.RequiresEnvironmentAttribute"/>.
    /// Pass <c>builder.Environment</c> in a WebApplication host.
    /// </param>
    /// <param name="configureModules">Declares which modules to load.</param>
    /// <param name="configureOptions">Configures runtime behaviour. Optional.</param>
    /// <example>
    /// <code>
    /// // WebApplication
    /// builder.Services.AddModulith(
    ///     builder.Configuration,
    ///     builder.Environment,
    ///     modules => modules.AddModulesFromAssemblyContaining&lt;Program&gt;());
    ///
    /// // Generic host
    /// hostBuilder.ConfigureServices((ctx, services) =>
    ///     services.AddModulith(
    ///         ctx.Configuration,
    ///         ctx.HostingEnvironment,
    ///         modules => modules.AddModulesFromAssemblyContaining&lt;Program&gt;()));
    /// </code>
    /// </example>
    public static IServiceCollection AddModulith(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment,
        Action<IModulithBuilder> configureModules,
        Action<ModulithOptions>? configureOptions = null) {

        // Validate and register options
        ModulithOptions options = new();
        configureOptions?.Invoke(options);

        ValidateOptionsResult validationResult = new ModulithOptionsValidator()
            .Validate(null, options);

        if(validationResult.Failed)
            throw new OptionsValidationException(
                nameof(ModulithOptions),
                typeof(ModulithOptions),
                validationResult.Failures);

        services
            .AddOptions<ModulithOptions>()
            .Configure(configureOptions ?? (_ => { }))
            .ValidateOnStart();

        services.AddSingleton<IValidateOptions<ModulithOptions>, ModulithOptionsValidator>();

        // Discover and filter modules
        ModulithBuilder builder = new();
        configureModules(builder);

        List<Type> candidates = builder.CandidateTypes.Distinct().ToList();

        IReadOnlyList<ModuleDescriptor> activeDescriptors =
            ModuleLoader.LoadActive(candidates, configuration, environment, options, logger: null);

        IReadOnlyList<ModuleDescriptor> sortedDescriptors =
            TopologicalSorter.Sort(activeDescriptors);

        // Instantiate and register each module in dependency order
        List<IModule> modules = [];

        foreach(ModuleDescriptor descriptor in sortedDescriptors) {
            IModule module = (IModule?)Activator.CreateInstance(descriptor.Type)
                ?? throw new InvalidOperationException(
                    $"Failed to create an instance of module '{descriptor.Type.Name}'. " +
                    "Ensure the module has a public parameterless constructor.");

            module.Register(services, configuration);
            modules.Add(module);

            services.TryAdd(new ServiceDescriptor(
                descriptor.Type, _ => module, options.ModuleLifetime));
        }

        services.AddSingleton(new ModuleRegistry(modules));
        services.AddHostedService<ModulithHostedService>();

        return services;
    }
}