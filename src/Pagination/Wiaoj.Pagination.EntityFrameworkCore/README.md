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

> The examples below are service methods, where `CursorRequest` is the right parameter to take. If the caller
> is a minimal API endpoint, bind `CursorParameters` from `Wiaoj.Pagination.AspNetCore` there and pass it in —
> it converts implicitly. Binding `CursorRequest` from a request directly does not work in either form.

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

#### The query must be ordered by the cursor key

The seek predicate is built from the key selector. The page window comes from the query's own `ORDER BY`. If those two refer to different columns, each page is cut from one ordering and continued in the other, so rows are skipped and repeated while every call still returns successfully. The ordering is therefore verified on every call, including the first page, and these cases throw `InvalidOperationException`:

| Query | Cursor key | Why it is refused |
| --- | --- | --- |
| `.OrderByDescending(a => a.FileSize)` | `a => a.Id` | Different column |
| *(no ordering)* | `a => a.Id` | The database may return rows in any order |
| `.OrderBy(a => a.Id).ThenBy(a => a.Name)` | `a => a.Id` | `Name` is not part of the seek |
| `.OrderBy(a => a.Id).Select(a => new AssetRow(a.Id, …))` | `x => x.Key` | A constructor cannot be traced back to `a.Id` |

This matters most when the ordering comes from a client. With `Wiaoj.Querying`, `?sort=` chooses the `ORDER BY` while the handler fixes the cursor key. On a keyset endpoint, restrict `AllowSort` to the cursor key.

These shapes are accepted:

- **Filters after the ordering.** `.OrderBy(a => a.Id).Where(…)` is fine. `AsNoTracking()` and `Include()` also pass through.
- **A replaced ordering.** In `.OrderBy(a => a.Name).OrderBy(a => a.Id)`, only the last `OrderBy` counts.
- **Projections that can be followed.** Anonymous types and member initialisers are traced back to their source column: `.OrderBy(a => a.Id).Select(a => new { Key = a.Id, … })` paged on `x => x.Key` works.
- **Omitted trailing keys.** The `Id` tie-breaker injected by the built-in overloads is one example. An omitted key is added to the `ORDER BY` in the direction of the level before it, so tied rows come back in the order the seek assumes.

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
            cursorDecoder: token => token.ToUtf8String(),
            cancellationToken: ct);
}
```

#### Strongly-typed identifiers and other value-converted keys

This is also the overload for an entity whose key is a value object reaching the database through a `ValueConverter`:

```csharp
public sealed class DeliveryLog {
    public NotificationRequestId RequestId { get; private set; }   // -> bigint via SnowflakeIdValueConverter
}
```

Such a key cannot supply a primitive key selector — there is no member access to the underlying `long` that EF Core could translate, so `l => l.RequestId.Value.Value` compiles and then fails at runtime. It does not need one. The seek predicate compares against the whole value object, which the provider already maps to its column, and the codec handles the token entirely in CLR space:

```csharp
return await db.DeliveryLogs
    .OrderBy(l => l.RequestId)
    .ToCursorResultAsync(
        request: request,
        keySelector: l => l.RequestId,
        cursorEncoder: id => CursorToken.FromBytes(BitConverter.GetBytes(id.Value)),
        cursorDecoder: token => new NotificationRequestId(BitConverter.ToInt64(token.ToBytes())),
        cancellationToken: ct);
```

> **`Value` is the wire form, not the payload.** `CursorToken.FromUtf8("ACC-4471").Value` is
> `"QUNDLTQ0NzE"` — the Base64Url text that travels in the URL. Decoding through it produces the encoded
> string, or a `FormatException` if you parse it as a number, and it fails on the *second* page, after the
> first one looked fine. `ToUtf8String()` and `ToBytes()` are the counterparts of `FromUtf8` and
> `FromBytes`; `TryDecode` remains the allocation-free path.

`TKey` only has to satisfy `IComparable<TKey>` — nothing else. The seek is expressed as `key.CompareTo(pivot) > 0`, and EF Core reduces that to the same plain column comparison an operator would produce:

```sql
WHERE "d"."RequestId" > @pivot
```

So relational operators are **not** required, and neither is an implicit implementation — a key implementing `IComparable<T>` explicitly pages exactly the same way. `CompareTo` is never actually invoked for the query; only its shape in the expression tree is read.

---

### 5. Paging a projection instead of the whole entity

Paging the entity and mapping to DTOs afterwards reads every column on every page. Project in the query instead. Carry the key **beside** the response in an anonymous wrapper, then unwrap the page with `Select`, which keeps the cursors and flags:

```csharp
var page = await db.Assets
    .Where(a => a.ApplicationId == appId)
    .OrderByDescending(a => a.Id)
    .Select(a => new {
        Key = a.Id,
        Item = new AssetSummaryResponse(a.Id.Encode(), a.FileName, a.ContentType, a.FileSize)
    })
    .ToCursorResultAsync(request, x => x.Key, AssetIdCodec.Encode, AssetIdCodec.Decode, ct);

return TypedResults.Ok(page.Select(x => x.Item));   // CursorResult<AssetSummaryResponse>, metadata intact
```

```sql
SELECT "a"."Id", "a"."FileName", "a"."ContentType", "a"."FileSize"
FROM "Assets" AS "a"
WHERE "a"."ApplicationId" = @appId AND "a"."Id" < @pivot
ORDER BY "a"."Id" DESC
```

Columns the projection does not use are not read. The seek is translated over the wrapper's `Key` member back to the column.

**The wrapper has to be a type EF Core can see through.** An anonymous type or a member initialiser (`new Row { Key = a.Id, … }`) works. A constructor call does not, and that includes a positional record such as `new AssetRow(a.Id, …)`, which is what response DTOs usually are. There is no binding from `x.Key` back to `a.Id` to follow. If you order before such a projection, the call refuses it. If you order after it, EF Core fails to translate the seek. Either way it fails, and it never pages on the wrong column.

**A non-translatable id encoding is fine in the final projection.** EF Core evaluates the top-level `Select` on the client, so `a.Id.Encode()` can appear there. It cannot appear in a `Where`, an `OrderBy`, or a subquery. On a value-converted id, EF Core guards that call with a null check built from `==`. A `readonly record struct` has that operator. A plain struct without `==` fails with *"The binary operator Equal is not defined"*.

#### A non-unique sort key needs the tie-breaker as a key

The built-in overloads inject the `Id` tie-breaker by finding a property named `Id` on the element type. An anonymous wrapper has no such property, so nothing is injected. When the sort column can repeat, pass the id as a second key yourself:

```csharp
var page = await db.Assets
    .OrderByDescending(a => a.Priority)
    .Select(a => new { a.Priority, a.Id, Item = new AssetSummaryResponse(a.Id.Encode(), a.FileName, a.FileSize) })
    .ToCursorResultAsync(request, x => x.Priority, x => x.Id, PriorityIdCodec.Encode, PriorityIdCodec.Decode, ct);
```

You do not need to write `ThenBy(a => a.Id)`. A trailing key without its own ordering level is added to the `ORDER BY` in the previous level's direction: `ORDER BY "Priority" DESC, "Id" DESC`. Do not rely on naming a wrapper member `Id` to get the tie-breaker back.

A strongly-typed id works in every level of a composite or triple seek, including ids with no relational operators. Types that declare operators, and `string`, are compared exactly as before. Other types are compared through `IComparable<T>.CompareTo`.

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
