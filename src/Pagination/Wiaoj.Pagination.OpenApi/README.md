# Wiaoj.Pagination.OpenApi

Makes `WithPagination()` visible in generated OpenAPI documents.

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

---

## What it adds

| | |
| --- | --- |
| `Link` response header | on every 2xx response, when link headers are enabled |
| `ETag` response header | on every 2xx response, when ETag evaluation is enabled |
| `304` response | when ETag evaluation is enabled |
| Paging query parameters | `page` / `size`, or `cursor` / `limit` / `direction` |

The parameter set is chosen from the endpoint's **declared response type** rather than guessed: `PagedResult<T>` means offset paging, `CursorResult<T>` means keyset. An endpoint returning neither gets headers documented and no parameters invented for it.

Parameters the document already carries are left alone. A handler taking `[AsParameters] CursorRequest` already has `cursor`, `limit` and `direction` described by ASP.NET Core, and adding them twice produces an invalid document.

Defaults and bounds come from the types themselves — `CursorRequest.DefaultLimit`, `CursorRequest.MaxLimit`, `PageRequest.DefaultSize`, `PageRequest.MaxSize` — so the document cannot drift from the values the code actually enforces.

---

## License

MIT
