# Wiaoj.Pagination.EntityFrameworkCore

Asynchronous Entity Framework Core query extensions for offset-based and keyset (cursor-based) pagination with $N+1$ count elimination, automatic sort direction detection, and binary cursor codecs.

Extension methods are exposed directly under the `Microsoft.EntityFrameworkCore` namespace for zero-configuration discoverability.

---

## Features

- **64-bit Offset Pagination (`ToPagedResultAsync`):** Utilizes `LongCountAsync` to support tables with billions of rows (`BIGINT`).
- **Out-of-Bounds Short-Circuiting:** Bypasses data `SELECT` queries entirely when the database is empty or the requested offset exceeds the total record count.
- **Zero-Cost Count Keyset Pagination (`ToCursorResultAsync`):** Uses the $N+1$ limit technique to evaluate boundary navigation flags without executing `COUNT(*)` queries.
- **Automatic Sort Direction Detection:** Inspects the query's LINQ expression tree to handle `ASC` and `DESC` sorting seamlessly across `Forward` and `Backward` navigation.
- **Built-in Binary Codecs:** Native big-endian binary encoding for `SnowflakeId`, `long`, `int`, `Guid`, and `DateTimeOffset` cursor keys without string formatting overhead.
- **Cached Expression Execution:** Caches compiled key selector delegates in a thread-safe dictionary to eliminate runtime IL compilation overhead on the hot path.

---

## Installation

```bash
dotnet add package Wiaoj.Pagination.EntityFrameworkCore
```

---

## Usage Examples

### 1. Offset Pagination

```csharp
using Microsoft.EntityFrameworkCore;
using Wiaoj.Pagination;

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

You can also pass raw integers without constructing a `PageRequest`:

```csharp
var result = await db.Products
    .OrderBy(p => p.Id)
    .ToPagedResultAsync(pageNumber: 2, pageSize: 20, ct);
```

---

### 2. Keyset / Cursor-Based Pagination (Built-in Types)

Built-in overloads handle binary cursor serialization for `SnowflakeId`, `long`, `int`, `Guid`, and `DateTimeOffset` without requiring manual codecs:

#### Distributed Unique Key (`SnowflakeId`)

```csharp
using Microsoft.EntityFrameworkCore;
using Wiaoj.Pagination;
using Wiaoj.Primitives.Snowflake;

public async Task<CursorResult<Message>> GetMessagesAsync(
    CursorRequest request, 
    AppDbContext db, 
    CancellationToken ct)
{
    return await db.Messages
        .AsNoTracking()
        .OrderBy(m => m.Id)
        .ToCursorResultAsync(request, m => m.Id, ct);
}
```

#### 64-Bit Integer Key (`long`)

```csharp
using Microsoft.EntityFrameworkCore;
using Wiaoj.Pagination;

public async Task<CursorResult<Order>> GetOrdersAsync(
    CursorRequest request, 
    AppDbContext db, 
    CancellationToken ct)
{
    return await db.Orders
        .AsNoTracking()
        .OrderBy(o => o.Id)
        .ToCursorResultAsync(request, o => o.Id, ct);
}
```

#### Timestamp Key (`DateTimeOffset`)

```csharp
public async Task<CursorResult<LogEntry>> GetLogsAsync(
    CursorRequest request, 
    AppDbContext db, 
    CancellationToken ct)
{
    return await db.Logs
        .AsNoTracking()
        .OrderByDescending(l => l.CreatedAt)
        .ToCursorResultAsync(request, l => l.CreatedAt, ct);
}
```

---

### 3. Bidirectional Navigation Matrix

The engine inspects whether the input query uses `.OrderBy(...)` or `.OrderByDescending(...)` and automatically adjusts the SQL predicate and directional sort order:

| Base Query Order | Navigation Direction | SQL Seek Predicate | SQL Query Order | In-Memory Alignment |
| :---: | :---: | :---: | :---: | :---: |
| **ASC** | `Forward` | `key > pivot` | `ASC` | Preserved |
| **ASC** | `Backward` | `key < pivot` | `DESC` | Reversed back to `ASC` |
| **DESC** | `Forward` | `key < pivot` | `DESC` | Preserved |
| **DESC** | `Backward` | `key > pivot` | `ASC` | Reversed back to `DESC` |

```csharp
// 1. Fetch forward
var forwardReq = new CursorRequest(currentCursor, limit: 10, CursorDirection.Forward);
var forwardPage = await db.Orders
    .OrderByDescending(o => o.Id)
    .ToCursorResultAsync(forwardReq, o => o.Id, ct);

// 2. Fetch backward
var backwardReq = new CursorRequest(forwardPage.Metadata.StartCursor, limit: 10, CursorDirection.Backward);
var previousPage = await db.Orders
    .OrderByDescending(o => o.Id)
    .ToCursorResultAsync(backwardReq, o => o.Id, ct);
```

---

### 4. Custom Key Codecs

For composite identifiers or custom types implementing `IComparable<TKey>`, supply custom encoding and decoding delegates:

```csharp
public async Task<CursorResult<Account>> GetAccountsAsync(
    CursorRequest request, 
    AppDbContext db, 
    CancellationToken ct)
{
    return await db.Accounts
        .OrderBy(a => a.AccountNumber)
        .ToCursorResultAsync(
            request: request,
            keySelector: a => a.AccountNumber,
            cursorEncoder: accNo => CursorToken.FromUtf8(accNo),
            cursorDecoder: token => token.Value,
            cancellationToken: ct);
}
```

---

## Architectural Behavior

### The N+1 Limit Optimization

When requesting a window of size $N$, the engine queries $N + 1$ records (`.Take(N + 1)`):

- If $N + 1$ records are returned, `HasNext` is set to `true`, and the extra item is removed before returning.
- If $N$ or fewer records are returned, `HasNext` is set to `false`.
- **Result:** Exact boundary detection with zero `COUNT(*)` database queries.

### Offset Short-Circuiting

In offset pagination, the total count is fetched first via `LongCountAsync`. If `TotalCount == 0` or `skip >= TotalCount`, the data query is completely skipped, returning an empty `PagedResult<T>` immediately.

---

## License

This project is licensed under the MIT License.
