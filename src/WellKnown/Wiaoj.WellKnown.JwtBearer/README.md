# Wiaoj.WellKnown.JwtBearer

Advertises a protected resource's RFC 9728 metadata in the `WWW-Authenticate` challenge that JwtBearer sends on a 401.

## Installation

```bash
dotnet add package Wiaoj.WellKnown.JwtBearer
```

## Usage

```csharp
builder.Services.AddOAuthProtectedResource(r => r.Resource = "https://api.example.com/v1");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options => { ... })
    .AddProtectedResourceMetadataChallenge();

app.MapOAuthProtectedResource();
```

A request with no token receives:

```http
HTTP/1.1 401 Unauthorized
WWW-Authenticate: Bearer resource_metadata="https://api.example.com/.well-known/oauth-protected-resource/v1"
```

An expired token keeps JwtBearer's own error, in the same challenge:

```http
WWW-Authenticate: Bearer error="invalid_token", error_description="The token expired at ...", resource_metadata="https://api.example.com/.well-known/oauth-protected-resource/v1"
```

## How it behaves

- **Additive.** The parameter is added as the response starts, after JwtBearer has written its challenge. `error`, `error_description`, `scope` and `realm` are left untouched (§5.1). Only 401 responses are changed; a `403 insufficient_scope` is not.
- **Composes with your events.** An `Events.OnChallenge` you set still runs first. If it calls `HandleResponse()`, your response is left exactly as you wrote it.
- **Derived URL.** The URL comes from `ProtectedResourceMetadataUri.For(resource)`, which is the route `MapOAuthProtectedResource()` serves. With several named resources, choose one per scheme with `AddProtectedResourceMetadataChallenge(scheme, resourceName: "admin")`.
- **`EventsType` is not supported.** Events resolved from `JwtBearerOptions.EventsType` cannot be composed with, and that combination throws instead of silently advertising nothing. Call `ProtectedResourceChallenge.AddOnStarting(context.HttpContext, metadataUrl)` from your events type's `Challenge` method instead.
- **Other handlers.** The same `ProtectedResourceChallenge.AddOnStarting` call works for an authentication handler other than JwtBearer.

## Migrating from `AttachProtectedResourceMetadata`

`context.AttachProtectedResourceMetadata(url)` inside `OnChallenge` is obsolete.

- **What changed.** It used to call `HandleResponse()` and replace the header, which dropped the token error. It now adds the parameter without handling the response.
- **What to do.** Remove the call and register `AddProtectedResourceMetadataChallenge()` instead. The URL is then derived from the registered resource, so there is nothing to keep in step.
