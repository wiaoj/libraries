# Wiaoj.Querying

A modular, type-safe, Native AOT–ready query parsing, validation, and LINQ execution engine for .NET.

Translates bracket-syntax query strings into validated LINQ expressions for `IQueryable<T>` without reflection-heavy expression compilation or dynamic code generation.

---

## Projects in this Repository

| Project | Target | Description |
| :--- | :--- | :--- |
| [`Wiaoj.Querying`](./src/Wiaoj.Querying) | `.NET Standard` / `.NET 8+` | **Core Engine.** Contains query AST structures (`QueryRequest`, `Sort`, `Q`), schema whitelisting (`QuerySchema<T>`), span-based parsers, deterministic hash generation (`XxHash3`), and `IQueryable<T>` extension methods. Zero external dependencies. |
| [`Wiaoj.Querying.AspNetCore`](./src/Wiaoj.Querying.AspNetCore) | `ASP.NET Core` | **Web Integration.** Provides Minimal API parameter binding (`Query<TEntity>`), endpoint validation filters (`WithQueryValidation`), and automatic RFC 7807 (`ProblemDetails`) response generation. |

---

## End-to-End Overview

### 1. Define Entity and Query Schema

```csharp
using Wiaoj.Querying;

public sealed record Product(int Id, string Name, decimal Price, DateTime CreatedAt, bool IsDeleted);

public static class ProductQuerySchema
{
    public static readonly QuerySchema<Product> Instance = new QuerySchema<Product>()
        // Filters & Operators Whitelist
        .AllowFilter(x => x.Price, QueryOperator.GreaterThanOrEqual, QueryOperator.LessThanOrEqual, QueryOperator.Between)
        .Property(x => x.Name)
            .AllowFilter(QueryOperator.Contains, QueryOperator.StartsWith)
            .AllowSort()
            
        // Sorting
        .AllowSort(x => x.CreatedAt)
        
        // Free-text Search (q=...)
        .SearchIn(x => x.Name)
        
        // Invariants & Defaults
        .RequireFilter(x => !x.IsDeleted)
        .DefaultSort(x => x.CreatedAt, SortDirection.Descending)
        
        // Security & Abuse Limits
        .ConfigureLimits(maxFilters: 10, maxInValues: 20, maxSortFields: 3);
}
```

### 2. Expose via Minimal API (with automatic validation)

```csharp
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Wiaoj.Querying.AspNetCore;
using Wiaoj.Querying.Extensions;

var app = builder.Build();

app.MapGet("/api/products", async (Query<Product> query, AppDbContext db, CancellationToken ct) =>
{
    // Automatic binding: `query` contains parsed Request AST
    // `query` implicitly converts to `QueryRequest`
    List<Product> products = await db.Products
        .ApplyQuery(query, ProductQuerySchema.Instance)
        .ToListAsync(ct);

    return Results.Ok(products);
})
.WithQueryValidation(ProductQuerySchema.Instance); // Automatically returns 400 ValidationProblem on violations

app.Run();
```

---

## Query Syntax Quick Reference

### Filters (`field[op]=value`)

- Equality: `?status=Active` or `?status[eq]=Active`
- Comparisons: `?price[gte]=100&price[lt]=500`
- Substring & Prefix: `?name[contains]=pro&sku[startsWith]=ABC`
- Ranges: `?price[between]=50..150`
- Collections: `?category[in]=Electronics,Books`
- Unary Null Checks: `?deletedAt[isNull]` or `?verifiedAt[isNotNull]`

### Sorting (`sort=...`)

- Prefix with `-` for descending, optional `+` for ascending:
- `?sort=-price,+createdAt,id`

### Free-Text Search (`q=...`)

- Searches across all selectors registered via `.SearchIn(...)`:
- `?q=wireless+keyboard`

---

## Key Characteristics

- **Memory & Allocation Conscious:** String parsing paths leverage `ReadOnlySpan<char>` and UTF-8 spans (`IUtf8SpanParsable<T>`).
- **Native AOT Compatible:** LINQ expressions are constructed via standard `System.Linq.Expressions` APIs targeting known entity shapes without dynamic code emission (`Emit`).
- **Deterministic Hashing:** Built-in `XxHash3` fingerprint calculation per request (`QueryRequest.QueryHash`), suitable for ETags and query-level caching keys.
- **Strict Security Boundaries:** Rejects undeclared properties, unpermitted operators, malformed ranges, and limits collection sizes to prevent database query degradation.

---

## License

This project is licensed under the MIT License.
