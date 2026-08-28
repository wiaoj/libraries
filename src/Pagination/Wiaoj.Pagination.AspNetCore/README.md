# Wiaoj.Pagination.AspNetCore

ASP.NET Core integration for the Wiaoj Pagination ecosystem, providing RFC 8288 Web Linking, RFC 6648 compliant metadata headers, and SIMD-accelerated XxHash3 ETag evaluation with HTTP 304 Not Modified handling.

Extension methods are exposed directly under the `Microsoft.AspNetCore.Builder` namespace for fluent Minimal API routing.

---

## Features

- **RFC 8288 Web Linking:** Automatically formats and appends standard HTTP `Link` headers (`rel="first"`, `rel="prev"`, `rel="next"`, `rel="last"`) for both offset and keyset pagination.
- **RFC 6648 Compliant Metadata:** Exposes pagination state via the clean `Pagination` header, eliminating legacy `X-` header anti-patterns.
- **SIMD-Accelerated ETag Caching:** Generates high-throughput weak ETags (`W/"..."`) using `XxHash3` (30+ GB/s) and cryptographic strong ETags using `Sha256Hash`.
- **Automatic 304 Not Modified Handling:** Evaluates client `If-None-Match` headers and short-circuits responses to `304 Not Modified` without transferring payload bodies.
- **Zero-Allocation Endpoint Filter:** Provides a pre-allocated singleton instance for default `.WithPagination()` routes.

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
app.MapGet("/api/orders", async (AppDbContext db, [AsParameters] CursorRequest request, CancellationToken ct) =>
{
    return await db.Orders
        .AsNoTracking()
        .OrderByDescending(o => o.Id)
        .ToCursorResultAsync(request, o => o.Id, ct);
})
.WithPagination();

app.Run();
```

---

### 2. Custom Configuration

You can configure headers, disable ETags, or rename metadata headers per endpoint:

```csharp
app.MapGet("/api/logs", async (AppDbContext db, CursorRequest request, CancellationToken ct) =>
{
    return await db.Logs
        .OrderByDescending(l => l.CreatedAt)
        .ToCursorResultAsync(request, l => l.CreatedAt, ct);
})
.WithPagination(options =>
{
    options.EnableETag = false;                  // Disable ETag calculation
    options.EnableLinkHeaders = true;            // Enable RFC 8288 Link header
    options.MetadataHeaderName = "Pagination";   // Custom header name (or null to disable)
});
```

---

### 3. Standalone RFC 8288 Link Header Generation

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

### 4. Standalone ETag Generation & Verification

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
Pagination: Page 2 of 5 (Total: 100)
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
