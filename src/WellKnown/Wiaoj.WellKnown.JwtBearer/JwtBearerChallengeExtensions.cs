using Microsoft.AspNetCore.Authentication.JwtBearer;
using Wiaoj.Preconditions;

namespace Wiaoj.WellKnown;

/// <summary>
/// The original challenge helper, kept so existing callers compile.
/// </summary>
public static class JwtBearerChallengeExtensions {
    /// <summary>
    /// Adds <c>resource_metadata</c> to the challenge JwtBearer writes for this request.
    /// </summary>
    /// <param name="context">The challenge context.</param>
    /// <param name="metadataUrl">The absolute URL of the metadata document.</param>
    /// <remarks>
    /// This used to call <c>HandleResponse()</c> and write the header itself, which dropped <c>error</c>,
    /// <c>error_description</c> and <c>scope</c>. It now adds the parameter to JwtBearer's own challenge instead, so
    /// calling it no longer changes anything else about the response.
    /// </remarks>
    [Obsolete("Register AddProtectedResourceMetadataChallenge() on the authentication builder instead; it derives the URL from the registered resource.")]
    public static void AttachProtectedResourceMetadata(this JwtBearerChallengeContext context, string metadataUrl) {
        Preca.ThrowIfNull(context);
        Preca.ThrowIfNull(metadataUrl);

        ProtectedResourceChallenge.AddOnStarting(context.HttpContext, new Uri(metadataUrl, UriKind.Absolute));
    }
}
