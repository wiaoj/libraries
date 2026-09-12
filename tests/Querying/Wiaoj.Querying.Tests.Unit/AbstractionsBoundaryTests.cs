using System.Reflection;

namespace Wiaoj.Querying.Tests.Unit;

/// <summary>
/// The abstractions package exists so a contract assembly can carry a <see cref="QueryRequest"/> without
/// taking dependency injection with it. Nothing in the build enforces that; a single convenience reference
/// added later would undo it silently. This does.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Feature", "Querying")]
[Trait("Component", "Packaging")]
public sealed class AbstractionsBoundaryTests {
    private static readonly Assembly Abstractions = typeof(QueryRequest).Assembly;

    [Fact]
    public void The_Query_Language_Should_Live_In_The_Abstractions_Assembly() {
        Assert.Equal("Wiaoj.Querying.Abstractions", Abstractions.GetName().Name);
    }

    [Fact]
    public void The_Abstractions_Assembly_Should_Not_Reference_Microsoft_Extensions() {
        string[] offending = [.. Abstractions.GetReferencedAssemblies()
            .Select(reference => reference.Name!)
            .Where(name => name.StartsWith("Microsoft.Extensions", StringComparison.Ordinal))];

        Assert.Empty(offending);
    }

    [Theory]
    [InlineData(typeof(QueryRequest))]
    [InlineData(typeof(FilterConditionNode))]
    [InlineData(typeof(Sort))]
    [InlineData(typeof(QueryOperator))]
    [InlineData(typeof(QueryValidationResult))]
    public void Moved_Types_Should_Keep_The_Root_Namespace(Type type) {
        Assert.Equal("Wiaoj.Querying", type.Namespace);
    }

    [Fact]
    public void The_Engine_Assembly_Should_Forward_The_Moved_Types() {
        // Code compiled against an earlier Wiaoj.Querying resolves QueryRequest through this assembly.
        Assembly engine = typeof(QuerySchema<>).Assembly;

        Type[] forwarded = engine.GetForwardedTypes();

        Assert.Contains(typeof(QueryRequest), forwarded);
        Assert.Contains(typeof(FilterConditionNode), forwarded);
    }
}
