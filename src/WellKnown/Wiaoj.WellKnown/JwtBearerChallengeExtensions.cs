using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;

namespace Wiaoj.WellKnown;

public static class JwtBearerChallengeExtensions {
    /// <summary>
    /// 401 Unauthorized durumunda istemciye resource_metadata discovery linkini döner (RFC 9728 Sec 5.1).
    /// </summary>
    public static void AttachProtectedResourceMetadata(this JwtBearerChallengeContext context, string metadataUrl) {
        context.HandleResponse();
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.Headers.Append("WWW-Authenticate", $"Bearer resource_metadata=\"{metadataUrl}\"");
    }
}