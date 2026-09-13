using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using Wiaoj.WellKnown;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Microsoft.AspNetCore.Routing;
#pragma warning restore IDE0130 // Namespace does not match folder structure

public static class WellKnownEndpointExtensions {
    /// <summary>
    /// /.well-known/oauth-protected-resource endpoint'ini dinlemeye açar.
    /// </summary>
    public static IEndpointRouteBuilder MapOAuthProtectedResource(this IEndpointRouteBuilder app) {
        app.MapGet(".well-known/oauth-protected-resource", (
            IOptions<OAuthProtectedResourceOptions> options,
            HttpContext context) => {

                OAuthProtectedResourceOptions opt = options.Value;

                context.Response.Headers.CacheControl = $"public, max-age={(int)opt.CacheDuration.TotalSeconds}";

                string resourceUrl = opt.Resource
                    ?? $"{context.Request.Scheme}://{context.Request.Host}";

                OAuthProtectedResourceMetadata metadata = new() {
                    Resource = resourceUrl,
                    AuthorizationServers = opt.AuthorizationServers,
                    ScopesSupported = opt.Scopes.Count > 0 ? opt.Scopes.OrderBy(s => s).ToList() : null,
                    BearerMethodsSupported = ["header"],
                    ResourceDocumentation = opt.ResourceDocumentation
                };

                return TypedResults.Ok(metadata);
            })
        .AllowAnonymous()
        .WithTags("Well-Known")
        .WithSummary("RFC 9728 OAuth 2.0 Protected Resource Metadata");

        return app;
    }
}