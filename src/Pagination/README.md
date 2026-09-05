# Wiaoj.Pagination

A modular, high-performance pagination toolkit for .NET applications supporting offset-based and keyset (cursor-based) pagination, cryptographic cursor signing, and database extensions.

---

## Packages

| Package | Description | Target Layer |
| :--- | :--- | :--- |
| **`Wiaoj.Pagination`** | Core primitives, request/response records, cursor tokens, HMAC signing, and JSON converters. Zero external dependencies. | Domain, Application, Contracts |
| **`Wiaoj.Pagination.EntityFrameworkCore`** | Asynchronous LINQ query extensions (`ToPagedResultAsync`, `ToCursorResultAsync`) optimized for relational databases. | Infrastructure, Persistence |
| **`Wiaoj.Pagination.AspNetCore`** | RFC 8288 Link headers, RFC 6648 metadata, and XxHash3 ETag evaluation (304 Not Modified) for Minimal APIs. | Web API, Presentation |

---

## Architecture & Layering

The solution follows Clean Architecture separation of concerns to avoid leaking infrastructure dependencies into core application layers:

```text
┌──────────────────────────────────────────────────────────┐
│                   API / Presentation                     │
│               (Minimal APIs, Controllers)                │
└────────────────────────────┬─────────────────────────────┘
                             │
                             ▼
┌──────────────────────────────────────────────────────────┐
│              Application / Domain Layers                 │
│         Depends on: Wiaoj.Pagination (Core)              │
│    (PageRequest, PagedResult<T>, CursorToken, Metadata)  │
└────────────────────────────▲─────────────────────────────┘
                             │
                             ▼
┌──────────────────────────────────────────────────────────┐
│                   Infrastructure Layer                   │
│   Depends on: Wiaoj.Pagination.EntityFrameworkCore       │
│           (IQueryable extensions, SQL Execution)         │
└──────────────────────────────────────────────────────────┘
```

---

## Quick Starts

### 1. Offset Pagination (Classic Page Number & Size)

```csharp
using Wiaoj.Pagination;
using Wiaoj.Pagination.EntityFrameworkCore;

// Handler / Service
public async Task<PagedResult<ProductDto>> GetProductsAsync(
    PageRequest request, 
    AppDbContext db, 
    CancellationToken ct)
{
    return await db.Products
        .AsNoTracking()
        .Where(p => p.IsActive)
        .OrderBy(p => p.Id)
        .Select(p => new ProductDto(p.Id, p.Name, p.Price))
        .ToPagedResultAsync(request, ct);
}
```

---

### 2. Keyset / Cursor Pagination (High-Performance Seek)

Keyset pagination eliminates costly `COUNT(*)` and large `OFFSET` queries by fetching $N + 1$ items to detect forward/backward boundaries:

```csharp
using Wiaoj.Pagination;
using Wiaoj.Pagination.EntityFrameworkCore;

// Handler / Service
public async Task<CursorResult<OrderDto>> GetOrdersAsync(
    CursorRequest request, 
    AppDbContext db, 
    CancellationToken ct)
{
    return await db.Orders
        .AsNoTracking()
        .OrderByDescending(o => o.Id)
        .ToCursorResultAsync(
            request: request,
            keySelector: o => o.Id,
            cursorEncoder: id => CursorToken.FromUtf8(id.ToString()),
            cursorDecoder: token => long.Parse(token.Value),
            cancellationToken: ct);
}
```

---

### 3. Cryptographic Cursor Signing (HMAC-SHA256)

Prevent client-side cursor tampering and ID enumeration:

```csharp
using Wiaoj.Pagination;

ReadOnlySpan<byte> secretKey = "your-32-byte-secret-key-here-!!!"u8;

// 1. Sign cursor before sending to client
CursorToken rawToken = CursorToken.FromUtf8("order_9901");
SignedCursorToken signedToken = SignedCursorToken.Sign(rawToken, secretKey);

// 2. Verify incoming cursor from client
if (SignedCursorToken.TryParse(requestCursorString, out var signed) &&
    signed.TryUnsign(secretKey, out CursorToken validToken))
{
    // Token is verified and untampered
}
```

---

### 4. Standard Query Parameters (`PaginationParameters`)

Central constants defining HTTP query string parameter names across offset and keyset pagination:

```csharp
using Wiaoj.Pagination;

// Available constants:
// PaginationParameters.Page      => "page"
// PaginationParameters.Size      => "size"
// PaginationParameters.Cursor    => "cursor"
// PaginationParameters.Direction => "direction"
// PaginationParameters.Limit     => "limit"
// PaginationParameters.All       => ["page", "size", "cursor", "direction", "limit"]
```

---

<!-- ## Repository Structure

```text
Pagination/
├── src/
│   ├── Wiaoj.Pagination/                       # Core contracts, tokens, JSON converters
│   │   ├── JsonConverters/
│   │   ├── CursorDirection.cs
│   │   ├── CursorMetadata.cs
│   │   ├── CursorRequest.cs
│   │   ├── CursorResult.cs
│   │   ├── CursorToken.cs
│   │   ├── PageMetadata.cs
│   │   ├── PageRequest.cs
│   │   ├── PagedResult.cs
│   │   └── SignedCursorToken.cs
│   └── Wiaoj.Pagination.EntityFrameworkCore/    # EF Core IQueryable async extensions
│       ├── Extensions/
│       └── Providers/
└── tests/
    ├── Wiaoj.Pagination.Tests.Unit/            # Target-Member unit test suite
    └── Wiaoj.Pagination.EntityFrameworkCore.Tests.Integration/ # SQLite integration tests
``` 

---
-->
## Building and Testing

```bash
# Build solution
dotnet build

# Run all unit and integration tests
dotnet test
```

---

## License

This project is licensed under the MIT License.
