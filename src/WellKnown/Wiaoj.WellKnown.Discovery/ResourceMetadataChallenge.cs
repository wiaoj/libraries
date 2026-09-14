using System.Text;

namespace Wiaoj.WellKnown.Discovery;

/// <summary>
/// Finds the RFC 9728 <c>resource_metadata</c> parameter in <c>WWW-Authenticate</c> challenges.
/// </summary>
/// <remarks>
/// §5.1 allows the parameter on any scheme — <c>Bearer</c>, <c>DPoP</c> — and beside any other parameter, so the header is
/// read as RFC 9110 §11.6.1 challenges: parameter names are case-insensitive, values are tokens or quoted strings, and a
/// comma inside a quoted string does not end the parameter.
/// </remarks>
public static class ResourceMetadataChallenge {
    /// <summary>The parameter name defined by RFC 9728 §5.1.</summary>
    public const string ParameterName = "resource_metadata";

    /// <summary>
    /// Returns the <c>resource_metadata</c> URL advertised by <paramref name="response"/>, or null when it advertises none.
    /// </summary>
    /// <param name="response">A response, normally a 401.</param>
    /// <returns>The advertised URL as written, or null.</returns>
    /// <exception cref="OAuthDiscoveryException">
    /// The challenges advertise more than one different URL (<see cref="OAuthDiscoveryFailure.AmbiguousChallenge"/>).
    /// </exception>
    public static string? Find(HttpResponseMessage response) {
        ArgumentNullException.ThrowIfNull(response);

        return response.Headers.TryGetValues("WWW-Authenticate", out IEnumerable<string>? values)
            ? Find(values)
            : null;
    }

    /// <summary>
    /// Returns the <c>resource_metadata</c> URL in <paramref name="headerValues"/>, or null when there is none.
    /// </summary>
    /// <param name="headerValues">The <c>WWW-Authenticate</c> field values.</param>
    /// <returns>The advertised URL as written, or null.</returns>
    /// <exception cref="OAuthDiscoveryException">More than one different URL is advertised.</exception>
    public static string? Find(IEnumerable<string> headerValues) {
        ArgumentNullException.ThrowIfNull(headerValues);

        string? found = null;
        foreach(string header in headerValues) {
            foreach((string name, string value) in Parameters(header)) {
                if(!name.Equals(ParameterName, StringComparison.OrdinalIgnoreCase)) {
                    continue;
                }

                if(found is not null && !found.Equals(value, StringComparison.Ordinal)) {
                    throw new OAuthDiscoveryException(
                        OAuthDiscoveryFailure.AmbiguousChallenge,
                        $"The response advertises two different resource_metadata URLs, '{found}' and '{value}'; neither can be trusted to be the resource's.");
                }

                found = value;
            }
        }

        return found;
    }

    /// <summary>Yields every <c>name=value</c> pair in a field value; scheme names and token68 credentials are skipped.</summary>
    private static IEnumerable<(string Name, string Value)> Parameters(string header) {
        int i = 0;
        while(i < header.Length) {
            while(i < header.Length && (header[i] is ' ' or '\t' or ',')) {
                i++;
            }

            int start = i;
            while(i < header.Length && IsTokenChar(header[i])) {
                i++;
            }

            if(i == start) {
                i++; // Not a token — skip the character rather than loop on it.
                continue;
            }

            string token = header[start..i];
            int afterToken = i;
            while(i < header.Length && (header[i] is ' ' or '\t')) {
                i++;
            }

            if(i >= header.Length || header[i] != '=') {
                // A scheme name, followed by whitespace and its parameters; parsing resumes after it.
                i = afterToken;
                continue;
            }

            i++;
            while(i < header.Length && (header[i] is ' ' or '\t')) {
                i++;
            }

            if(i < header.Length && header[i] == '"') {
                (string value, int end) = QuotedString(header, i);
                i = end;
                yield return (token, value);
            }
            else {
                int valueStart = i;
                while(i < header.Length && header[i] is not (',' or ' ' or '\t')) {
                    i++;
                }

                yield return (token, header[valueStart..i]);
            }
        }
    }

    /// <summary>Reads a quoted-string starting at the opening quote; returns its unescaped content and the index after the closing quote.</summary>
    private static (string Value, int End) QuotedString(string header, int openingQuote) {
        StringBuilder value = new();
        int i = openingQuote + 1;

        while(i < header.Length) {
            char c = header[i];
            if(c == '\\' && i + 1 < header.Length) {
                value.Append(header[i + 1]);
                i += 2;
            }
            else if(c == '"') {
                return (value.ToString(), i + 1);
            }
            else {
                value.Append(c);
                i++;
            }
        }

        return (value.ToString(), i);
    }

    /// <summary>RFC 9110 §5.6.2 <c>tchar</c>.</summary>
    private static bool IsTokenChar(char c) {
        return char.IsAsciiLetterOrDigit(c) || c is '!' or '#' or '$' or '%' or '&' or '\'' or '*' or '+' or '-' or '.' or '^' or '_' or '`' or '|' or '~';
    }
}
