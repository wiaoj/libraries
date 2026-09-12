using System.Net.Mime;

namespace Wiaoj.Querying.Parsers;

/// <summary>
/// Payload parser implementation for handling JSON request bodies (<c>application/json</c>).
/// </summary>
public sealed class JsonQueryPayloadParser : IQueryPayloadParser {
    private const string JsonMediaType = MediaTypeNames.Application.Json;

    /// <inheritdoc/>
    public bool CanParse(string mediaType) {
        if(string.IsNullOrWhiteSpace(mediaType)) {
            return false;
        }

        ReadOnlySpan<char> span = mediaType.AsSpan().Trim();
        int semicolonIndex = span.IndexOf(';');
        ReadOnlySpan<char> baseType = (semicolonIndex >= 0 ? span[..semicolonIndex] : span).Trim();

        return baseType.Equals(JsonMediaType, StringComparison.OrdinalIgnoreCase);
    }

    /// <inheritdoc/>
    public bool TryParse(ReadOnlySpan<byte> utf8Payload, out QueryRequest result) {
        return JsonQueryParser.TryParse(utf8Payload, out result);
    }
}