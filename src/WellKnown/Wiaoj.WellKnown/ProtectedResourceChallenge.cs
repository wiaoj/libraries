using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Wiaoj.Preconditions;

namespace Wiaoj.WellKnown;

/// <summary>
/// Adds the RFC 9728 <c>resource_metadata</c> parameter to a response's <c>WWW-Authenticate</c> challenges.
/// </summary>
/// <remarks>
/// <para>
/// A client that receives a 401 has no other way to discover where to obtain a token for this resource. §5.1 defines
/// <c>resource_metadata</c> as a parameter <b>on</b> the challenge, beside the ones the authentication scheme already
/// wrote — <c>error="invalid_token"</c>, <c>error_description</c>, <c>scope</c> — which tell an expired token from a
/// missing one. Replacing the header to add it loses them.
/// </para>
/// <para>
/// So the parameter is added as the response starts, after the authentication handler has written its challenge, and
/// only to challenges of the given scheme that do not already carry one.
/// </para>
/// </remarks>
public static class ProtectedResourceChallenge {
    /// <summary>The <c>WWW-Authenticate</c> parameter name defined by RFC 9728 §5.1.</summary>
    public const string ParameterName = "resource_metadata";

    /// <summary>
    /// Adds <c>resource_metadata="<paramref name="metadataUrl"/>"</c> to each <paramref name="scheme"/> challenge when the
    /// response starts.
    /// </summary>
    /// <param name="context">The request being challenged.</param>
    /// <param name="metadataUrl">The absolute URL of the protected resource's metadata document.</param>
    /// <param name="scheme">The authentication scheme whose challenges receive the parameter; <c>Bearer</c> by default.</param>
    public static void AddOnStarting(HttpContext context, Uri metadataUrl, string scheme = "Bearer") {
        Preca.ThrowIfNull(context);
        Preca.ThrowIfNull(metadataUrl);
        Preca.ThrowIfNull(scheme);

        HttpResponse response = context.Response;
        response.OnStarting(() => {
            if(response.StatusCode == StatusCodes.Status401Unauthorized) {
                response.Headers.WWWAuthenticate = AddParameter(response.Headers.WWWAuthenticate, metadataUrl, scheme);
            }

            return Task.CompletedTask;
        });
    }

    /// <summary>
    /// Returns <paramref name="challenges"/> with <c>resource_metadata</c> added to each <paramref name="scheme"/>
    /// challenge that lacks it; other challenges are returned unchanged.
    /// </summary>
    /// <param name="challenges">The <c>WWW-Authenticate</c> values.</param>
    /// <param name="metadataUrl">The absolute URL of the metadata document.</param>
    /// <param name="scheme">The authentication scheme to add the parameter to.</param>
    /// <returns>The challenges, with the parameter added. A header without any such challenge gains a bare one.</returns>
    public static StringValues AddParameter(StringValues challenges, Uri metadataUrl, string scheme = "Bearer") {
        Preca.ThrowIfNull(metadataUrl);
        Preca.ThrowIfNull(scheme);

        // AbsoluteUri percent-encodes '"' and normalises '\', so the value cannot break out of its quoted-string.
        string parameter = $"{ParameterName}=\"{metadataUrl.AbsoluteUri}\"";
        string[] values = [.. challenges.Select(value => value ?? string.Empty)];
        bool found = false;

        for(int i = 0; i < values.Length; i++) {
            string value = values[i];
            if(!IsScheme(value, scheme)) {
                continue;
            }

            found = true;
            if(value.Contains(ParameterName + "=", StringComparison.OrdinalIgnoreCase)) {
                continue;
            }

            string trimmed = value.TrimEnd();
            values[i] = trimmed.Length == scheme.Length
                ? $"{trimmed} {parameter}"
                : $"{trimmed}, {parameter}";
        }

        return found ? new StringValues(values) : StringValues.Concat(challenges, $"{scheme} {parameter}");
    }

    private static bool IsScheme(string challenge, string scheme) {
        string trimmed = challenge.TrimStart();
        return trimmed.StartsWith(scheme, StringComparison.OrdinalIgnoreCase)
            && (trimmed.Length == scheme.Length || trimmed[scheme.Length] == ' ');
    }
}
