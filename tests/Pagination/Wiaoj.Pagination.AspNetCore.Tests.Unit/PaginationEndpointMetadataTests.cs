using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Wiaoj.Pagination.AspNetCore;
using Xunit;

namespace Wiaoj.Pagination.AspNetCore.Tests.Unit;

/// <summary>
/// WithPagination installs an endpoint filter, and a filter leaves no trace on the endpoint. The metadata is
/// what lets anything reading the endpoint afterwards — OpenAPI generation above all — see that it is
/// paginated and how.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Feature", "Pagination")]
[Trait("Component", "EndpointMetadata")]
public sealed class PaginationEndpointMetadataTests {

    private static Endpoint BuildEndpoint(Action<IEndpointRouteBuilder> configure) {
        ServiceCollection services = new();
        services.AddRouting();
        ServiceProvider provider = services.BuildServiceProvider();

        DefaultEndpointRouteBuilder routeBuilder = new(provider);
        configure(routeBuilder);

        return Assert.Single(routeBuilder.DataSources).Endpoints.Single();
    }

    private sealed class DefaultEndpointRouteBuilder(IServiceProvider serviceProvider) : IEndpointRouteBuilder {
        public IServiceProvider ServiceProvider { get; } = serviceProvider;
        public ICollection<EndpointDataSource> DataSources { get; } = [];
        public IApplicationBuilder CreateApplicationBuilder() => new ApplicationBuilder(this.ServiceProvider);
    }

    [Fact]
    public void WithPagination_Should_Mark_The_Endpoint_As_Paginated() {
        Endpoint endpoint = BuildEndpoint(routes => routes.MapGet("/items", () => Results.Ok()).WithPagination());

        PaginationEndpointMetadata metadata = Assert.Single(endpoint.Metadata.OfType<PaginationEndpointMetadata>());

        Assert.True(metadata.EmitsLinkHeaders);
        Assert.True(metadata.EvaluatesETag);
    }

    [Fact]
    public void WithPagination_Should_Record_The_Options_It_Was_Configured_With() {
        Endpoint endpoint = BuildEndpoint(routes => routes
            .MapGet("/items", () => Results.Ok())
            .WithPagination(options => {
                options.EnableETag = false;
                options.EnableLinkHeaders = false;
            }));

        PaginationEndpointMetadata metadata = Assert.Single(endpoint.Metadata.OfType<PaginationEndpointMetadata>());

        Assert.False(metadata.EmitsLinkHeaders);
        Assert.False(metadata.EvaluatesETag);
    }

    [Fact]
    public void An_Endpoint_Without_Pagination_Should_Carry_No_Metadata() {
        Endpoint endpoint = BuildEndpoint(routes => routes.MapGet("/items", () => Results.Ok()));

        Assert.Empty(endpoint.Metadata.OfType<PaginationEndpointMetadata>());
    }
}
