# Wiaoj.Pagination.OpenApi

Makes pagination visible in generated OpenAPI documents — both what `WithPagination()` does to an endpoint, and the shape of what it returns.

---

## Installation

```bash
dotnet add package Wiaoj.Pagination.OpenApi
```

```csharp
builder.Services.AddOpenApi(options => options.AddWiaojPagination());
```

That is the whole setup. Endpoints without `WithPagination()` are left untouched, so it is safe to add once for the entire document.

---

## The problem it solves

`WithPagination()` installs an endpoint filter. The filter writes RFC 8288 `Link` headers, computes an `ETag`, and answers `304 Not Modified` when the client sends a matching `If-None-Match` — **none of which appears anywhere in the handler's signature.**

So a document generated without this package describes an endpoint that returns 200 and sets no headers. That is not the endpoint that exists. A client generated from it has no way to follow `rel="next"`, because as far as the document is concerned there is no `Link` header to read.

Neither is the **result type** described. `PagedResult<T>` and `CursorResult<T>` serialise through hand-written converters, and their CLR properties are an `EquatableArray<T>` and a `CursorToken` rather than an array and a string — nothing a schema generator reading properties can make sense of. Its answer to a type it cannot read is an empty schema, and `openapi-typescript` renders that as:

```ts
CursorResultOfProductDto: unknown;
```

The responses are correct; only the document is silent about them. It surfaces one step out, in the generated client, and the rational move for whoever hits it is to go back to whichever paging style *is* described — which is the wrong reason to choose a pagination strategy.

---

## What it adds

| | |
| --- | --- |
| `PagedResult<T>` / `CursorResult<T>` schemas | `{ items: T[], metadata }`, with `items` referencing `T`'s own schema |
| `PageMetadata` / `CursorMetadata` schemas | shared components, referenced rather than inlined per endpoint |
| `Link` response header | on every 2xx response, when link headers are enabled |
| `ETag` response header | on every 2xx response, when ETag evaluation is enabled |
| `304` response | when ETag evaluation is enabled |
| Paging query parameters | `page` / `size`, or `cursor` / `limit` / `direction` |

A cursor is described as a nullable **string**, not as the struct it is. A caller sends back what it was given; the inside of a cursor is not part of the contract, and describing it would invite clients to read it.

The parameter set is chosen from the endpoint's **declared response type** rather than guessed: `PagedResult<T>` means offset paging, `CursorResult<T>` means keyset. An endpoint returning neither gets headers documented and no parameters invented for it.

Parameters the document already carries are left alone. A handler taking `[AsParameters] PageRequest` already has `page` and `size` described by ASP.NET Core, and adding them twice produces an invalid document. A handler taking `CursorParameters` has *nothing* described — a type that binds itself contributes no parameters to the document — so this is where `cursor`, `limit` and `direction` come from.

A handler taking a bare `CursorRequest` or `PageRequest` — without `[AsParameters]` — is a different endpoint: it binds the whole request from **one** query value, in the compact format `cursor:limit:direction` or `page:size`. The document is given that grammar, and the separate parameters are *not* added, because that endpoint does not accept them.

Defaults and bounds come from the types themselves — `CursorRequest.DefaultLimit`, `CursorRequest.MaxLimit`, `PageRequest.DefaultSize`, `PageRequest.MaxSize` — so the document cannot drift from the values the code actually enforces.

---

## License

MIT
