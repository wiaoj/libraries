using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Wiaoj.Preconditions;
using Wiaoj.WellKnown;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Microsoft.AspNetCore.Routing;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Serves RFC 9728 protected resource metadata documents.
/// </summary>
public static class WellKnownEndpointExtensions {
    private static readonly JsonSerializerOptions DocumentJson = new() {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Serves the metadata document of every registered protected resource, each at the path derived from its identifier.
    /// </summary>
    /// <param name="app">The endpoint route builder.</param>
    /// <returns>The endpoint route builder for chaining.</returns>
    /// <exception cref="InvalidOperationException">
    /// No resource was registered, or two resources derive the same document path.
    /// </exception>
    /// <exception cref="OptionsValidationException">A resource's options are invalid.</exception>
    /// <remarks>
    /// <para>
    /// The options are resolved here, while the application builds its endpoints, so an invalid document fails at
    /// startup rather than on the first client that asks for it. The route comes from
    /// <see cref="ProtectedResourceMetadataUri.PathFor"/>, the same derivation a challenge uses to advertise the URL.
    /// </para>
    /// <para>
    /// The document is read from the options on every request, so a reloaded configuration is served without a
    /// restart — and validated again when it reloads. The route's path is fixed at startup.
    /// </para>
    /// </remarks>
    public static IEndpointRouteBuilder MapOAuthProtectedResource(this IEndpointRouteBuilder app) {
        Preca.ThrowIfNull(app);

        ProtectedResourceRegistry registry = app.ServiceProvider.GetService<ProtectedResourceRegistry>() is { Names.Count: > 0 } found
            ? found
            : throw new InvalidOperationException(
                "No protected resource is registered. Call services.AddOAuthProtectedResource(...) before mapping its metadata.");

        IOptionsMonitor<OAuthProtectedResourceOptions> monitor = app.ServiceProvider.GetRequiredService<IOptionsMonitor<OAuthProtectedResourceOptions>>();
        Dictionary<string, string> namesByPath = new(StringComparer.OrdinalIgnoreCase);

        foreach(string name in registry.Names) {
            string resource = monitor.Get(name).Resource!;
            string path = ProtectedResourceMetadataUri.PathFor(resource);

            if(namesByPath.TryGetValue(path, out string? other)) {
                throw new InvalidOperationException(
                    $"The protected resources '{other}' and '{name}' both publish their metadata at '{path}'. Each resource " +
                    "identifier must be distinct, since a client finds a document by its identifier.");
            }

            namesByPath[path] = name;
            MapDocument(app, name, path);
        }

        return app;
    }

    private static void MapDocument(IEndpointRouteBuilder app, string name, string path) {
        app.MapGet(path, (HttpContext context, IOptionsMonitor<OAuthProtectedResourceOptions> options) => {
                OAuthProtectedResourceOptions resource = options.Get(name);

                context.Response.Headers.CacheControl = resource.CacheDuration > TimeSpan.Zero
                    ? $"public, max-age={((long)resource.CacheDuration.TotalSeconds).ToString(CultureInfo.InvariantCulture)}"
                    : "no-cache";

                return Results.Json(OAuthProtectedResourceMetadata.FromOptions(resource), DocumentJson, "application/json", StatusCodes.Status200OK);
            })
            .AllowAnonymous()
            .WithTags("Well-Known")
            .WithName(name.Length == 0 ? "OAuthProtectedResourceMetadata" : $"OAuthProtectedResourceMetadata:{name}")
            .WithSummary("RFC 9728 OAuth 2.0 Protected Resource Metadata");
    }
}
