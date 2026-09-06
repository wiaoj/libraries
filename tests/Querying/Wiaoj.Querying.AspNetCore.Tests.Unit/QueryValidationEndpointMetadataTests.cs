using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Wiaoj.Querying.AspNetCore;
using Xunit;

namespace Wiaoj.Querying.AspNetCore.Tests.Unit;

/// <summary>
/// The validation filter is installed through a filter factory that resolves the schema from the container,
/// so nothing about it reaches the endpoint. The metadata records which entity's schema governs the
/// endpoint, which is what anything reading endpoint metadata needs in order to resolve the schema itself.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Feature", "Querying")]
[Trait("Component", "EndpointMetadata")]
public sealed class QueryValidationEndpointMetadataTests {

    private sealed class Product {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    private sealed class RouteBuilder(IServiceProvider serviceProvider) : IEndpointRouteBuilder {
        public IServiceProvider ServiceProvider { get; } = serviceProvider;
        public ICollection<EndpointDataSource> DataSources { get; } = [];
        public IApplicationBuilder CreateApplicationBuilder() => new ApplicationBuilder(this.ServiceProvider);
    }

    private static Endpoint BuildEndpoint(Action<IEndpointRouteBuilder> configure) {
        ServiceCollection services = new();
        services.AddRouting();

        // The container-resolving overload builds its filter at endpoint-build time, so the schema has to be
        // there for the endpoint to exist at all.
        services.AddSingleton(new QuerySchema<Product>());
        ServiceProvider provider = services.BuildServiceProvider();

        RouteBuilder routes = new(provider);
        configure(routes);

        return Assert.Single(routes.DataSources).Endpoints.Single();
    }

    [Fact]
    public void WithQueryValidation_Should_Record_The_Entity_Whose_Schema_Applies() {
        QuerySchema<Product> schema = new();

        Endpoint endpoint = BuildEndpoint(routes => routes
            .MapGet("/products", () => Results.Ok())
            .WithQueryValidation<RouteHandlerBuilder, Product>(schema));

        QueryValidationEndpointMetadata metadata =
            Assert.Single(endpoint.Metadata.OfType<QueryValidationEndpointMetadata>());

        Assert.Equal(typeof(Product), metadata.EntityType);
    }

    [Fact]
    public void WithQueryValidation_Should_Record_The_Entity_When_The_Schema_Comes_From_The_Container() {
        Endpoint endpoint = BuildEndpoint(routes => routes
            .MapGet("/products", () => Results.Ok())
            .WithQueryValidation<Product>());

        QueryValidationEndpointMetadata metadata =
            Assert.Single(endpoint.Metadata.OfType<QueryValidationEndpointMetadata>());

        Assert.Equal(typeof(Product), metadata.EntityType);
    }

    [Fact]
    public void An_Endpoint_Without_Query_Validation_Should_Carry_No_Metadata() {
        Endpoint endpoint = BuildEndpoint(routes => routes.MapGet("/products", () => Results.Ok()));

        Assert.Empty(endpoint.Metadata.OfType<QueryValidationEndpointMetadata>());
    }

    [Fact]
    public void Ignored_And_Allowed_Parameters_Should_Reach_The_Endpoint_Alongside_The_Entity() {
        Endpoint endpoint = BuildEndpoint(routes => routes
            .MapGet("/products", () => Results.Ok())
            .WithQueryValidation<Product>()
            .IgnoreQueryParameters("utm_source")
            .AllowQueryParameters("page"));

        QueryValidationEndpointMetadata entity =
            Assert.Single(endpoint.Metadata.OfType<QueryValidationEndpointMetadata>());
        QueryValidationEndpointOptions options =
            Assert.Single(endpoint.Metadata.OfType<QueryValidationEndpointOptions>());

        Assert.Equal(typeof(Product), entity.EntityType);
        Assert.Contains("utm_source", options.IgnoredParameters);
        Assert.Contains("page", options.AllowedParameters);
    }
}
