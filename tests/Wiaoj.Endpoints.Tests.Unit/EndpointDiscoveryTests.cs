using Microsoft.AspNetCore.Routing;
using Wiaoj.Endpoints.Internal;

namespace Wiaoj.Endpoints.Tests.Unit;
public sealed class EndpointDiscoveryTests {
    // ── FindIn ────────────────────────────────────────────────────────────────

    [Fact]
    public void FindIn_CurrentAssembly_FindsConcreteEndpoints() {
        IEnumerable<Type> result = EndpointDiscovery.FindIn(typeof(EndpointDiscoveryTests).Assembly);

        Assert.Contains(typeof(ConcreteEndpointA), result);
        Assert.Contains(typeof(ConcreteEndpointB), result);
    }

    [Fact]
    public void FindIn_CurrentAssembly_ExcludesAbstractTypes() {
        IEnumerable<Type> result = EndpointDiscovery.FindIn(typeof(EndpointDiscoveryTests).Assembly);

        Assert.DoesNotContain(typeof(AbstractEndpoint), result);
    }

    [Fact]
    public void FindIn_CurrentAssembly_ExcludesNonEndpointTypes() {
        IEnumerable<Type> result = EndpointDiscovery.FindIn(typeof(EndpointDiscoveryTests).Assembly);

        Assert.DoesNotContain(typeof(EndpointDiscoveryTests), result);
        Assert.DoesNotContain(typeof(string), result);
    }

    // ── CreateInstance ────────────────────────────────────────────────────────

    [Fact]
    public void CreateInstance_WithValidType_ReturnsInstance() {
        IEndpoint instance = EndpointDiscovery.CreateInstance(typeof(ConcreteEndpointA));

        Assert.NotNull(instance);
        Assert.IsType<ConcreteEndpointA>(instance);
    }

    [Fact]
    public void CreateInstance_WithTypeWithoutParameterlessConstructor_Throws() {
        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => EndpointDiscovery.CreateInstance(typeof(NoDefaultCtorEndpoint)));

        Assert.Contains(typeof(NoDefaultCtorEndpoint).FullName, ex.Message);
    }

    // ── RoutePrefixAttribute ──────────────────────────────────────────────────

    [Fact]
    public void RoutePrefix_StoresPrefix() {
        RoutePrefixAttribute attr = new("/orders");

        Assert.Equal("/orders", attr.Prefix);
    }

    [Fact]
    public void RoutePrefix_WithNullPrefix_Throws() {
        Assert.ThrowsAny<ArgumentException>(() => new RoutePrefixAttribute(null!));
    }

    [Fact]
    public void RoutePrefix_WithWhitespacePrefix_Throws() {
        Assert.Throws<ArgumentException>(() => new RoutePrefixAttribute("   "));
    }

    [Fact]
    public void RoutePrefix_AppliedToClass_IsReadableViaReflection() {
        RoutePrefixAttribute? attr = typeof(PrefixedEndpoint)
            .GetCustomAttributes(typeof(RoutePrefixAttribute), inherit: false)
            .Cast<RoutePrefixAttribute>()
            .SingleOrDefault();

        Assert.NotNull(attr);
        Assert.Equal("/prefixed", attr.Prefix);
    }

    // ── Stub types ────────────────────────────────────────────────────────────

    internal sealed class ConcreteEndpointA : IEndpoint {
        public void Map(IEndpointRouteBuilder app) { }
    }

    internal sealed class ConcreteEndpointB : IEndpoint {
        public void Map(IEndpointRouteBuilder app) { }
    }

    internal abstract class AbstractEndpoint : IEndpoint {
        public abstract void Map(IEndpointRouteBuilder app);
    }

    [RoutePrefix("/prefixed")]
    internal sealed class PrefixedEndpoint : IEndpoint {
        public void Map(IEndpointRouteBuilder app) { }
    }

    internal sealed class NoDefaultCtorEndpoint : IEndpoint {
        public NoDefaultCtorEndpoint(string required) { }
        public void Map(IEndpointRouteBuilder app) { }
    }
}