using Microsoft.AspNetCore.Http;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Wiaoj.Webhooks.AspNetCore;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// High-performance, zero-allocation discriminator extractor scanning target root JSON properties
/// directly from UTF-8 payload bytes using <see cref="Utf8JsonReader"/>.
/// </summary>
public sealed class JsonPropertyEventDiscriminatorExtractor : IWebhookEventDiscriminatorExtractor {
    private readonly byte[] _propertyNameUtf8;

    /// <summary>
    /// Gets the target JSON property name.
    /// </summary>
    public string PropertyName { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonPropertyEventDiscriminatorExtractor"/> class.
    /// </summary>
    /// <param name="propertyName">The JSON property name to extract (e.g. <c>"type"</c>, <c>"event"</c>).</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="propertyName"/> is <see langword="null"/>, empty, or whitespace.</exception>
    public JsonPropertyEventDiscriminatorExtractor(string propertyName) {
        Preca.ThrowIfNullOrWhiteSpace(propertyName);
        this.PropertyName = propertyName;
        this._propertyNameUtf8 = propertyName.ToUtf8Bytes();
    }

    /// <inheritdoc/>
    public bool TryExtractEventName(HttpContext context, ReadOnlySpan<byte> rawBody, [NotNullWhen(true)] out string? eventName) {
        Preca.ThrowIfNull(context);

        if(rawBody.IsEmpty) {
            eventName = null;
            return false;
        }

        try {
            Utf8JsonReader reader = new(rawBody, isFinalBlock: true, state: default);

            if(!reader.Read() || reader.TokenType != JsonTokenType.StartObject) {
                eventName = null;
                return false;
            }

            int depth = 1;
            while(reader.Read()) {
                if(reader.TokenType is JsonTokenType.StartObject or JsonTokenType.StartArray) {
                    depth++;
                }
                else if(reader.TokenType is JsonTokenType.EndObject or JsonTokenType.EndArray) {
                    depth--;
                    if(depth == 0) {
                        break;
                    }
                }
                else if(depth == 1 && reader.TokenType == JsonTokenType.PropertyName) {
                    if(reader.ValueTextEquals(this._propertyNameUtf8)) {
                        if(reader.Read() && reader.TokenType == JsonTokenType.String) {
                            eventName = reader.GetString();
                            return !string.IsNullOrWhiteSpace(eventName);
                        }

                        eventName = null;
                        return false;
                    }

                    reader.Skip();
                }
            }
        }
        catch(JsonException) {
            // Malformed JSON payload
        }

        eventName = null;
        return false;
    }
}