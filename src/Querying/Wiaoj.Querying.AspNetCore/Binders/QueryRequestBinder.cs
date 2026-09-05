using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Buffers;
using System.IO.Pipelines;
using Wiaoj.Preconditions;
using Wiaoj.Primitives.Buffers;
using Wiaoj.Querying.Parsers;

namespace Wiaoj.Querying.AspNetCore.Binders;

/// <summary>
/// High-performance parameter binder resolving <see cref="QueryRequest"/> instances from HTTP request contexts.
/// Supports standard GET query strings and RFC 10008 HTTP QUERY / POST request bodies via extensible payload parsers.
/// </summary>
internal static class QueryRequestBinder {
    private const string AcceptQueryHeader = "Accept-Query";
    private const string DefaultSupportedMediaTypes = "application/json, text/plain, application/x-www-form-urlencoded";

    private static readonly IQueryPayloadParser[] DefaultParsers = [
        new JsonQueryPayloadParser(),
        new BracketQueryPayloadParser()
    ];

    /// <summary>
    /// Asynchronously binds a <see cref="QueryRequest"/> from the incoming HTTP request.
    /// Reads from the request body for HTTP QUERY or POST methods using registered payload parsers,
    /// falling back to URL query parameters when appropriate.
    /// </summary>
    /// <param name="context">The current HTTP context.</param>
    /// <returns>A value task containing the parsed <see cref="QueryRequest"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="context"/> is <see langword="null"/>.</exception>
    /// <exception cref="BadHttpRequestException">
    /// Thrown with HTTP 415 when the content type is unsupported,
    /// HTTP 413 when the payload exceeds server limits,
    /// or HTTP 400 when payload syntax is malformed.
    /// </exception>
    public static async ValueTask<QueryRequest> BindAsync(HttpContext context) {
        Preca.ThrowIfNull(context);

        HttpRequest request = context.Request;

        // Register Accept-Query response header for QUERY and POST requests
        if(IsBodyQuerySupported(request.Method)) {
            context.Response.OnStarting(static state => {
                HttpContext httpContext = (HttpContext)state;
                httpContext.Response.Headers[AcceptQueryHeader] = DefaultSupportedMediaTypes;
                return Task.CompletedTask;
            }, context);
        }

        QueryOptions? options = context.RequestServices?.GetService<IOptions<QueryOptions>>()?.Value;
        bool allowBodyPayloads = options?.AllowBodyPayloads ?? true;

        if(allowBodyPayloads && IsBodyQuerySupported(request.Method) && request.Body != null) {
            string? contentType = request.ContentType;
            if(!string.IsNullOrWhiteSpace(contentType)) {
                QueryRequest? bodyRequest = await TryBindFromBodyAsync(context, contentType, options).ConfigureAwait(false);
                if(bodyRequest.HasValue) {
                    return bodyRequest.Value;
                }
            }
        }

        IQueryCollection query = request.Query;
        if(query.Count == 0) {
            return QueryRequest.Empty;
        }

        return BindFromQueryCollection(query, options);
    }

    private static bool IsBodyQuerySupported(string method) {
        return HttpMethods.IsPost(method) || HttpMethods.IsQuery(method);
    }

    private static async ValueTask<QueryRequest?> TryBindFromBodyAsync(HttpContext context, string contentType, QueryOptions? options) {
        HttpRequest request = context.Request;
        IQueryPayloadParser[] parsers = ResolveParsers(context.RequestServices);

        IQueryPayloadParser? selectedParser = null;
        for(int i = 0; i < parsers.Length; i++) {
            if(parsers[i].CanParse(contentType)) {
                selectedParser = parsers[i];
                break;
            }
        }

        if(selectedParser is null) {
            throw new BadHttpRequestException(
                $"The media type '{contentType}' is not supported for query payloads.",
                StatusCodes.Status415UnsupportedMediaType);
        }

        PipeReader reader = request.BodyReader;
        long? maxRequestBodySize = context.Features.Get<IHttpMaxRequestBodySizeFeature>()?.MaxRequestBodySize;

        // Three-way minimum: the parser's own default (e.g. 64 KB), an optional application-wide
        // ceiling from QueryOptions (tightens uniformly across all parsers, never loosens below a
        // parser's own limit), and the host's general-purpose limit (often tens of megabytes, sized
        // for uploads). Without this, an oversized query payload would stream all the way up to
        // whichever of those is largest before ever being rejected.
        long effectiveMaxPayloadBytes = selectedParser.MaxPayloadBytes;

        if(options?.MaxPayloadBytes is int optionsLimit) {
            effectiveMaxPayloadBytes = Math.Min(effectiveMaxPayloadBytes, optionsLimit);
        }

        if(maxRequestBodySize.HasValue) {
            effectiveMaxPayloadBytes = Math.Min(effectiveMaxPayloadBytes, maxRequestBodySize.Value);
        }

        while(true) {
            ReadResult readResult = await reader.ReadAsync().ConfigureAwait(false);
            ReadOnlySequence<byte> buffer = readResult.Buffer;

            if(buffer.Length > effectiveMaxPayloadBytes) {
                reader.AdvanceTo(buffer.End);
                throw new BadHttpRequestException(
                    "The query payload exceeds the configured maximum size for this content type.",
                    StatusCodes.Status413PayloadTooLarge);
            }

            if(readResult.IsCompleted) {
                if(buffer.IsEmpty) {
                    reader.AdvanceTo(buffer.End);
                    return null;
                }

                bool parsed;
                QueryRequest result;

                if(buffer.IsSingleSegment) {
                    parsed = selectedParser.TryParse(buffer.FirstSpan, out result);
                }
                else {
                    using ValueBuffer<byte> pooled = new((int)buffer.Length, stackalloc byte[512]);
                    buffer.CopyTo(pooled.Span);
                    parsed = selectedParser.TryParse(pooled.Span[..(int)buffer.Length], out result);
                }

                reader.AdvanceTo(buffer.End);

                if(!parsed) {
                    throw new BadHttpRequestException(
                        "Invalid query payload syntax.",
                        StatusCodes.Status400BadRequest);
                }

                return result;
            }

            reader.AdvanceTo(buffer.Start, buffer.End);
        }
    }

    private static IQueryPayloadParser[] ResolveParsers(IServiceProvider? serviceProvider) {
        if(serviceProvider is null) {
            return DefaultParsers;
        }

        IEnumerable<IQueryPayloadParser>? registered = serviceProvider.GetServices<IQueryPayloadParser>();
        if(registered is null) {
            return DefaultParsers;
        }

        IQueryPayloadParser[] array = registered as IQueryPayloadParser[] ?? [.. registered];
        return array.Length > 0 ? array : DefaultParsers;
    }

    private static QueryRequest BindFromQueryCollection(IQueryCollection query, QueryOptions? options) {
        Q q = default;
        Sort sort = default;
        List<FilterConditionNode>? filters = null;
        HashSet<string>? ignored = options?.IgnoredParameters;

        foreach((string? key, Microsoft.Extensions.Primitives.StringValues stringValues) in query) {
            if(string.IsNullOrWhiteSpace(key)) {
                continue;
            }

            string trimmedKey = key.Trim();

            if(trimmedKey.Equals(QuerySyntax.Parameters.Q, StringComparison.OrdinalIgnoreCase)) {
                q = new Q(stringValues.ToString());
                continue;
            }

            if(trimmedKey.Equals(QuerySyntax.Parameters.Sort, StringComparison.OrdinalIgnoreCase)) {
                if(Sort.TryParse(stringValues.ToString(), out Sort parsedSort)) {
                    sort = parsedSort;
                }
                continue;
            }

            if(ignored is { Count: > 0 } && IsIgnoredParameter(trimmedKey, ignored)) {
                continue;
            }

            if(stringValues.Count == 0) {
                if(BracketQueryParser.TryParse(trimmedKey, out FilterConditionNode unaryNode)) {
                    filters ??= [];
                    filters.Add(unaryNode);
                }
                continue;
            }

            for(int i = 0; i < stringValues.Count; i++) {
                string? val = stringValues[i];

                if(string.IsNullOrEmpty(val) && BracketQueryParser.TryParse(trimmedKey, out FilterConditionNode unaryNode)) {
                    filters ??= [];
                    filters.Add(unaryNode);
                    continue;
                }

                string rawPair = $"{trimmedKey}={val}";
                if(BracketQueryParser.TryParse(rawPair, out FilterConditionNode filterNode)) {
                    filters ??= [];
                    filters.Add(filterNode);
                }
            }
        }

        return new QueryRequest(q: q, sort: sort, filters: filters);
    }

    private static bool IsIgnoredParameter(string key, HashSet<string> ignoredParameters) {
        if(ignoredParameters.Contains(key)) {
            return true;
        }

        int bracketIndex = key.IndexOf(QuerySyntax.OpenBracket);
        if(bracketIndex > 0) {
            string baseKey = key[..bracketIndex].Trim();
            return ignoredParameters.Contains(baseKey);
        }

        return false;
    }
}