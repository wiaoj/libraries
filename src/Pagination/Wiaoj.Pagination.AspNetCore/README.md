# Wiaoj.Pagination.AspNetCore

ASP.NET Core integration for the Wiaoj Pagination ecosystem, providing RFC 8288 Web Linking and SIMD-accelerated XxHash3 ETag evaluation with HTTP 304 Not Modified handling.

Extension methods are exposed directly under the `Microsoft.AspNetCore.Builder` namespace for fluent Minimal API routing.

---

## Features

- **RFC 8288 Web Linking:** Automatically formats and appends standard HTTP `Link` headers (`rel="first"`, `rel="prev"`, `rel="next"`, `rel="last"`) for both offset and keyset pagination.
- **SIMD-Accelerated ETag Caching:** Generates high-throughput weak ETags (`W/"..."`) using `XxHash3` (30+ GB/s) and cryptographic strong ETags using `Sha256Hash`.
- **Automatic 304 Not Modified Handling:** Evaluates client `If-None-Match` headers and short-circuits responses to `304 Not Modified` without transferring payload bodies.
- **Endpoint Filter:** Provides a pre-allocated singleton instance for default `.WithPagination()` routes.

> **Note:** Pagination state (`totalCount`, `pageNumber`, `pageSize`, `totalPages`, `hasPrevious`, `hasNext`) is exposed exclusively via the response body's `metadata` object, not via a separate header. This avoids duplicating the same data across two channels — see [`PagedResult<T>`](#) and [`PageMetadata`](#).

---

## Installation

```bash
dotnet add package Wiaoj.Pagination.AspNetCore
```

---

## Usage Examples

### 1. Minimal API Integration

Add `.WithPagination()` to any endpoint returning `PagedResult<T>` or `CursorResult<T>`:

```csharp
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Wiaoj.Pagination;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// Offset pagination with automatic headers and ETag
app.MapGet("/api/products", async (AppDbContext db, [AsParameters] PageRequest request, CancellationToken ct) =>
{
    return await db.Products
        .AsNoTracking()
        .OrderBy(p => p.Id)
        .ToPagedResultAsync(request, ct);
})
.WithPagination();

// Keyset pagination with automatic headers and ETag
app.MapGet("/api/orders", async (AppDbContext db, CursorParameters paging, CancellationToken ct) =>
{
    return await db.Orders
        .AsNoTracking()
        .OrderByDescending(o => o.Id)
        .ToCursorResultAsync(paging, o => o.Id, ct);
})
.WithPagination();

app.Run();
```

> **How the paging parameters are bound is not stylistic.**
>
> For keyset, take **`CursorParameters`**. It binds `cursor`, `limit` and `direction` from the query string
> and converts implicitly to `CursorRequest`, so it goes straight into `ToCursorResultAsync`.
>
> Neither alternative works, in opposite directions:
>
> - A bare `CursorRequest request` binds from a *single* composite value — `?request=cursor:limit:direction` —
>   because the type is `ISpanParsable<T>`. It rejects the `?cursor=…&direction=…` form with **400**, which is
>   exactly the form this package's own `Link` headers emit. The first page works, then `rel="next"` returns
>   400.
> - `[AsParameters] CursorRequest` binds through the record's constructor, where the cursor has no default —
>   so the cursor becomes a **required** query value and the request for the *first* page, the one that
>   carries no cursor yet, returns 400. Giving it a default does not fix it: minimal APIs cannot express an
>   optional parameter of a custom struct type, and the endpoint fails to build at all.
>
> `PageRequest` has no such trap — every one of its constructor parameters has a default — so
> `[AsParameters] PageRequest` is correct for offset paging.

---

### 2. Configuration

Set the defaults once for the application:

```csharp
builder.Services.AddPagination(options => options.EnableETag = false);
```

Every `.WithPagination()` endpoint then uses them. An endpoint that differs states only the difference — its callback applies **on top of** the application's settings, not on top of fresh defaults:

```csharp
app.MapGet("/api/logs", ...)
   .WithPagination(options => options.EnableLinkHeaders = false);   // ETag stays off, from the application
```

Settings layer: library defaults → `AddPagination` → the endpoint. `AddPagination` is optional; an application that never calls it keeps the library defaults. The OpenAPI document is resolved the same way from the same metadata, so it never advertises an ETag the application turned off.

---

### 3. Responses that carry more than the page

A paged response is often an envelope — a workspace view with summaries beside the rows — so it cannot be a `PagedResult<T>`. Say at the endpoint where its metadata is:

```csharp
internal sealed record WorkspaceResponse(
    ProjectSummary Project,
    IReadOnlyList<KeyRow> Items,
    PageMetadata Metadata);

app.MapGet("api/v1/applications/{applicationId}/workspace", Handle)
   .WithPagination<WorkspaceResponse>(response => response.Metadata);
```

The response stays a plain record and implements nothing from this library — how an endpoint is paginated is a fact about the endpoint, not about the contract type. Use a `CursorMetadata` accessor for a keyset envelope.

The declaration is checked when the endpoint is built: a `TResponse` that does not appear in the handler's return type throws there, rather than leaving an endpoint that silently sends no `Link` header. Union return types (`Results<Ok<WorkspaceResponse>, ProblemHttpResult>`) are seen through, and the problem branch is left alone.

> Without this, `.WithPagination()` on an envelope endpoint does **nothing** — the filter only acts on a result it recognises as a page.

---

### 4. Standalone RFC 8288 Link Header Generation

Use `Rfc8288LinkHeaderBuilder` directly in custom middlewares or controllers:

```csharp
using Wiaoj.Pagination.AspNetCore.Linking;

// Offset Pagination Linking
string offsetLinkHeader = Rfc8288LinkHeaderBuilder.Build(
    metadata: pagedResult.Metadata,
    pageUriFactory: page => $"https://api.example.com/items?pageNumber={page}&pageSize=20");

// Keyset Pagination Linking
string keysetLinkHeader = Rfc8288LinkHeaderBuilder.Build(
    metadata: cursorResult.Metadata,
    cursorUriFactory: (cursor, direction) => 
        $"https://api.example.com/items?cursor={cursor.Value}&direction={direction}");

// Set to response
httpContext.Response.Headers.Link = offsetLinkHeader;
```

---

### 5. Standalone ETag Generation & Verification

```csharp
using Wiaoj.Pagination.AspNetCore.Caching;

// 1. Generate ETag from response bytes
byte[] utf8Payload = "{\"items\":[...]}"u8.ToArray();
string etag = ETagGenerator.GenerateWeakETag(utf8Payload); // W/"3fa85f64ac28d019"

// 2. Evaluate incoming If-None-Match header
string? ifNoneMatch = httpContext.Request.Headers.IfNoneMatch;
if (ETagGenerator.IsNotModified(ifNoneMatch, etag))
{
    // Return 304 Not Modified
    return Results.StatusCode(StatusCodes.Status304NotModified);
}
```

---

## HTTP Response Headers Output

When calling an endpoint configured with `.WithPagination()`, the response includes:

```http
HTTP/1.1 200 OK
Content-Type: application/json; charset=utf-8
ETag: W/"5f8a92cb14e03d7a"
Link: <https://api.example.com/items?pageNumber=1&pageSize=20>; rel="first", <https://api.example.com/items?pageNumber=1&pageSize=20>; rel="prev", <https://api.example.com/items?pageNumber=3&pageSize=20>; rel="next", <https://api.example.com/items?pageNumber=5&pageSize=20>; rel="last"

{
  "items": [...],
  "metadata": {
    "totalCount": 100,
    "pageNumber": 2,
    "pageSize": 20,
    "totalPages": 5,
    "hasPrevious": true,
    "hasNext": true
  }
}
```

When a subsequent request is sent with `If-None-Match: W/"5f8a92cb14e03d7a"`, the server returns:

```http
HTTP/1.1 304 Not Modified
ETag: W/"5f8a92cb14e03d7a"
```

---

## License

This project is licensed under the MIT License.