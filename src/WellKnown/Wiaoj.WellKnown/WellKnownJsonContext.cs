using System.Text.Json.Serialization;

namespace Wiaoj.WellKnown;

/// <summary>
/// Source-generated serialization for the documents this package serves, so they work under Native AOT and trimming.
/// </summary>
/// <remarks>
/// Deliberately not the application's JSON options: a well-known document's member names and omission rules are fixed
/// by its RFC, and an application naming policy or a different null handling would publish a document clients reject.
/// </remarks>
[JsonSourceGenerationOptions(DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(OAuthProtectedResourceMetadata))]
[JsonSerializable(typeof(OAuthAuthorizationServerMetadata))]
[JsonSerializable(typeof(System.Text.Json.Nodes.JsonObject))]
internal sealed partial class WellKnownJsonContext : JsonSerializerContext;
