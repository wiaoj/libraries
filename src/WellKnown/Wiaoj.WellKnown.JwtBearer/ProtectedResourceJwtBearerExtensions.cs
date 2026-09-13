using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Wiaoj.Preconditions;
using Wiaoj.WellKnown;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Microsoft.Extensions.DependencyInjection;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Advertises a protected resource's RFC 9728 metadata in the challenges a JwtBearer scheme sends.
/// </summary>
public static class ProtectedResourceJwtBearerExtensions {
    /// <summary>
    /// Adds <c>resource_metadata</c> to every 401 challenge of the <paramref name="authenticationScheme"/> JwtBearer
    /// scheme, pointing at the metadata of the resource registered under <paramref name="resourceName"/>.
    /// </summary>
    /// <param name="builder">The authentication builder the JwtBearer scheme was added to.</param>
    /// <param name="authenticationScheme">The JwtBearer scheme; <see cref="JwtBearerDefaults.AuthenticationScheme"/> by default.</param>
    /// <param name="resourceName">The protected resource to advertise; the unnamed resource by default.</param>
    /// <returns>The authentication builder for chaining.</returns>
    /// <remarks>
    /// <para>
    /// The challenge JwtBearer writes is kept whole: <c>error</c>, <c>error_description</c>, <c>scope</c> and
    /// <c>realm</c> stay as they are, and the parameter is added beside them. A handler the application set in
    /// <c>Events.OnChallenge</c> still runs first; if it handles the response itself, the response is left alone.
    /// </para>
    /// <para>
    /// The URL is derived from the resource's identifier by <see cref="ProtectedResourceMetadataUri.For"/> — the same
    /// derivation <c>MapOAuthProtectedResource</c> maps the document at — so the challenge cannot point elsewhere.
    /// </para>
    /// <para>
    /// An application that supplies its events through <c>JwtBearerOptions.EventsType</c> resolves them after this runs,
    /// so there is nothing to compose with; call <see cref="ProtectedResourceChallenge.AddOnStarting"/> from that events
    /// type's <c>Challenge</c> instead. That combination throws when the scheme is first used, rather than silently
    /// advertising nothing.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// services.AddAuthentication()
    ///     .AddJwtBearer(options =&gt; { ... })
    ///     .AddProtectedResourceMetadataChallenge();
    /// </code>
    /// </example>
    public static AuthenticationBuilder AddProtectedResourceMetadataChallenge(
        this AuthenticationBuilder builder,
        string authenticationScheme = JwtBearerDefaults.AuthenticationScheme,
        string? resourceName = null) {

        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNull(authenticationScheme);

        string name = resourceName ?? Options.Options.DefaultName;

        builder.Services.AddOptions<JwtBearerOptions>(authenticationScheme)
            .PostConfigure<IOptionsMonitor<OAuthProtectedResourceOptions>>((options, resources) => {
                if(options.EventsType is not null) {
                    throw new InvalidOperationException(
                        $"The '{authenticationScheme}' JwtBearer scheme resolves its events from EventsType, which cannot be composed " +
                        "with. Call ProtectedResourceChallenge.AddOnStarting(context.HttpContext, metadataUrl) from that type's " +
                        "Challenge method instead of AddProtectedResourceMetadataChallenge().");
                }

                options.Events ??= new JwtBearerEvents();
                Func<JwtBearerChallengeContext, Task> previous = options.Events.OnChallenge;

                options.Events.OnChallenge = async context => {
                    await previous(context).ConfigureAwait(false);

                    if(context.Handled) {
                        return;
                    }

                    string resource = resources.Get(name).Resource ?? throw new InvalidOperationException(
                        $"The protected resource '{name}' advertised by the '{authenticationScheme}' scheme has no Resource. " +
                        "Register it with services.AddOAuthProtectedResource(...).");

                    ProtectedResourceChallenge.AddOnStarting(context.HttpContext, ProtectedResourceMetadataUri.For(resource));
                };
            });

        return builder;
    }
}
