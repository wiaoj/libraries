# Wiaoj.WellKnown

Publishes `/.well-known/*` documents: **RFC 9728 OAuth 2.0 Protected Resource Metadata** for an API, and **RFC 8414 OAuth 2.0 Authorization Server Metadata** for an authorization server such as Vaultex. A client that gets a 401 from your API reads this document to find out which authorization server issues tokens for it, and with which scopes. MCP clients follow this flow.

## Installation

```bash
dotnet add package Wiaoj.WellKnown
```

## Usage

```csharp
builder.Services.AddOAuthProtectedResource(resource => {
    resource.Resource = "https://api.example.com";                 // required, see below
    resource.AuthorizationServers.Add("https://auth.example.com");
    resource.ResourceName = "Example API";
    resource.BearerMethodsSupported.Add("header");
});

// Each module publishes the scopes it defines
builder.Services.AddProtectedResourceScopes("assets:read", "assets:write");

app.MapOAuthProtectedResource();   // GET /.well-known/oauth-protected-resource
```

The response looks like this:

```json
{
  "resource": "https://api.example.com",
  "authorization_servers": ["https://auth.example.com"],
  "scopes_supported": ["assets:read", "assets:write"],
  "bearer_methods_supported": ["header"],
  "resource_name": "Example API"
}
```

- **Status and headers.** The response is `200` with `application/json` and `Cache-Control: public, max-age=86400`. `CacheDuration` changes the max-age, and `TimeSpan.Zero` sends `no-cache`.
- **Omitted values.** A parameter that is unset, an empty list or `false` is left out, as RFC 9728 §3.2 requires.
- **Anonymous access.** The endpoint allows anonymous access even under a fallback authorization policy.

## `Resource` is required

`Resource` is the only parameter the RFC requires, and clients compare it exactly. A client that found the document through `https://api.example.com` discards the document unless `resource` is exactly `https://api.example.com` (§3.3). So configure it as the **public** URL.

It is never derived from the request. A proxy changes the scheme and host the application sees, and the `Host` header is supplied by the caller.

## Resources with a path, and several on one host

The document URL comes from the identifier. The rule is: remove a trailing slash after the host, then insert the well-known suffix between the host and the path (§3).

| `Resource` | Served at |
| --- | --- |
| `https://api.example.com` | `/.well-known/oauth-protected-resource` |
| `https://api.example.com/v1` | `/.well-known/oauth-protected-resource/v1` |
| `https://api.example.com/v1/` | `/.well-known/oauth-protected-resource/v1/` |

Register several resources by name. `MapOAuthProtectedResource()` serves each one at its own path:

```csharp
builder.Services.AddOAuthProtectedResource("public", r => r.Resource = "https://api.example.com/v1");
builder.Services.AddOAuthProtectedResource("admin", r => r.Resource = "https://api.example.com/admin");
builder.Services.AddProtectedResourceScopes("admin", ["users:manage"]);
```

`ProtectedResourceMetadataUri.For(resource)` returns the absolute document URL. The challenge in `Wiaoj.WellKnown.JwtBearer` uses the same computation, so the URL it advertises is always the route that answers.

## What startup rejects

The options are validated when the application starts. Every failure is reported in a single `OptionsValidationException`, so you do not have to restart once per mistake.

| Rule | Why |
| --- | --- |
| `Resource` is set, absolute, `https`, and has no query, fragment or user info | Clients compare it exactly. A fragment is not allowed (§1.2), and a query cannot be routed. |
| `AuthorizationServers` and `JwksUri` entries are absolute `https` URLs | They are issuer and key locations |
| `ResourceDocumentation`, `ResourcePolicyUri` and `ResourceTosUri` are absolute URLs | They are links |
| The algorithm lists do not contain `none` | Forbidden by §2 |
| `BearerMethodsSupported` contains only `header`, `body` or `query` | These are the §2 values |
| Every scope is a valid scope token, with no spaces or quotes | RFC 6749 §3.3 |

`http` is accepted on a loopback host (`localhost`, `127.0.0.1`) for local development.

Also mapped at startup: two resources that produce the same document path throw.

## Advertising the document on 401

See `Wiaoj.WellKnown.JwtBearer`. It adds `resource_metadata` to JwtBearer's challenge and keeps `error="invalid_token"` and the other parameters. `ProtectedResourceChallenge.AddOnStarting` does the same for any other authentication handler.

## Authorization server metadata (RFC 8414)

An authorization server publishes where its endpoints are, and what it supports. A client that learned the issuer from a protected resource's `authorization_servers` reads this document to find the token endpoint.

```csharp
builder.Services.AddOAuthAuthorizationServer(server => {
    server.Issuer = "https://vaultex.example.com";                          // required
    server.TokenEndpoint = "https://vaultex.example.com/connect/token";
    server.DeviceAuthorizationEndpoint = "https://vaultex.example.com/connect/device"; // RFC 8628, for the CLI
    server.JwksUri = "https://vaultex.example.com/.well-known/jwks.json";
    server.GrantTypesSupported.AddRange(["client_credentials", "urn:ietf:params:oauth:grant-type:device_code"]);
    server.ResponseTypesSupported.Add("code");                             // required
    server.TokenEndpointAuthMethodsSupported.Add("private_key_jwt");
    server.TokenEndpointAuthSigningAlgValuesSupported.Add("ES256");        // required with private_key_jwt
});

app.MapOAuthAuthorizationServer();   // GET /.well-known/oauth-authorization-server
```

Status, caching, omitted values and anonymous access work as for the protected resource document.

### What is required

RFC 8414 §2 requires less than it seems, and more:

| Parameter | Required |
| --- | --- |
| `issuer` | Always. Absolute `https`, no query or fragment. Clients compare it exactly (§3.3). |
| `response_types_supported` | Always, even for a server with no authorization endpoint. |
| `authorization_endpoint` | Unless no supported grant type uses it. `authorization_code` and `implicit` use it. |
| `token_endpoint` | Unless `implicit` is the only supported grant. |
| `…_auth_signing_alg_values_supported` | When `private_key_jwt` or `client_secret_jwt` is listed for that endpoint. |

An empty `GrantTypesSupported` is **not** "no grant types": clients read it as `authorization_code` and `implicit`, so both endpoints are then required. A service-to-service server lists its grant types explicitly.

Startup also rejects:

- endpoint URLs that are not absolute `https`. Documentation, policy and terms links may be `http`.
- `none` in any algorithm list.
- invalid scope tokens.
- `protected_resources` entries that are not resource identifiers.
- `RequirePushedAuthorizationRequests` without `PushedAuthorizationRequestEndpoint`.

### Issuers with a path

RFC 8414 removes the **terminating slash** of the issuer's path. RFC 9728 removes only the slash after the host.

| `Issuer` | Served at |
| --- | --- |
| `https://vaultex.example.com` | `/.well-known/oauth-authorization-server` |
| `https://vaultex.example.com/tenant1` | `/.well-known/oauth-authorization-server/tenant1` |
| `https://vaultex.example.com/tenant1/` | `/.well-known/oauth-authorization-server/tenant1` |

Several issuers are registered by name, as resources are. Two issuers that differ only by that slash derive the same path, so mapping them throws. `AuthorizationServerMetadataUri.For(issuer)` returns the document URL.

### Parameters

Typed options cover:

- every RFC 8414 §2 parameter
- `device_authorization_endpoint` (RFC 8628)
- `protected_resources` (RFC 9728 §4)
- `pushed_authorization_request_endpoint` and `require_pushed_authorization_requests` (RFC 9126)
- `authorization_response_iss_parameter_supported` (RFC 9207)
- `dpop_signing_alg_values_supported` (RFC 9449)
- `tls_client_certificate_bound_access_tokens` (RFC 8705)

Any other registered parameter goes in `AdditionalParameters`, for example OpenID Connect's `userinfo_endpoint` or `mtls_endpoint_aliases`. A name that has a typed option is refused, because it would bypass validation.

Publishing at `openid-configuration` as well is not supported.

## Discovering these documents

`Wiaoj.WellKnown.Discovery` is the client side. From a 401 or a resource identifier, it fetches and validates both documents.

## Not supported

`signed_metadata` (JWS-signed metadata, RFC 9728 §2.2 and RFC 8414 §2.1) is not supported.
