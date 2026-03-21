using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Wiaoj.Modulith.Internal;
using Xunit;

namespace Wiaoj.Modulith.Tests.Unit;
public sealed class TopologicalSorterTests {

    // ── Happy path ────────────────────────────────────────────────────────────

    [Fact]
    public void Sort_WithNoDependencies_ReturnsAllModules() {
        List<ModuleDescriptor> descriptors = [
            new(typeof(StandaloneA)),
            new(typeof(StandaloneB)),
            new(typeof(StandaloneC)),
        ];

        IReadOnlyList<ModuleDescriptor> result = TopologicalSorter.Sort(descriptors);

        Assert.Equal(3, result.Count);
        Assert.Contains(result, d => d.Type == typeof(StandaloneA));
        Assert.Contains(result, d => d.Type == typeof(StandaloneB));
        Assert.Contains(result, d => d.Type == typeof(StandaloneC));
    }

    [Fact]
    public void Sort_WithLinearChain_ReturnsCorrectOrder() {
        // StandaloneA → LinearB → LinearC
        List<ModuleDescriptor> descriptors = [
            new(typeof(LinearC)),
            new(typeof(StandaloneA)),
            new(typeof(LinearB)),
        ];

        IReadOnlyList<ModuleDescriptor> result = TopologicalSorter.Sort(descriptors);

        Assert.True(IndexOf<StandaloneA>(result) < IndexOf<LinearB>(result));
        Assert.True(IndexOf<LinearB>(result) < IndexOf<LinearC>(result));
    }

    [Fact]
    public void Sort_WithDiamondDependency_RootAlwaysBeforeLeaf() {
        // DiamondCore ← DiamondOrders    ← DiamondShipping
        //             ← DiamondInventory ↗
        List<ModuleDescriptor> descriptors = [
            new(typeof(DiamondShipping)),
            new(typeof(DiamondOrders)),
            new(typeof(DiamondInventory)),
            new(typeof(DiamondCore)),
        ];

        IReadOnlyList<ModuleDescriptor> result = TopologicalSorter.Sort(descriptors);

        int core = IndexOf<DiamondCore>(result);
        int orders = IndexOf<DiamondOrders>(result);
        int inventory = IndexOf<DiamondInventory>(result);
        int shipping = IndexOf<DiamondShipping>(result);

        Assert.True(core < orders, "Core must boot before Orders");
        Assert.True(core < inventory, "Core must boot before Inventory");
        Assert.True(orders < shipping, "Orders must boot before Shipping");
        Assert.True(inventory < shipping, "Inventory must boot before Shipping");
    }

    [Fact]
    public void Sort_WithSingleModule_ReturnsThatModule() {
        IReadOnlyList<ModuleDescriptor> result =
            TopologicalSorter.Sort([new(typeof(StandaloneA))]);

        Assert.Single(result);
        Assert.Equal(typeof(StandaloneA), result[0].Type);
    }

    [Fact]
    public void Sort_WithEmptyList_ReturnsEmpty() {
        IReadOnlyList<ModuleDescriptor> result = TopologicalSorter.Sort([]);

        Assert.Empty(result);
    }

    // ── Cycle detection ───────────────────────────────────────────────────────

    [Fact]
    public void Sort_WithDirectCycle_ThrowsAndMentionsBothModules() {
        List<ModuleDescriptor> descriptors = [
            new(typeof(CycleA)),
            new(typeof(CycleB)),
        ];

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => TopologicalSorter.Sort(descriptors));

        Assert.Contains("CycleA", ex.Message);
        Assert.Contains("CycleB", ex.Message);
    }

    [Fact]
    public void Sort_WithThreeModuleCycle_ThrowsAndMentionsAllModules() {
        List<ModuleDescriptor> descriptors = [
            new(typeof(Cycle3A)),
            new(typeof(Cycle3B)),
            new(typeof(Cycle3C)),
        ];

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => TopologicalSorter.Sort(descriptors));

        Assert.Contains("Cycle3A", ex.Message);
        Assert.Contains("Cycle3B", ex.Message);
        Assert.Contains("Cycle3C", ex.Message);
    }

    // ── Missing dependency ────────────────────────────────────────────────────

    [Fact]
    public void Sort_WhenDependencyNotRegistered_ThrowsAndMentionsBothNames() {
        // LinearB depends on StandaloneA but StandaloneA is not in the list
        List<ModuleDescriptor> descriptors = [new(typeof(LinearB))];

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => TopologicalSorter.Sort(descriptors));

        Assert.Contains("LinearB", ex.Message);
        Assert.Contains("StandaloneA", ex.Message);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static int IndexOf<T>(IReadOnlyList<ModuleDescriptor> list)
        => list.Select((d, i) => (d, i)).First(x => x.d.Type == typeof(T)).i;

    // ── Stub types ────────────────────────────────────────────────────────────

    private sealed class StandaloneA : IModule {
        public string Name => nameof(StandaloneA);
        public void Register(IServiceCollection s, IConfiguration c) { }
    }

    private sealed class StandaloneB : IModule {
        public string Name => nameof(StandaloneB);
        public void Register(IServiceCollection s, IConfiguration c) { }
    }

    private sealed class StandaloneC : IModule {
        public string Name => nameof(StandaloneC);
        public void Register(IServiceCollection s, IConfiguration c) { }
    }

    // Linear chain: StandaloneA → LinearB → LinearC
    [DependsOn(typeof(StandaloneA))]
    private sealed class LinearB : IModule {
        public string Name => nameof(LinearB);
        public void Register(IServiceCollection s, IConfiguration c) { }
    }

    [DependsOn(typeof(LinearB))]
    private sealed class LinearC : IModule {
        public string Name => nameof(LinearC);
        public void Register(IServiceCollection s, IConfiguration c) { }
    }

    // Diamond
    private sealed class DiamondCore : IModule {
        public string Name => nameof(DiamondCore);
        public void Register(IServiceCollection s, IConfiguration c) { }
    }

    [DependsOn(typeof(DiamondCore))]
    private sealed class DiamondOrders : IModule {
        public string Name => nameof(DiamondOrders);
        public void Register(IServiceCollection s, IConfiguration c) { }
    }

    [DependsOn(typeof(DiamondCore))]
    private sealed class DiamondInventory : IModule {
        public string Name => nameof(DiamondInventory);
        public void Register(IServiceCollection s, IConfiguration c) { }
    }

    [DependsOn(typeof(DiamondOrders), typeof(DiamondInventory))]
    private sealed class DiamondShipping : IModule {
        public string Name => nameof(DiamondShipping);
        public void Register(IServiceCollection s, IConfiguration c) { }
    }

    // Direct cycle: CycleA ↔ CycleB
    [DependsOn(typeof(CycleB))]
    private sealed class CycleA : IModule {
        public string Name => nameof(CycleA);
        public void Register(IServiceCollection s, IConfiguration c) { }
    }

    [DependsOn(typeof(CycleA))]
    private sealed class CycleB : IModule {
        public string Name => nameof(CycleB);
        public void Register(IServiceCollection s, IConfiguration c) { }
    }

    // Three-module cycle: Cycle3A → Cycle3B → Cycle3C → Cycle3A
    [DependsOn(typeof(Cycle3C))]
    private sealed class Cycle3A : IModule {
        public string Name => nameof(Cycle3A);
        public void Register(IServiceCollection s, IConfiguration c) { }
    }

    [DependsOn(typeof(Cycle3A))]
    private sealed class Cycle3B : IModule {
        public string Name => nameof(Cycle3B);
        public void Register(IServiceCollection s, IConfiguration c) { }
    }

    [DependsOn(typeof(Cycle3B))]
    private sealed class Cycle3C : IModule {
        public string Name => nameof(Cycle3C);
        public void Register(IServiceCollection s, IConfiguration c) { }
    }
}