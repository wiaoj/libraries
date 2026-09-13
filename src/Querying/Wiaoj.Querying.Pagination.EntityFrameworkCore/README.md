# Wiaoj.Querying.Pagination.EntityFrameworkCore

Pages the result of a query contract in a single call, using Entity Framework Core. The call validates the request, applies the filters, search and sort, projects each row to the response type, and pages the result, with offset or keyset pages.

It connects `Wiaoj.Querying` and `Wiaoj.Pagination.EntityFrameworkCore`. Because the connection lives in this package, `Wiaoj.Querying` itself does not depend on EF Core.

## Installation

```bash
dotnet add package Wiaoj.Querying.Pagination.EntityFrameworkCore
```

## Offset pages

Declare the endpoint's contract as a `QuerySchema<TEntity, TResponse>` with a tie-breaker:

```csharp
public sealed class PublicAssetSchema : QuerySchema<Asset, AssetSummaryResponse> {
    public PublicAssetSchema() {
        Project(a => new AssetSummaryResponse(a.Id.Encode(), a.FileName, a.FileSize));
        AllowFilter(a => a.FileName);
        AllowSort(a => a.FileSize);
        DefaultSort(a => a.FileSize, SortDirection.Descending);
        TieBreaker(a => a.Id);
    }
}
```

Then page with one call:

```csharp
app.MapGet("/assets", async (Query<Asset> query, [AsParameters] PageRequest page, PublicAssetSchema schema, AppDbContext db, CancellationToken ct) =>
        TypedResults.Ok(await db.Assets
            .Where(a => a.ApplicationId == appId)
            .ToPagedResultAsync(query, schema, page, ct)))
   .WithQueryValidation<Asset, PublicAssetSchema>()
   .WithPagination();
```

The call does the following:

- **Validates the request.** A misspelt filter throws `QueryValidationException`. It is never silently skipped, which would return a wider result.
- **Orders the rows.** It applies the caller's `sort` if there is one, otherwise the schema's default sort, and always ends with the tie-breaker. Without a tie-breaker, rows that share a sort value can appear on two pages and be missing from another, so a schema without one throws.
- **Projects in SQL.** Only the columns the projection uses are read.
- **Returns the response shape.** The result is `PagedResult<TResponse>` with its metadata. There is no mapping step where the metadata could be dropped.

Any ordering you apply to the source before the call is replaced, because the contract decides the order.

## Keyset pages

A cursor-paged endpoint lets the caller pick the sort, and the cursor seeks on that sort:

```csharp
public sealed class AssetFeedSchema : QuerySchema<Asset, AssetSummaryResponse> {
    public AssetFeedSchema() {
        Project(a => new AssetSummaryResponse(a.Id.Encode(), a.FileName, a.FileSize));
        AllowFilter(a => a.FileName);
        Property(a => a.CreatedAt).AsCursor();      // sortable, and pageable by cursor
        Property(a => a.FileName).AsCursor();
        AllowSort(a => a.FileSize);                 // sortable on offset endpoints only
        DefaultSort(a => a.CreatedAt, SortDirection.Descending);
        TieBreaker(a => a.Id, id => id.Value.ToString(CultureInfo.InvariantCulture),
                              text => new AssetId(long.Parse(text, CultureInfo.InvariantCulture)));
    }
}

CursorResult<AssetSummaryResponse> page = await db.Assets
    .Where(a => a.ApplicationId == appId)
    .ToCursorResultAsync(query, schema, cursorRequest, ct);
```

**Why this combination is safe.** Applying a client-chosen sort and then paging with a cursor fixed on another key loses rows without any error (#73). Here the cursor is built from the sort keys followed by the tie-breaker, so the seek continues exactly where the ordering stopped.

**What the call enforces:**

| Situation | Result |
| --- | --- |
| `sort=` names a field that is not `AsCursor()` | `QueryValidationException` (`FieldNotCursorSortable`) → 400 |
| A cursor is sent back with a different `sort`, including only a changed direction | `QueryValidationException` (`CursorSortChanged`) → 400 |
| The cursor cannot be read | `QueryValidationException` (`InvalidCursor`) → 400 |
| No `sort=` | The schema's default sort, whose fields must be `AsCursor()`. With no default sort, the tie-breaker alone. |
| The schema has no tie-breaker, or no codec for it | `InvalidOperationException`: a server configuration error |
| A cursor key is null on a page boundary | `InvalidOperationException`: a keyset seek cannot move past `NULL` |

Behind `WithQueryValidation`, a `QueryValidationException` thrown from the handler becomes the same 400 `ValidationProblem` that an invalid query string gets.

**Cursor keys.** Built-in codecs cover strings, integers, `decimal`, floating point, `bool`, `Guid`, `DateTime`, `DateTimeOffset`, `DateOnly`, `TimeOnly`, `TimeSpan` and enums. A strongly-typed id needs a codec. A nullable value type cannot be a cursor key.

**The cursor itself.** It records the sort it was issued for as a fingerprint. That fingerprint detects a changed sort; it is not a signature. If callers must not be able to forge a cursor, sign it with `SignedCursorToken`.
