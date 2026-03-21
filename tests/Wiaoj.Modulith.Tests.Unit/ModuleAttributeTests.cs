using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Wiaoj.Modulith.Tests.Unit;
public sealed class ModuleAttributeTests {
    // ── DependsOnAttribute ────────────────────────────────────────────────────

    [Fact]
    public void DependsOn_WithValidModuleTypes_StoresDependencies() {
        DependsOnAttribute attr = new(typeof(ModuleStubA), typeof(ModuleStubB));

        Assert.Equal(2, attr.Dependencies.Count);
        Assert.Contains(typeof(ModuleStubA), attr.Dependencies);
        Assert.Contains(typeof(ModuleStubB), attr.Dependencies);
    }

    [Fact]
    public void DependsOn_WithNonModuleType_ThrowsArgumentException() {
        ArgumentException ex = Assert.Throws<ArgumentException>(
            () => new DependsOnAttribute(typeof(string)));

        Assert.Contains("String", ex.Message);
        Assert.Contains("IModule", ex.Message);
    }

    [Fact]
    public void DependsOn_WithMixedValidAndInvalidTypes_Throws() {
        Assert.Throws<ArgumentException>(
            () => new DependsOnAttribute(typeof(ModuleStubA), typeof(int)));
    }

    // ── FeatureFlagAttribute ──────────────────────────────────────────────────

    [Fact]
    public void FeatureFlag_StoresKey() {
        FeatureFlagAttribute attr = new("Features:Billing");

        Assert.Equal("Features:Billing", attr.Key);
    }

    [Fact]
    public void FeatureFlag_LoadWhenMissing_DefaultsFalse() {
        FeatureFlagAttribute attr = new("Features:Billing");

        Assert.False(attr.LoadWhenMissing);
    }

    [Fact]
    public void FeatureFlag_WithNullKey_Throws() {
        Assert.ThrowsAny<ArgumentException>(() => new FeatureFlagAttribute(null!));
    }

    [Fact]
    public void FeatureFlag_WithWhitespaceKey_Throws() {
        Assert.Throws<ArgumentException>(() => new FeatureFlagAttribute("   "));
    }

    // ── RequiresEnvironmentAttribute ─────────────────────────────────────────

    [Fact]
    public void RequiresEnvironment_StoresEnvironments() {
        RequiresEnvironmentAttribute attr = new("Development", "Staging");

        Assert.Equal(2, attr.Environments.Count);
        Assert.Contains("Development", attr.Environments);
        Assert.Contains("Staging", attr.Environments);
    }

    [Fact]
    public void RequiresEnvironment_WithNoEnvironments_Throws() {
        Assert.Throws<ArgumentOutOfRangeException>(() => new RequiresEnvironmentAttribute());
    }

    // ── Stubs ─────────────────────────────────────────────────────────────────

    private sealed class ModuleStubA : IModule {
        public string Name => nameof(ModuleStubA);
        public void Register(IServiceCollection s, IConfiguration c) { }
    }

    private sealed class ModuleStubB : IModule {
        public string Name => nameof(ModuleStubB);
        public void Register(IServiceCollection s, IConfiguration c) { }
    }
}