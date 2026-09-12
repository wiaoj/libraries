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

    /// <summary>
    /// Gets the maximum payload size, in bytes, this parser is willing to accept. The binder uses this
    /// to stop streaming the request body early — before the host's much larger general-purpose request
    /// body limit (commonly tens of megabytes, meant for uploads) would otherwise let an oversized query
    /// payload accumulate in memory. Defaults to 64 KB; override to raise or lower it per parser.
    /// </summary>
    int MaxPayloadBytes => 64 * 1024;
}