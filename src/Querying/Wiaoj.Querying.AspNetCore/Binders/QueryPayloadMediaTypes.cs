using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using System.Text;
using Wiaoj.Querying.Parsers;

namespace Wiaoj.Querying.AspNetCore.Binders;

/// <summary>
/// The media types query payloads may use, taken from the registered <see cref="IQueryPayloadParser"/>s — the single
/// source for the <c>Accept-Query</c> and <c>Accept</c> response fields and for generated documents.
/// </summary>
public static class QueryPayloadMediaTypes {
    /// <summary>The response field that advertises QUERY support and its media types (RFC 10008 §3).</summary>
    public const string AcceptQueryHeaderName = "Accept-Query";

    private static readonly IQueryPayloadParser[] DefaultParsers = [new JsonQueryPayloadParser(), new BracketQueryPayloadParser()];

    /// <summary>
    /// Returns the parsers the binder uses: those registered in <paramref name="services"/>, or the built-in JSON and
    /// bracket parsers when none are.
    /// </summary>
    /// <param name="services">The request or application services; may be null.</param>
    /// <returns>The parsers, in registration order.</returns>
    public static IReadOnlyList<IQueryPayloadParser> ResolveParsers(IServiceProvider? services) {
        IQueryPayloadParser[] registered = services?.GetServices<IQueryPayloadParser>() is { } found ? [.. found] : [];
        return registered.Length > 0 ? registered : DefaultParsers;
    }

    /// <summary>
    /// Returns the distinct media types the parsers declare, without parameters, in registration order.
    /// </summary>
    /// <param name="services">The request or application services; may be null.</param>
    /// <returns>The media types.</returns>
    public static IReadOnlyList<string> Resolve(IServiceProvider? services) {
        List<string> mediaTypes = [];
        foreach(IQueryPayloadParser parser in ResolveParsers(services)) {
            foreach(string mediaType in parser.SupportedMediaTypes) {
                string baseType = mediaType.Split(';', 2)[0].Trim();
                if(baseType.Length > 0 && !mediaTypes.Contains(baseType, StringComparer.OrdinalIgnoreCase)) {
                    mediaTypes.Add(baseType);
                }
            }
        }

        return mediaTypes;
    }

    /// <summary>
    /// Formats media types as an RFC 8941 List of Tokens or Strings, as RFC 10008 §3 requires for <c>Accept-Query</c>:
    /// a media type is written as a Token when it is one, and as a String otherwise (a leading digit, for example).
    /// </summary>
    /// <param name="mediaTypes">The media types, without parameters.</param>
    /// <returns>The field value.</returns>
    public static string FormatStructuredList(IEnumerable<string> mediaTypes) {
        StringBuilder value = new();
        foreach(string mediaType in mediaTypes) {
            if(value.Length > 0) {
                value.Append(", ");
            }

            if(IsToken(mediaType)) {
                value.Append(mediaType);
            }
            else {
                value.Append('"').Append(mediaType.Replace("\\", "\\\\").Replace("\"", "\\\"")).Append('"');
            }
        }

        return value.ToString();
    }

    /// <summary>Whether the endpoint handling <paramref name="context"/> accepts the <c>QUERY</c> method.</summary>
    internal static bool EndpointAcceptsQuery(HttpContext context) {
        return context.GetEndpoint()?.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods
            .Contains(HttpMethods.Query, StringComparer.OrdinalIgnoreCase) == true;
    }

    /// <summary>RFC 8941 §3.3.4: <c>sf-token = ( ALPHA / "*" ) *( tchar / ":" / "/" )</c>.</summary>
    private static bool IsToken(string value) {
        if(value.Length == 0 || !(char.IsAsciiLetter(value[0]) || value[0] == '*')) {
            return false;
        }

        foreach(char c in value) {
            bool tchar = char.IsAsciiLetterOrDigit(c) || c is '!' or '#' or '$' or '%' or '&' or '\'' or '*' or '+' or '-' or '.' or '^' or '_' or '`' or '|' or '~';
            if(!tchar && c is not (':' or '/')) {
                return false;
            }
        }

        return true;
    }
}
