using System.Text;
using Wiaoj.Preconditions;
using Wiaoj.Preconditions.Exceptions;

namespace Wiaoj.Pagination.AspNetCore.Linking;

/// <summary>
/// Provides standard IANA relation type constants defined in RFC 8288 (Web Linking).
/// </summary>
public static class Rfc8288Relations {
    /// <summary>Refers to the first page in a pagination sequence.</summary>
    public const string First = "first";

    /// <summary>Refers to the immediately preceding page in a pagination sequence.</summary>
    public const string Prev = "prev";

    /// <summary>Refers to the immediately succeeding page in a pagination sequence.</summary>
    public const string Next = "next";

    /// <summary>Refers to the final page in a pagination sequence.</summary>
    public const string Last = "last";
}

/// <summary>
/// Provides utility methods for formatting standard RFC 8288 (Web Linking) HTTP <c>Link</c> header strings.
/// </summary>
/// <remarks>
/// <para>
/// Formats links according to the IANA standard link relations (<c>first</c>, <c>prev</c>, <c>next</c>, <c>last</c>).
/// The resulting header string can be set directly onto <c>HttpResponse.Headers.Link</c>.
/// </para>
/// </remarks>
public static class Rfc8288LinkHeaderBuilder {
    private const char LinkPrefix = '<';
    private const string LinkDelimiter = ", <";
    private const string Delimiter = ", ";
    private const string RelFirstSuffix = ">; rel=\"" + Rfc8288Relations.First + "\"";
    private const string RelPrevSuffix = ">; rel=\"" + Rfc8288Relations.Prev + "\"";
    private const string RelNextSuffix = ">; rel=\"" + Rfc8288Relations.Next + "\"";
    private const string RelLastSuffix = ">; rel=\"" + Rfc8288Relations.Last + "\"";

    /// <summary>
    /// Builds an RFC 8288 compliant <c>Link</c> header string for offset-based pagination.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Evaluates <see cref="PageMetadata.HasPrevious"/> and <see cref="PageMetadata.HasNext"/> flags to dynamically include 
    /// only the applicable directional relation links. Always includes <c>rel="first"</c> and <c>rel="last"</c> when total pages exceed 1.
    /// </para>
    /// </remarks>
    /// <param name="metadata">The pagination metadata.</param>
    /// <param name="pageUriFactory">A factory delegate generating the full URI for a given page number.</param>
    /// <returns>A comma-separated RFC 8288 link header string, or <see cref="string.Empty"/> if metadata is empty.</returns>
    /// <exception cref="PrecaArgumentNullException">Thrown when <paramref name="pageUriFactory"/> is <see langword="null"/>.</exception>
    /// <example>
    /// Output format:
    /// <code>
    /// &lt;https://api/items?page=1&gt;; rel="first", &lt;https://api/items?page=3&gt;; rel="next", &lt;https://api/items?page=5&gt;; rel="last"
    /// </code>
    /// </example>
    public static string Build(PageMetadata metadata, Func<int, string> pageUriFactory) {
        Preca.ThrowIfNull(pageUriFactory);

        if(metadata.IsEmpty || metadata.TotalCount == 0) {
            return string.Empty;
        }

        StringBuilder sb = new(256);

        // 1. rel="first"
        sb.Append(LinkPrefix).Append(pageUriFactory(1)).Append(RelFirstSuffix);

        // 2. rel="prev"
        if(metadata.HasPrevious) {
            sb.Append(LinkDelimiter).Append(pageUriFactory(metadata.PageNumber - 1)).Append(RelPrevSuffix);
        }

        // 3. rel="next"
        if(metadata.HasNext) {
            sb.Append(LinkDelimiter).Append(pageUriFactory(metadata.PageNumber + 1)).Append(RelNextSuffix);
        }

        // 4. rel="last"
        if(metadata.TotalPages > 1) {
            sb.Append(LinkDelimiter).Append(pageUriFactory((int)metadata.TotalPages)).Append(RelLastSuffix);
        }

        return sb.ToString();
    }

    /// <summary>
    /// Builds an RFC 8288 compliant <c>Link</c> header string for keyset (cursor-based) pagination.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Evaluates <see cref="CursorMetadata.HasPrevious"/> (generating <c>rel="prev"</c> with <see cref="CursorMetadata.StartCursor"/>) 
    /// and <see cref="CursorMetadata.HasNext"/> (generating <c>rel="next"</c> with <see cref="CursorMetadata.EndCursor"/>).
    /// </para>
    /// </remarks>
    /// <param name="metadata">The keyset cursor metadata.</param>
    /// <param name="cursorUriFactory">A factory delegate generating the full URI for a given cursor token and direction.</param>
    /// <returns>A comma-separated RFC 8288 link header string, or <see cref="string.Empty"/> if metadata is empty.</returns>
    /// <exception cref="PrecaArgumentNullException">Thrown when <paramref name="cursorUriFactory"/> is <see langword="null"/>.</exception>
    /// <example>
    /// Output format:
    /// <code>
    /// &lt;https://api/items?cursor=start_01&amp;direction=Backward&gt;; rel="prev", &lt;https://api/items?cursor=end_10&amp;direction=Forward&gt;; rel="next"
    /// </code>
    /// </example>
    public static string Build(CursorMetadata metadata, Func<CursorToken, CursorDirection, string> cursorUriFactory) {
        Preca.ThrowIfNull(cursorUriFactory);

        if(metadata.IsEmpty) {
            return string.Empty;
        }

        StringBuilder sb = new(256);
        bool hasAppended = false;

        // 1. rel="prev" (Backward seeking using StartCursor)
        if(metadata.HasPrevious && !metadata.StartCursor.IsEmpty) {
            sb.Append(LinkPrefix).Append(cursorUriFactory(metadata.StartCursor, CursorDirection.Backward)).Append(RelPrevSuffix);
            hasAppended = true;
        }

        // 2. rel="next" (Forward seeking using EndCursor)
        if(metadata.HasNext && !metadata.EndCursor.IsEmpty) {
            if(hasAppended) {
                sb.Append(Delimiter);
            }
            sb.Append(LinkPrefix).Append(cursorUriFactory(metadata.EndCursor, CursorDirection.Forward)).Append(RelNextSuffix);
        }

        return sb.ToString();
    }
}