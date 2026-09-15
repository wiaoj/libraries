using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wiaoj.Preconditions;
using Wiaoj.WellKnown;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Microsoft.AspNetCore.Routing;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Serves RFC 9728 protected resource and RFC 8414 authorization server metadata documents, and the RFC 9116 security.txt.
/// </summary>
public static class WellKnownEndpointExtensions {

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

    /// <summary>
    /// Serves the RFC 8414 metadata document of every registered authorization server, each at the path derived from its
    /// issuer.
    /// </summary>
    /// <param name="app">The endpoint route builder.</param>
    /// <returns>The endpoint route builder for chaining.</returns>
    /// <exception cref="InvalidOperationException">
    /// No authorization server was registered, or two issuers derive the same document path.
    /// </exception>
    /// <exception cref="OptionsValidationException">A server's options are invalid.</exception>
    /// <remarks>
    /// <para>
    /// As with <see cref="MapOAuthProtectedResource"/>, the options are resolved while the endpoints are built, so an
    /// invalid document fails at startup; the document is read from the options on every request.
    /// </para>
    /// <para>
    /// <c>https://auth.example.com/tenant1</c> and <c>https://auth.example.com/tenant1/</c> are different issuers that
    /// derive the same path (RFC 8414 §3.1 removes the terminating slash), so registering both fails here.
    /// </para>
    /// </remarks>
    public static IEndpointRouteBuilder MapOAuthAuthorizationServer(this IEndpointRouteBuilder app) {
        Preca.ThrowIfNull(app);

        AuthorizationServerRegistry registry = app.ServiceProvider.GetService<AuthorizationServerRegistry>() is { Names.Count: > 0 } found
            ? found
            : throw new InvalidOperationException(
                "No authorization server is registered. Call services.AddOAuthAuthorizationServer(...) before mapping its metadata.");

        IOptionsMonitor<OAuthAuthorizationServerOptions> monitor = app.ServiceProvider.GetRequiredService<IOptionsMonitor<OAuthAuthorizationServerOptions>>();
        Dictionary<string, string> namesByPath = new(StringComparer.OrdinalIgnoreCase);

        foreach(string name in registry.Names) {
            string path = AuthorizationServerMetadataUri.PathFor(monitor.Get(name).Issuer!);

            if(namesByPath.TryGetValue(path, out string? other)) {
                throw new InvalidOperationException(
                    $"The authorization servers '{other}' and '{name}' both publish their metadata at '{path}'. Each issuer must " +
                    "derive a distinct path, since a client finds a document by its issuer.");
            }

            namesByPath[path] = name;

            // A RequestDelegate rather than a lambda with bound parameters, so the endpoint works under Native AOT.
            app.MapGet(path, context => {
                    OAuthAuthorizationServerOptions server = context.RequestServices
                        .GetRequiredService<IOptionsMonitor<OAuthAuthorizationServerOptions>>()
                        .Get(name);

                    return WellKnownDocument.WriteAsync(context, OAuthAuthorizationServerMetadata.FromOptions(server).ToUtf8Json(), server.CacheDuration);
                })
                .WithMetadata(new ProducesResponseTypeMetadata(StatusCodes.Status200OK, typeof(OAuthAuthorizationServerMetadata), ["application/json"]))
                .AllowAnonymous()
                .WithTags("Well-Known")
                .WithName(name.Length == 0 ? "OAuthAuthorizationServerMetadata" : $"OAuthAuthorizationServerMetadata:{name}")
                .WithSummary("RFC 8414 OAuth 2.0 Authorization Server Metadata");
        }

        return app;
    }

    /// <summary>
    /// Serves the RFC 9116 <c>security.txt</c> at <c>/.well-known/security.txt</c>, and redirects <c>/security.txt</c>
    /// to it unless <see cref="SecurityTxtOptions.RedirectLegacyPath"/> is off.
    /// </summary>
    /// <param name="app">The endpoint route builder.</param>
    /// <returns>The endpoint route builder for chaining.</returns>
    /// <exception cref="InvalidOperationException"><c>AddSecurityTxt</c> was not called.</exception>
    /// <exception cref="OptionsValidationException">The options are invalid, or Expires has passed.</exception>
    /// <remarks>
    /// <para>
    /// The options are resolved while the endpoints are built, so an invalid or expired file fails at startup. An Expires
    /// more than a year ahead, or less than 30 days away, logs a warning here. The file is rendered from the options on
    /// every request, so a reloaded configuration is served without a restart.
    /// </para>
    /// <para>
    /// RFC 9116 §3 requires the file to be retrieved over https. Serve the application over https — behind a proxy that
    /// terminates TLS, that is the proxy's job — and list the public URL in <see cref="SecurityTxtOptions.Canonical"/>.
    /// </para>
    /// </remarks>
    public static IEndpointRouteBuilder MapSecurityTxt(this IEndpointRouteBuilder app) {
        Preca.ThrowIfNull(app);

        if(app.ServiceProvider.GetService<SecurityTxtRegistration>() is null) {
            throw new InvalidOperationException("security.txt is not registered. Call services.AddSecurityTxt(...) before mapping it.");
        }

        SecurityTxtOptions options = app.ServiceProvider.GetRequiredService<IOptionsMonitor<SecurityTxtOptions>>().CurrentValue;
        TimeProvider timeProvider = app.ServiceProvider.GetService<TimeProvider>() ?? TimeProvider.System;
        SecurityTxtEndpoint.WarnAboutExpiry(
            app.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(SecurityTxtEndpoint.LoggerCategory),
            options.Expires!.Value,
            timeProvider.GetUtcNow());

        SecurityTxtExpiredWarning expiredWarning = new();
        app.MapGet(SecurityTxtDocument.WellKnownPath, context => SecurityTxtEndpoint.WriteAsync(context, expiredWarning))
            .WithMetadata(new ProducesResponseTypeMetadata(StatusCodes.Status200OK, typeof(string), ["text/plain"]))
            .AllowAnonymous()
            .WithTags("Well-Known")
            .WithName("SecurityTxt")
            .WithSummary("RFC 9116 security.txt");

        if(options.RedirectLegacyPath) {
            app.MapGet(SecurityTxtDocument.LegacyPath, SecurityTxtEndpoint.RedirectAsync)
                .AllowAnonymous()
                .ExcludeFromDescription();
        }

        return app;
    }

    private static void MapDocument(IEndpointRouteBuilder app, string name, string path) {
        // A RequestDelegate rather than a lambda with bound parameters: the delegate overload needs no reflection or
        // runtime code generation, so the endpoint works under Native AOT, and the document is written through the
        // source-generated context for the same reason.
        app.MapGet(path, context => {
                OAuthProtectedResourceOptions resource = context.RequestServices
                    .GetRequiredService<IOptionsMonitor<OAuthProtectedResourceOptions>>()
                    .Get(name);

                return WellKnownDocument.WriteAsync(context, OAuthProtectedResourceMetadata.FromOptions(resource).ToUtf8Json(), resource.CacheDuration);
            })
            .WithMetadata(new ProducesResponseTypeMetadata(StatusCodes.Status200OK, typeof(OAuthProtectedResourceMetadata), ["application/json"]))
            .AllowAnonymous()
            .WithTags("Well-Known")
            .WithName(name.Length == 0 ? "OAuthProtectedResourceMetadata" : $"OAuthProtectedResourceMetadata:{name}")
            .WithSummary("RFC 9728 OAuth 2.0 Protected Resource Metadata");
    }
}
