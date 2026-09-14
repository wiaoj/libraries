using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Wiaoj.Preconditions;

namespace Wiaoj.WellKnown.Discovery;

/// <summary>
/// Discovers, from a protected API itself, which authorization server issues its tokens — RFC 9728 protected resource
/// metadata, then RFC 8414 authorization server metadata.
/// </summary>
/// <remarks>
/// <para>
/// Every document is checked before it is returned: a protected resource's <c>resource</c> against the identifier it
/// was looked up for (RFC 9728 §3.3), an authorization server's <c>issuer</c> against the issuer (RFC 8414 §3.3). A
/// document that fails is never returned, whether it came from the network or the cache.
/// </para>
/// <para>
/// Documents are fetched only over https (http on loopback when <see cref="OAuthDiscoveryOptions.AllowHttpOnLoopback"/>),
/// never through a redirect to another origin, and cached for the lifetime their <c>Cache-Control</c> allows.
/// </para>
/// <para>
/// Obtaining a token is not part of this client: <see cref="OAuthDiscoveryResult"/> hands over the endpoints, and the
/// application's OAuth client uses them.
/// </para>
/// </remarks>
public sealed class OAuthDiscoveryClient {
    private readonly HttpClient _http;
    private readonly DiscoveryDocumentCache _cache;
    private readonly OAuthDiscoveryOptions _options;

    /// <summary>
    /// Creates a client with its own cache — for use without dependency injection. Keep the instance: a new one starts
    /// with an empty cache.
    /// </summary>
    /// <param name="httpClient">
    /// The HttpClient to fetch with. Give it a handler that does not follow redirects itself
    /// (<c>AllowAutoRedirect = false</c>): the client follows same-origin redirects on its own, and refuses a response a
    /// handler reached through another origin.
    /// </param>
    /// <param name="options">The options; defaults when null.</param>
    /// <param name="timeProvider">The clock cache lifetimes are measured with; the system clock when null.</param>
    public OAuthDiscoveryClient(HttpClient httpClient, OAuthDiscoveryOptions? options = null, TimeProvider? timeProvider = null)
        : this(httpClient, new DiscoveryDocumentCache(options ?? new OAuthDiscoveryOptions(), timeProvider ?? TimeProvider.System)) {
    }

    internal OAuthDiscoveryClient(HttpClient httpClient, DiscoveryDocumentCache cache) {
        Preca.ThrowIfNull(httpClient);
        Preca.ThrowIfNull(cache);

        cache.Options.Validate();
        this._http = httpClient;
        this._cache = cache;
        this._options = cache.Options;
    }

    /// <summary>
    /// Fetches the metadata of the protected resource <paramref name="resource"/>, from the URL derived from it (RFC 9728 §3.1).
    /// </summary>
    /// <param name="resource">The resource identifier, such as <c>https://api.example.com/v1</c>.</param>
    /// <param name="cancellationToken">Cancels waiting for the document.</param>
    /// <returns>The document, whose <c>resource</c> is identical to <paramref name="resource"/>.</returns>
    /// <exception cref="OAuthDiscoveryException">The document could not be fetched, or must not be used.</exception>
    public async Task<ProtectedResourceMetadataDocument> GetProtectedResourceMetadataAsync(string resource, CancellationToken cancellationToken = default) {
        Preca.ThrowIfNull(resource);

        Uri url = DeriveUrl(resource, ProtectedResourcePath, trimTerminatingSlash: false, "resource identifier");
        ProtectedResourceMetadataDocument document = await this.GetAsync(url, ProtectedResourceMetadataDocument.Parse, refreshIfOlderThan: null, cancellationToken).ConfigureAwait(false);

        if(!document.Resource.Equals(resource, StringComparison.Ordinal)) {
            throw Mismatch($"The protected resource metadata at '{url}' is for '{document.Resource}', not '{resource}'. RFC 9728 §3.3 forbids using it.");
        }

        return document;
    }

    /// <summary>
    /// Fetches the protected resource metadata a challenge advertises through <c>resource_metadata</c> (RFC 9728 §5.1).
    /// </summary>
    /// <param name="challengedResponse">The response to a request for the resource, normally a 401. Its <see cref="HttpResponseMessage.RequestMessage"/> must be set, as HttpClient sets it.</param>
    /// <param name="cancellationToken">Cancels waiting for the document.</param>
    /// <returns>The document, or null when the response advertises no metadata.</returns>
    /// <exception cref="OAuthDiscoveryException">The document could not be fetched, or does not describe the requested URL.</exception>
    /// <remarks>
    /// A challenge signals that the metadata may have changed (§5.2), so a cached document older than
    /// <see cref="OAuthDiscoveryOptions.ChallengeRefreshInterval"/> is fetched again. The document's <c>resource</c> must
    /// be identical to the challenged request's URL (§3.3), so a 401 from <c>…/v1/keys</c> cannot use metadata for
    /// <c>…/v1</c>; a client that knows the API's identifier uses <see cref="DiscoverAsync(string, CancellationToken)"/>.
    /// </remarks>
    public async Task<ProtectedResourceMetadataDocument?> GetProtectedResourceMetadataAsync(HttpResponseMessage challengedResponse, CancellationToken cancellationToken = default) {
        Preca.ThrowIfNull(challengedResponse);

        Uri requestUrl = challengedResponse.RequestMessage?.RequestUri is { IsAbsoluteUri: true } requested
            ? requested
            : throw new ArgumentException("The response has no absolute request URL to match the resource against.", nameof(challengedResponse));

        if(ResourceMetadataChallenge.Find(challengedResponse) is not { } advertised) {
            return null;
        }

        if(!Uri.TryCreate(advertised, UriKind.Absolute, out Uri? url)) {
            throw new OAuthDiscoveryException(OAuthDiscoveryFailure.InvalidUrl, $"The resource_metadata URL '{advertised}' is not an absolute URL.");
        }

        ProtectedResourceMetadataDocument document = await this.GetAsync(
            url, ProtectedResourceMetadataDocument.Parse, this._options.ChallengeRefreshInterval, cancellationToken).ConfigureAwait(false);

        if(!MatchesRequest(document.Resource, requestUrl)) {
            throw Mismatch(
                $"The protected resource metadata at '{url}', advertised by a response from '{requestUrl}', is for '{document.Resource}'. " +
                "RFC 9728 §3.3 requires it to be identical to the requested URL. To discover an API by its identifier instead, use DiscoverAsync(resource).");
        }

        return document;
    }

    /// <summary>
    /// Fetches the metadata of the authorization server <paramref name="issuer"/>, from the URL derived from it (RFC 8414 §3.1).
    /// </summary>
    /// <param name="issuer">The issuer identifier, such as <c>https://auth.example.com</c>.</param>
    /// <param name="cancellationToken">Cancels waiting for the document.</param>
    /// <returns>The document, whose <c>issuer</c> is identical to <paramref name="issuer"/>.</returns>
    /// <exception cref="OAuthDiscoveryException">The document could not be fetched, or must not be used.</exception>
    public async Task<AuthorizationServerMetadataDocument> GetAuthorizationServerMetadataAsync(string issuer, CancellationToken cancellationToken = default) {
        Preca.ThrowIfNull(issuer);

        Uri url = DeriveUrl(issuer, AuthorizationServerPath, trimTerminatingSlash: true, "issuer identifier");
        AuthorizationServerMetadataDocument document = await this.GetAsync(url, AuthorizationServerMetadataDocument.Parse, refreshIfOlderThan: null, cancellationToken).ConfigureAwait(false);

        if(!document.Issuer.Equals(issuer, StringComparison.Ordinal)) {
            throw Mismatch($"The authorization server metadata at '{url}' is for issuer '{document.Issuer}', not '{issuer}'. RFC 8414 §3.3 forbids using it.");
        }

        return document;
    }

    /// <summary>
    /// Discovers the protected resource <paramref name="resource"/> and the authorization server to use for it.
    /// </summary>
    /// <param name="resource">The resource identifier.</param>
    /// <param name="cancellationToken">Cancels waiting for the documents.</param>
    /// <returns>Both validated documents.</returns>
    /// <exception cref="OAuthDiscoveryException">A document could not be fetched or used, or no usable authorization server is listed.</exception>
    public async Task<OAuthDiscoveryResult> DiscoverAsync(string resource, CancellationToken cancellationToken = default) {
        ProtectedResourceMetadataDocument protectedResource = await this.GetProtectedResourceMetadataAsync(resource, cancellationToken).ConfigureAwait(false);
        return await this.WithAuthorizationServerAsync(protectedResource, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Discovers, from a challenged response, the protected resource and the authorization server to use for it — the flow
    /// of RFC 9728 §5.
    /// </summary>
    /// <param name="challengedResponse">The response to a request for the resource, normally a 401.</param>
    /// <param name="cancellationToken">Cancels waiting for the documents.</param>
    /// <returns>Both validated documents, or null when the response advertises no metadata.</returns>
    /// <exception cref="OAuthDiscoveryException">A document could not be fetched or used, or no usable authorization server is listed.</exception>
    public async Task<OAuthDiscoveryResult?> DiscoverAsync(HttpResponseMessage challengedResponse, CancellationToken cancellationToken = default) {
        ProtectedResourceMetadataDocument? protectedResource = await this.GetProtectedResourceMetadataAsync(challengedResponse, cancellationToken).ConfigureAwait(false);

        return protectedResource is null
            ? null
            : await this.WithAuthorizationServerAsync(protectedResource, cancellationToken).ConfigureAwait(false);
    }

    private const string ProtectedResourcePath = "/.well-known/oauth-protected-resource";
    private const string AuthorizationServerPath = "/.well-known/oauth-authorization-server";

    private async Task<OAuthDiscoveryResult> WithAuthorizationServerAsync(ProtectedResourceMetadataDocument protectedResource, CancellationToken cancellationToken) {
        if(protectedResource.AuthorizationServers.Count == 0) {
            throw new OAuthDiscoveryException(
                OAuthDiscoveryFailure.NoAuthorizationServer,
                $"The protected resource '{protectedResource.Resource}' lists no authorization_servers.");
        }

        string issuer;
        if(this._options.TrustedAuthorizationServers.Count == 0) {
            issuer = protectedResource.AuthorizationServers[0];
        }
        else {
            issuer = protectedResource.AuthorizationServers.FirstOrDefault(server => this._options.TrustedAuthorizationServers.Contains(server, StringComparer.Ordinal))
                ?? throw new OAuthDiscoveryException(
                    OAuthDiscoveryFailure.UntrustedAuthorizationServer,
                    $"The protected resource '{protectedResource.Resource}' lists authorization servers " +
                    $"{string.Join(", ", protectedResource.AuthorizationServers.Select(s => $"'{s}'"))}, none of which is trusted.");
        }

        AuthorizationServerMetadataDocument authorizationServer = await this.GetAuthorizationServerMetadataAsync(issuer, cancellationToken).ConfigureAwait(false);
        return new OAuthDiscoveryResult(protectedResource, authorizationServer);
    }

    /// <summary>RFC 9728 §3.3: identical to the URL the client requested, as sent or as normalised by <see cref="Uri"/>.</summary>
    private static bool MatchesRequest(string resource, Uri requestUrl) {
        return resource.Equals(requestUrl.AbsoluteUri, StringComparison.Ordinal)
            || resource.Equals(requestUrl.OriginalString, StringComparison.Ordinal);
    }

    private async Task<T> GetAsync<T>(Uri url, Func<Uri, JsonElement, T> parse, TimeSpan? refreshIfOlderThan, CancellationToken cancellationToken) where T : class {
        this.RequireSecure(url);

        return await this._cache.GetAsync(
            url,
            async ct => {
                (JsonElement json, TimeSpan freshness) = await this.FetchAsync(url, ct).ConfigureAwait(false);
                return (parse(url, json), freshness);
            },
            refreshIfOlderThan,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<(JsonElement Json, TimeSpan Freshness)> FetchAsync(Uri url, CancellationToken cancellationToken) {
        Uri current = url;

        for(int redirects = 0; ; redirects++) {
            using HttpRequestMessage request = new(HttpMethod.Get, current);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            HttpResponseMessage response;
            try {
                response = await this._http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            }
            catch(HttpRequestException exception) {
                throw new OAuthDiscoveryException(OAuthDiscoveryFailure.HttpError, $"The metadata document at '{current}' could not be fetched: {exception.Message}", exception);
            }

            using(response) {
                // A handler that follows redirects itself hides them; the request it ended on tells.
                if(response.RequestMessage?.RequestUri is { } answered && !SameOrigin(answered, url)) {
                    throw CrossOrigin(url, answered);
                }

                if(IsRedirect(response.StatusCode)) {
                    if(response.Headers.Location is not { } location) {
                        throw new OAuthDiscoveryException(OAuthDiscoveryFailure.HttpError, $"The metadata document at '{current}' redirected without a Location.");
                    }

                    Uri target = location.IsAbsoluteUri ? location : new Uri(current, location);
                    if(!SameOrigin(target, url)) {
                        throw CrossOrigin(url, target);
                    }

                    if(redirects >= this._options.MaxRedirects) {
                        throw new OAuthDiscoveryException(
                            OAuthDiscoveryFailure.TooManyRedirects,
                            $"The metadata document at '{url}' redirected more than {this._options.MaxRedirects} times.");
                    }

                    current = target;
                    continue;
                }

                if(response.StatusCode != HttpStatusCode.OK) {
                    throw new OAuthDiscoveryException(
                        OAuthDiscoveryFailure.HttpError,
                        $"The metadata document at '{current}' answered {(int)response.StatusCode} {response.ReasonPhrase}; RFC 9728 and RFC 8414 require 200.");
                }

                JsonElement json = await this.ReadJsonObjectAsync(response, current, cancellationToken).ConfigureAwait(false);
                return (json, Freshness(response));
            }
        }
    }

    private async Task<JsonElement> ReadJsonObjectAsync(HttpResponseMessage response, Uri url, CancellationToken cancellationToken) {
        string? mediaType = response.Content.Headers.ContentType?.MediaType;
        if(!string.Equals(mediaType, "application/json", StringComparison.OrdinalIgnoreCase)) {
            throw new OAuthDiscoveryException(
                OAuthDiscoveryFailure.InvalidDocument,
                $"The metadata document at '{url}' has content type '{mediaType}', not application/json.");
        }

        if(response.Content.Headers.ContentLength > this._options.MaxDocumentBytes) {
            throw TooLarge(url);
        }

        byte[] body;
        await using(Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false)) {
            using MemoryStream buffer = new();
            byte[] chunk = new byte[16 * 1024];
            int read;

            while((read = await stream.ReadAsync(chunk, cancellationToken).ConfigureAwait(false)) > 0) {
                if(buffer.Length + read > this._options.MaxDocumentBytes) {
                    throw TooLarge(url);
                }

                buffer.Write(chunk, 0, read);
            }

            body = buffer.ToArray();
        }

        try {
            using JsonDocument document = JsonDocument.Parse(body);
            return document.RootElement.ValueKind == JsonValueKind.Object
                ? document.RootElement.Clone()
                : throw new OAuthDiscoveryException(OAuthDiscoveryFailure.InvalidDocument, $"The metadata document at '{url}' is not a JSON object.");
        }
        catch(JsonException exception) {
            throw new OAuthDiscoveryException(OAuthDiscoveryFailure.InvalidDocument, $"The metadata document at '{url}' is not valid JSON.", exception);
        }
    }

    /// <summary>
    /// How long the response may be used: <c>max-age</c> less <c>Age</c>, or nothing under <c>no-store</c>,
    /// <c>no-cache</c>, or without <c>max-age</c> — a document is never used beyond what its server allowed.
    /// </summary>
    private static TimeSpan Freshness(HttpResponseMessage response) {
        CacheControlHeaderValue? cacheControl = response.Headers.CacheControl;
        if(cacheControl is null || cacheControl.NoStore || cacheControl.NoCache || cacheControl.MaxAge is not { } maxAge) {
            return TimeSpan.Zero;
        }

        TimeSpan remaining = maxAge - (response.Headers.Age ?? TimeSpan.Zero);
        return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
    }

    private void RequireSecure(Uri url) {
        bool secure = url.Scheme == Uri.UriSchemeHttps
            || (url.Scheme == Uri.UriSchemeHttp && url.IsLoopback && this._options.AllowHttpOnLoopback);

        if(!secure) {
            throw new OAuthDiscoveryException(
                OAuthDiscoveryFailure.InsecureTransport,
                $"The metadata document at '{url}' would be fetched without TLS; RFC 9728 §7.1 and RFC 8414 §3 require https.");
        }
    }

    private static Uri DeriveUrl(string identifier, string suffix, bool trimTerminatingSlash, string kind) {
        Uri parsed;
        try {
            parsed = WellKnownUri.Parse(identifier, $"A {kind}", "identifier");
        }
        catch(ArgumentException exception) {
            throw new OAuthDiscoveryException(OAuthDiscoveryFailure.InvalidUrl, $"The {kind} is unusable: {exception.Message}", exception);
        }

        return WellKnownUri.Absolute(parsed, WellKnownUri.PathFor(parsed, suffix, trimTerminatingSlash));
    }

    private static bool SameOrigin(Uri a, Uri b) {
        return string.Equals(a.Scheme, b.Scheme, StringComparison.OrdinalIgnoreCase)
            && string.Equals(a.IdnHost, b.IdnHost, StringComparison.OrdinalIgnoreCase)
            && a.Port == b.Port;
    }

    private static bool IsRedirect(HttpStatusCode status) {
        return status is HttpStatusCode.MovedPermanently or HttpStatusCode.Found or HttpStatusCode.SeeOther
            or HttpStatusCode.TemporaryRedirect or HttpStatusCode.PermanentRedirect;
    }

    private static OAuthDiscoveryException Mismatch(string message) => new(OAuthDiscoveryFailure.IdentifierMismatch, message);

    private static OAuthDiscoveryException CrossOrigin(Uri url, Uri target) => new(
        OAuthDiscoveryFailure.CrossOriginRedirect,
        $"The metadata document at '{url}' redirected to '{target}', another origin. A document reached that way could be another party's, so it is not used.");

    private OAuthDiscoveryException TooLarge(Uri url) => new(
        OAuthDiscoveryFailure.DocumentTooLarge,
        $"The metadata document at '{url}' is larger than {this._options.MaxDocumentBytes} bytes.");
}
