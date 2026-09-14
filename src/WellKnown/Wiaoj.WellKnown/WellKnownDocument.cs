using Microsoft.AspNetCore.Http;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Wiaoj.WellKnown;

/// <summary>
/// Writing a well-known JSON document: the standard members from a source-generated serializer, the additional ones
/// merged in, and the response sent with a length and a cache lifetime.
/// </summary>
internal static class WellKnownDocument {
    /// <summary>
    /// Copies additional parameters into a new object. A JSON node belongs to one parent, so the options' own nodes cannot
    /// be attached to a document; attaching them to the first request's document would fail every later request.
    /// </summary>
    public static JsonObject? CopyAdditionalParameters(Dictionary<string, JsonNode?> parameters) {
        if(parameters.Count == 0) {
            return null;
        }

        JsonObject copy = [];
        foreach(KeyValuePair<string, JsonNode?> parameter in parameters) {
            copy[parameter.Key] = parameter.Value?.DeepClone();
        }

        return copy;
    }

    /// <summary>
    /// Appends <paramref name="additional"/> to the serialized standard members and returns the UTF-8 JSON.
    /// </summary>
    /// <remarks>
    /// Not extension data, which source-generated serialization writes incorrectly for nested values. The nodes are cloned
    /// because a document may be written more than once.
    /// </remarks>
    public static byte[] ToUtf8Json(JsonNode standard, JsonObject? additional) {
        JsonObject document = standard.AsObject();

        if(additional is not null) {
            foreach(KeyValuePair<string, JsonNode?> parameter in additional) {
                document[parameter.Key] = parameter.Value?.DeepClone();
            }
        }

        return JsonSerializer.SerializeToUtf8Bytes(document, WellKnownJsonContext.Default.JsonObject);
    }

    /// <summary>Writes <paramref name="body"/> as a 200 <c>application/json</c> response cached for <paramref name="cacheDuration"/>.</summary>
    public static Task WriteAsync(HttpContext context, byte[] body, TimeSpan cacheDuration) {
        context.Response.StatusCode = StatusCodes.Status200OK;
        context.Response.ContentType = "application/json";
        context.Response.Headers.CacheControl = cacheDuration > TimeSpan.Zero
            ? $"public, max-age={((long)cacheDuration.TotalSeconds).ToString(CultureInfo.InvariantCulture)}"
            : "no-cache";

        // Buffered: a metadata document is a few hundred bytes, and the length can then be sent.
        context.Response.ContentLength = body.Length;
        return context.Response.Body.WriteAsync(body, context.RequestAborted).AsTask();
    }
}
