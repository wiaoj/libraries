using Microsoft.AspNetCore.Http;
using Wiaoj.Preconditions;
using Wiaoj.RateLimiting.AspNetCore;

#pragma warning disable IDE0130 // Namespace matches ASP.NET Core endpoint routing convention
namespace Microsoft.AspNetCore.Builder;
#pragma warning restore IDE0130

/// <summary>
/// Extension methods for attaching rate limiting metadata to ASP.NET Core endpoint routes.
/// </summary>
public static class RateLimitEndpointConventionBuilderExtensions {
    /// <summary>
    /// Configures a static rate limiting cost for this endpoint.
    /// </summary>
    public static TBuilder WithRateLimitCost<TBuilder>(this TBuilder builder, int cost) where TBuilder : IEndpointConventionBuilder {
        Preca.ThrowIfNull(builder);
        if(cost <= 0) throw new ArgumentOutOfRangeException(nameof(cost), "Cost must be greater than zero.");

        builder.Add(endpointBuilder => {
            endpointBuilder.Metadata.Add(new RateLimitMetadata { Cost = cost });
        });
        return builder;
    }

    /// <summary>
    /// Configures a dynamic cost resolver for this endpoint (e.g. calculated from query string or bulk batch size).
    /// </summary>
    public static TBuilder WithRateLimitCost<TBuilder>(
        this TBuilder builder,
        Func<HttpContext, int> costResolver) where TBuilder : IEndpointConventionBuilder {
        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNull(costResolver);

        builder.Add(endpointBuilder => {
            endpointBuilder.Metadata.Add(new RateLimitMetadata { DynamicCostResolver = costResolver });
        });
        return builder;
    }

    /// <summary>
    /// Disables rate limiting for requests matching this endpoint.
    /// </summary>
    public static TBuilder DisableRateLimiting<TBuilder>(this TBuilder builder) where TBuilder : IEndpointConventionBuilder {
        Preca.ThrowIfNull(builder);

        builder.Add(endpointBuilder => {
            endpointBuilder.Metadata.Add(new RateLimitMetadata { IsDisabled = true });
        });
        return builder;
    }
}