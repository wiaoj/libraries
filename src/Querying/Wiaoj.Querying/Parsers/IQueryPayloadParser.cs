namespace Wiaoj.Querying.Parsers;

/// <summary>
/// Defines a contract for parsing raw request body payloads into <see cref="QueryRequest"/> instances.
/// </summary>
public interface IQueryPayloadParser {
    /// <summary>
    /// Determines whether this parser can process the specified media type.
    /// </summary>
    /// <param name="mediaType">The media type string (e.g. "application/json", "text/plain").</param>
    /// <returns><see langword="true"/> if supported; otherwise, <see langword="false"/>.</returns>
    bool CanParse(string mediaType);

    /// <summary>
    /// Attempts to parse the raw UTF-8 byte payload into a <see cref="QueryRequest"/>.
    /// </summary>
    /// <param name="utf8Payload">The raw UTF-8 payload buffer.</param>
    /// <param name="result">When this method returns, contains the parsed instance if successful; otherwise, <see cref="QueryRequest.Empty"/>.</param>
    /// <returns><see langword="true"/> if parsing succeeded; otherwise, <see langword="false"/>.</returns>
    bool TryParse(ReadOnlySpan<byte> utf8Payload, out QueryRequest result);
}