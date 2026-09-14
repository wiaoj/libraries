# Wiaoj.WellKnown

Serves `/.well-known/*` documents for ASP.NET Core applications, and discovers them from any .NET client.

| Package | Purpose |
| --- | --- |
| **`Wiaoj.WellKnown`** | RFC 9728 Protected Resource Metadata and RFC 8414 Authorization Server Metadata: the documents, identifiers with a path, and startup validation |
| **`Wiaoj.WellKnown.JwtBearer`** | Adds `resource_metadata` to JwtBearer's 401 challenge without dropping its error |
| **`Wiaoj.WellKnown.Discovery`** | The client: from a 401 or a resource identifier to validated resource and authorization server metadata. No ASP.NET Core dependency. |

## How the pieces meet

```
client ──GET /v1/keys──────────────▶ API (Wiaoj.WellKnown + .JwtBearer)
       ◀─401 WWW-Authenticate: Bearer resource_metadata="…/oauth-protected-resource/v1"
       ──GET resource metadata─────▶ API            → resource, authorization_servers
       ──GET /.well-known/oauth-authorization-server ▶ Vaultex (Wiaoj.WellKnown)
       ◀─issuer, token_endpoint, device_authorization_endpoint
```

`Wiaoj.WellKnown.Discovery` runs the client side and checks each document before it is used.

## Migrating from the first version

The first version was merged in #94 and used from Verba as a local reference.

| Before | Now |
| --- | --- |
| `Resource` optional; when missing, derived from `Request.Scheme` and `Request.Host` | **Required**, validated at startup. Configure the public URL. |
| `authorization_servers` always written, even when empty | Omitted when empty, like every unset parameter |
| `bearer_methods_supported` always `["header"]` | Written only when set: `resource.BearerMethodsSupported.Add("header")` |
| Only `/.well-known/oauth-protected-resource` mapped | The path is derived from `Resource`, so a resource like `https://api.example.com/v1` is served at `…/v1` |
| `AttachProtectedResourceMetadata(url)` in the core package | Moved to `Wiaoj.WellKnown.JwtBearer` and marked obsolete. Use `AddProtectedResourceMetadataChallenge()`. |
| `AddProtectedResourceScopes("a", "b")` | Unchanged. For a named resource, pass a collection: `AddProtectedResourceScopes("admin", ["users:manage"])` |
