using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Wiaoj.Results.AspNetCore.Internal;

namespace Wiaoj.Results;

/// <summary>
/// Extension methods for registering Result filters on Minimal API endpoints and groups.
/// </summary>
public static class ResultEndpointFilterExtensions {

    /// <summary>
    /// Adds an endpoint filter to an endpoint route that automatically translates <see cref="Result{TValue}"/> returns into HTTP responses.
    /// </summary>
    public static RouteHandlerBuilder WithResultFilter(this RouteHandlerBuilder builder) {
        ArgumentNullException.ThrowIfNull(builder);
        builder.AddEndpointFilter(new ResultEndpointFilter());
        return builder;
    }

    /// <summary>
    /// Adds an endpoint filter to a route group that automatically translates <see cref="Result{TValue}"/> returns into HTTP responses.
    /// </summary>
    public static RouteGroupBuilder WithResultFilter(this RouteGroupBuilder builder) {
        ArgumentNullException.ThrowIfNull(builder);
        builder.AddEndpointFilter(new ResultEndpointFilter());
        return builder;
    }
}