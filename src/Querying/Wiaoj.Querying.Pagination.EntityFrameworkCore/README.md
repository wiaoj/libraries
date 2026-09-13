# Wiaoj.Querying.Pagination.EntityFrameworkCore

Pages the result of a query contract in a single call, using Entity Framework Core. The call validates the request, applies the filters, search and sort, projects each row to the response type, and pages the result.

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
