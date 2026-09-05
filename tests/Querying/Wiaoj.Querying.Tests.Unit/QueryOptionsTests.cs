using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Wiaoj.Querying.Tests.Unit;

/// <summary>
/// Unit tests for <see cref="QueryOptions"/> configuration, ignored parameters, and builder extension methods.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Feature", "Options")]
public class QueryOptionsTests {
    [Fact]
    public void IgnoredParameters_Should_Default_To_Empty_Collection() {
        // Arrange & Act
        QueryOptions options = new();

        // Assert
        Assert.NotNull(options.IgnoredParameters);
        Assert.Empty(options.IgnoredParameters);
    }

    [Fact]
    public void Builder_IgnoreParameters_With_Span_Should_Configure_Options_Case_Insensitively() {
        // Arrange
        ServiceCollection services = new();
        IQueryingBuilder builder = services.AddQuerying();

        // Act
        builder.IgnoreParameters("page", "size", "CURSOR");
        ServiceProvider provider = services.BuildServiceProvider();
        QueryOptions options = provider.GetRequiredService<IOptions<QueryOptions>>().Value;

        // Assert
        Assert.Equal(3, options.IgnoredParameters.Count);
        Assert.Contains("page", options.IgnoredParameters);
        Assert.Contains("PAGE", options.IgnoredParameters);
        Assert.Contains("Size", options.IgnoredParameters);
        Assert.Contains("cursor", options.IgnoredParameters);
    }

    [Fact]
    public void Builder_IgnoreParameters_With_Span_Should_Ignore_Whitespace_Or_Empty_Values() {
        // Arrange
        ServiceCollection services = new();
        IQueryingBuilder builder = services.AddQuerying();

        // Act
        builder.IgnoreParameters("page", "  ", "", null!);
        ServiceProvider provider = services.BuildServiceProvider();
        QueryOptions options = provider.GetRequiredService<IOptions<QueryOptions>>().Value;

        // Assert
        Assert.Single(options.IgnoredParameters);
        Assert.Contains("page", options.IgnoredParameters);
    }

    [Fact]
    public void Builder_IgnoreParameters_With_Enumerable_Should_Configure_Options() {
        // Arrange
        ServiceCollection services = new();
        IQueryingBuilder builder = services.AddQuerying();
        List<string> parameters = ["cursor", "limit", "offset"];

        // Act
        builder.IgnoreParameters(parameters);
        ServiceProvider provider = services.BuildServiceProvider();
        QueryOptions options = provider.GetRequiredService<IOptions<QueryOptions>>().Value;

        // Assert
        Assert.Equal(3, options.IgnoredParameters.Count);
        Assert.Contains("cursor", options.IgnoredParameters);
        Assert.Contains("limit", options.IgnoredParameters);
        Assert.Contains("offset", options.IgnoredParameters);
    }

    [Fact]
    public void Builder_IgnoreParameters_Should_Throw_When_Builder_Or_Enumerable_Is_Null() {
        // Arrange
        ServiceCollection services = new();
        IQueryingBuilder builder = services.AddQuerying();

        // Act & Assert
        Assert.ThrowsAny<ArgumentNullException>(() => ((IQueryingBuilder)null!).IgnoreParameters("page"));
        Assert.ThrowsAny<ArgumentNullException>(() => ((IQueryingBuilder)null!).IgnoreParameters((IEnumerable<string>)["page"]));
        Assert.ThrowsAny<ArgumentNullException>(() => builder.IgnoreParameters((IEnumerable<string>)null!));
    }
}
