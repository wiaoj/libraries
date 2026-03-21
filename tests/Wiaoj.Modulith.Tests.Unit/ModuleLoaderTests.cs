using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using Wiaoj.Modulith.Internal;
using Xunit;

namespace Wiaoj.Modulith.Tests.Unit;
public sealed class ModuleLoaderTests {

    private static readonly ModulithOptions DefaultOptions = new();

    // ── Environment filter ────────────────────────────────────────────────────

    [Fact]
    public void LoadActive_ModuleWithMatchingEnvironment_IsIncluded() {
        IReadOnlyList<ModuleDescriptor> result = Load<DevOnlyModule>(environment: "Development");

        Assert.Single(result);
        Assert.Equal(typeof(DevOnlyModule), result[0].Type);
    }

    [Fact]
    public void LoadActive_ModuleWithNonMatchingEnvironment_IsExcluded() {
        IReadOnlyList<ModuleDescriptor> result = Load<DevOnlyModule>(environment: "Production");

        Assert.Empty(result);
    }

    [Fact]
    public void LoadActive_EnvironmentComparison_IsCaseInsensitive() {
        IReadOnlyList<ModuleDescriptor> result = Load<DevOnlyModule>(environment: "DEVELOPMENT");

        Assert.Single(result);
    }

    [Fact]
    public void LoadActive_ModuleWithMultipleEnvironments_MatchesAny() {
        IReadOnlyList<ModuleDescriptor> staging = Load<StagingOrProdModule>(environment: "Staging");
        IReadOnlyList<ModuleDescriptor> production = Load<StagingOrProdModule>(environment: "Production");

        Assert.Single(staging);
        Assert.Single(production);
    }

    [Fact]
    public void LoadActive_ModuleWithNoEnvironmentRestriction_IsAlwaysIncluded() {
        IReadOnlyList<ModuleDescriptor> result = Load<UnrestrictedModule>(environment: "Production");

        Assert.Single(result);
    }

    // ── Feature flag filter ───────────────────────────────────────────────────

    [Fact]
    public void LoadActive_FeatureFlagTrue_IsIncluded() {
        IReadOnlyList<ModuleDescriptor> result = Load<FlaggedModule>(
            config: new() { ["Features:Billing"] = "true" });

        Assert.Single(result);
    }

    [Fact]
    public void LoadActive_FeatureFlagFalse_IsExcluded() {
        IReadOnlyList<ModuleDescriptor> result = Load<FlaggedModule>(
            config: new() { ["Features:Billing"] = "false" });

        Assert.Empty(result);
    }

    [Theory]
    [InlineData("TRUE")]
    [InlineData("True")]
    [InlineData("true")]
    public void LoadActive_FeatureFlagComparison_IsCaseInsensitive(string value) {
        IReadOnlyList<ModuleDescriptor> result = Load<FlaggedModule>(
            config: new() { ["Features:Billing"] = value });

        Assert.Single(result);
    }

    [Fact]
    public void LoadActive_FeatureFlagKeyMissing_IsExcludedByDefault() {
        IReadOnlyList<ModuleDescriptor> result = Load<FlaggedModule>(config: new());

        Assert.Empty(result);
    }

    [Fact]
    public void LoadActive_FeatureFlagKeyMissing_WithLoadWhenMissing_IsIncluded() {
        IReadOnlyList<ModuleDescriptor> result = Load<LoadWhenMissingModule>(config: new());

        Assert.Single(result);
    }

    [Fact]
    public void LoadActive_LoadWhenMissing_WhenKeyExplicitlyFalse_IsExcluded() {
        IReadOnlyList<ModuleDescriptor> result = Load<LoadWhenMissingModule>(
            config: new() { ["Features:Legacy"] = "false" });

        Assert.Empty(result);
    }

    // ── Combined filters ──────────────────────────────────────────────────────

    [Fact]
    public void LoadActive_BothFiltersPass_IsIncluded() {
        IReadOnlyList<ModuleDescriptor> result = Load<EnvironmentAndFlagModule>(
            environment: "Production",
            config: new() { ["Features:NewCheckout"] = "true" });

        Assert.Single(result);
    }

    [Fact]
    public void LoadActive_EnvironmentPassesFlagFails_IsExcluded() {
        IReadOnlyList<ModuleDescriptor> result = Load<EnvironmentAndFlagModule>(
            environment: "Production",
            config: new() { ["Features:NewCheckout"] = "false" });

        Assert.Empty(result);
    }

    [Fact]
    public void LoadActive_EnvironmentFailsFlagPasses_IsExcluded() {
        IReadOnlyList<ModuleDescriptor> result = Load<EnvironmentAndFlagModule>(
            environment: "Development",
            config: new() { ["Features:NewCheckout"] = "true" });

        Assert.Empty(result);
    }

    // ── Non-module types ──────────────────────────────────────────────────────

    [Fact]
    public void LoadActive_AbstractType_IsIgnored() {
        IReadOnlyList<ModuleDescriptor> result = LoadTypes([typeof(AbstractModule)]);

        Assert.Empty(result);
    }

    [Fact]
    public void LoadActive_NonModuleType_IsIgnored() {
        IReadOnlyList<ModuleDescriptor> result = LoadTypes([typeof(string)]);

        Assert.Empty(result);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static IReadOnlyList<ModuleDescriptor> Load<TModule>(
        string environment = "Development",
        Dictionary<string, string?>? config = null)
        => LoadTypes([typeof(TModule)], environment, config);

    private static IReadOnlyList<ModuleDescriptor> LoadTypes(
        IReadOnlyList<Type> types,
        string environment = "Development",
        Dictionary<string, string?>? config = null) {

        IHostEnvironment env = Substitute.For<IHostEnvironment>();
        env.EnvironmentName.Returns(environment);

        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(config ?? [])
            .Build();

        return ModuleLoader.LoadActive(types, configuration, env, DefaultOptions, logger: null);
    }

    // ── Stub types ────────────────────────────────────────────────────────────

    private sealed class UnrestrictedModule : IModule {
        public string Name => nameof(UnrestrictedModule);
        public void Register(IServiceCollection s, IConfiguration c) { }
    }

    [RequiresEnvironment("Development")]
    private sealed class DevOnlyModule : IModule {
        public string Name => nameof(DevOnlyModule);
        public void Register(IServiceCollection s, IConfiguration c) { }
    }

    [RequiresEnvironment("Staging", "Production")]
    private sealed class StagingOrProdModule : IModule {
        public string Name => nameof(StagingOrProdModule);
        public void Register(IServiceCollection s, IConfiguration c) { }
    }

    [FeatureFlag("Features:Billing")]
    private sealed class FlaggedModule : IModule {
        public string Name => nameof(FlaggedModule);
        public void Register(IServiceCollection s, IConfiguration c) { }
    }

    [FeatureFlag("Features:Legacy", LoadWhenMissing = true)]
    private sealed class LoadWhenMissingModule : IModule {
        public string Name => nameof(LoadWhenMissingModule);
        public void Register(IServiceCollection s, IConfiguration c) { }
    }

    [RequiresEnvironment("Production")]
    [FeatureFlag("Features:NewCheckout")]
    private sealed class EnvironmentAndFlagModule : IModule {
        public string Name => nameof(EnvironmentAndFlagModule);
        public void Register(IServiceCollection s, IConfiguration c) { }
    }

    private abstract class AbstractModule : IModule {
        public string Name => nameof(AbstractModule);
        public abstract void Register(IServiceCollection s, IConfiguration c);
    }
}