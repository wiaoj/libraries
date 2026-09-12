using System.Net.Mime;

namespace Wiaoj.Querying.Parsers;

/// <summary>
/// Payload parser implementation for handling plain text and URL-encoded form request bodies
/// (<c>text/plain</c> and <c>application/x-www-form-urlencoded</c>).
/// </summary>
public sealed class BracketQueryPayloadParser : IQueryPayloadParser {
    private const string TextPlainMediaType = MediaTypeNames.Text.Plain;
    private const string FormUrlEncodedMediaType = MediaTypeNames.Application.FormUrlEncoded;

    /// <inheritdoc/>
    public bool CanParse(string mediaType) {
        if(string.IsNullOrWhiteSpace(mediaType)) {
            return false;
        }

        ReadOnlySpan<char> span = mediaType.AsSpan().Trim();
        int semicolonIndex = span.IndexOf(';');
        ReadOnlySpan<char> baseType = (semicolonIndex >= 0 ? span[..semicolonIndex] : span).Trim();

        return baseType.Equals(TextPlainMediaType, StringComparison.OrdinalIgnoreCase) ||
               baseType.Equals(FormUrlEncodedMediaType, StringComparison.OrdinalIgnoreCase);
    }

    /// <inheritdoc/>
    public bool TryParse(ReadOnlySpan<byte> utf8Payload, out QueryRequest result) {
        if(utf8Payload.IsEmpty) {
            result = QueryRequest.Empty;
            return false;
        }

        if(QueryRequest.TryParse(utf8Payload, out result)) {
            return !result.IsEmpty;
        }

        result = QueryRequest.Empty;
        return false;
    }
}