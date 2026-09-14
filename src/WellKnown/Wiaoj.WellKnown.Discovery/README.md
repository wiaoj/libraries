# Wiaoj.WellKnown.Discovery

Finds out, from a protected API itself, which authorization server issues its tokens and where that server's endpoints are. It reads **RFC 9728 protected resource metadata** and then **RFC 8414 authorization server metadata**. Each document is validated before it is used.

It depends only on `Microsoft.Extensions.Http`, not on ASP.NET Core or a token library, so it works in a service, a worker or a CLI. It is Native AOT compatible.

## Installation

```bash
dotnet add package Wiaoj.WellKnown.Discovery
```

## Usage

```csharp
builder.Services.AddOAuthDiscoveryClient(options =>
    options.TrustedAuthorizationServers.Add("https://vaultex.example.com"));
```

### From a known resource identifier (between our services)

```csharp
public sealed class PrismClient(OAuthDiscoveryClient discovery) {
    public async Task<string?> TokenEndpointAsync(CancellationToken ct) {
        OAuthDiscoveryResult found = await discovery.DiscoverAsync("https://prism.example.com/v1", ct);

        // found.ProtectedResource.Resource        "https://prism.example.com/v1" — the token's audience (RFC 8707 resource)
        // found.ProtectedResource.ScopesSupported
        // found.AuthorizationServer.DeviceAuthorizationEndpoint — RFC 8628, for a CLI
        return found.AuthorizationServer.TokenEndpoint;   // obtain a token with your OAuth client
    }
}
```

### From a 401 (a client that doesn't know the API)

```csharp
using HttpResponseMessage response = await http.GetAsync(userSuppliedUrl, ct);

if(response.StatusCode == HttpStatusCode.Unauthorized
   && await discovery.DiscoverAsync(response, ct) is { } found) {
    // resource == userSuppliedUrl (RFC 9728 §3.3)
}
```
`GetProtectedResourceMetadataAsync` and `GetAuthorizationServerMetadataAsync` fetch one document each. Parameters without a property of their own are available in `Json`.

The client stops before getting a token. How the token is obtained (client credentials, device flow, token exchange) belongs to your OAuth client.

## What is checked

Each failure throws `OAuthDiscoveryException`, and its `Failure` says why.

| Check | Failure | Source |
| --- | --- | --- |
| `resource` is identical to the identifier looked up, code point for code point | `IdentifierMismatch` | RFC 9728 §3.3, §6 |
| `issuer` is identical to the issuer looked up | `IdentifierMismatch` | RFC 8414 §3.3, §6.2 |
| From a 401, `resource` is identical to the challenged URL (see below) | `IdentifierMismatch` | RFC 9728 §3.3 |
| Documents are fetched over `https` (`http` only on loopback, for development) | `InsecureTransport` | RFC 9728 §7.1, RFC 8414 §3 |
| A redirect to another origin is never followed, including one a custom handler followed itself | `CrossOriginRedirect` | |
| At most `MaxRedirects` same-origin redirects | `TooManyRedirects` | |
| `200`, `application/json`, a JSON object, known parameters of the right type, at most `MaxDocumentBytes` | `HttpError`, `InvalidDocument`, `DocumentTooLarge` | RFC 9728 §3.2, RFC 8414 §3.2 |
| The resource lists an authorization server, and a trusted one when `TrustedAuthorizationServers` is set | `NoAuthorizationServer`, `UntrustedAuthorizationServer` | RFC 9728 §7.6, §7.7 |
| Challenges don't advertise two different metadata URLs | `AmbiguousChallenge` | |

The checks run on every call, whether the document comes from the network or from the cache.

### A 401 or the identifier?

RFC 9728 §3.3 says that metadata found through a 401 is used only if its `resource` is **identical** to the URL that was requested. There are two ways to discover an API:

| Situation | Call |
| --- | --- |
| The client doesn't know the API, and the user gives it a URL (for example an MCP server's single endpoint) | `DiscoverAsync(unauthorizedResponse)` |
| The client knows the API's identifier, for example from config (Verba → `https://prism.example.com/v1`) | `DiscoverAsync("https://prism.example.com/v1")` |

In the first case, a 401 from `…/v1/keys` can't use metadata for `…/v1`, because the RFC forbids it. When the identifier is known, the second call is used. It fetches the document from the URL derived from the identifier and requires `resource` to be identical to that identifier, so a 401 isn't needed.
## Choosing the authorization server

If `TrustedAuthorizationServers` is empty, the first entry in `authorization_servers` is used.

If it is set, the first listed server that is also trusted is used. A resource that lists none of them is refused before anything is fetched from those servers.

RFC 9728 §7.7 warns that fetching whatever a resource names enables server-side request forgery. Between services that know each other, the trusted list is the control. The client does not block private IP addresses. A client that talks to arbitrary APIs should add that check itself, in the handler.

## Caching

Documents are cached by URL, according to their response:

- **Freshness.** A document is cached for `max-age` minus `Age`, capped at `MaxCacheDuration`. It is never served after that.
- **Not cached.** Nothing is cached under `no-store` or `no-cache`, or when `max-age` is missing. Failures are not cached either.
- **Size bound.** At most `MaxCachedDocuments` documents are kept. When the cache is full, expired documents go first, then the ones fetched longest ago.
- **Concurrent requests.** Concurrent requests for the same document share one fetch. One caller cancelling does not fail the others.
- **Challenges.** A new `resource_metadata` challenge means the metadata may have changed (§5.2). It re-fetches the document if the cached copy is older than `ChallengeRefreshInterval` (30 seconds). A burst of 401s therefore causes at most one fetch.

`AddOAuthDiscoveryClient` shares one cache across every typed client instance. Without dependency injection, keep one `OAuthDiscoveryClient` instance, because a new instance starts with an empty cache.

## Handlers

The registered primary handler has `AllowAutoRedirect = false`, so a cross-origin redirect is refused before it is followed. If you replace the primary handler with `ConfigurePrimaryHttpMessageHandler`, the client still refuses a response the handler reached through another origin.

## Not supported

- `signed_metadata`
- OpenID Connect's `openid-configuration` location
