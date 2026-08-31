# Wiaoj.Querying.AspNetCore

ASP.NET Core integration for `Wiaoj.Querying`. Provides zero-configuration Minimal API parameter binding and automatic RFC 7807 schema validation filters.

## Features

- **Minimal API Parameter Binding:** Strongly typed `Query<TEntity>` parameter binding via `IBindableFromHttpContext<T>`.
- **Automatic Endpoint Validation:** `.WithQueryValidation(schema)` endpoint filter validates incoming queries before handler execution.
- **RFC 7807 Validation Responses:** Automatically returns standard `400 Bad Request` (`ValidationProblem`) when requests violate schema rules, limits, or types.
- **Native AOT Compatible:** Reflection-free request binding from `IQueryCollection`.
- **Zero Boilerplate:** Implicit conversions allow `Query<TEntity>` to be passed directly to `.ApplyQuery(...)`.

---

## Installation

Add project reference or NuGet package:

```shell
dotnet add package Wiaoj.Querying.AspNetCore
```

---

## Quick Start (Minimal APIs)

### 1. Define Entity & Schema

```csharp
using Wiaoj.Querying;

public sealed record Product(int Id, string Name, decimal Price, bool IsDeleted);

public static class ProductQuerySchema
{
    public static readonly QuerySchema<Product> Instance = new QuerySchema<Product>()
        .AllowFilter(x => x.Price, QueryOperator.GreaterThanOrEqual, QueryOperator.LessThanOrEqual, QueryOperator.Between)
        .AllowSort(x => x.Price)
        .Property(x => x.Name)
            .AllowFilter(QueryOperator.Contains, QueryOperator.StartsWith)
            .AllowSort()
        .SearchIn(x => x.Name)
        .RequireFilter(x => !x.IsDeleted);
}
```

### 2. Register Minimal API Endpoint

Declare `Query<Product>` in your route handler signature and attach `.WithQueryValidation(...)`:

```csharp
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Wiaoj.Querying;
using Wiaoj.Querying.AspNetCore;
using Wiaoj.Querying.Extensions;

var app = builder.Build();

app.MapGet("/api/products", async (Query<Product> query, AppDbContext db, CancellationToken ct) =>
{
    // query is implicitly converted to QueryRequest
    var results = await db.Products
        .ApplyQuery(query, ProductQuerySchema.Instance)
        .ToListAsync(ct);

    return Results.Ok(results);
})
.WithQueryValidation(ProductQuerySchema.Instance);

app.Run();
```

---

## How It Works

### 1. Parameter Binding (`Query<TEntity>`)

The `Query<TEntity>` record implements `IBindableFromHttpContext<Query<TEntity>>`. During request binding:

- Resolves search term (`q=...`).
- Resolves sort directives (`sort=-price,name`).
- Extracts bracket-style filters (`price[gte]=100`, `name[contains]=pro`).
- Parses unary flags without values (`deletedAt[isNull]`).

> **Note:** `Query<TEntity>` carries only the parsed `QueryRequest` data and does not perform validation during binding. This ensures validation errors can be properly formatted by the filter pipeline.

### 2. Validation Endpoint Filter (`WithQueryValidation`)

The `.WithQueryValidation(schema)` extension adds an `IEndpointFilter` to the endpoint:

1. Locates the `Query<TEntity>` argument in the handler invocation context.
2. Validates the request against the provided `QuerySchema<TEntity>`.
3. If invalid, short-circuits execution and returns `Results.ValidationProblem(...)` (`400 Bad Request`).
4. If valid, proceeds to the route handler.

---

## Validation Response Format

When query parameters fail schema validation, the endpoint returns an RFC 7807 `application/problem+json` response:

#### Example Request

```http
GET /api/products?price[between]=invalid..value&unsupportedField=test&sort=-secretColumn
```

#### Response (`400 Bad Request`)

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more query validation errors occurred.",
  "status": 400,
  "errors": {
    "price": [
      "Range boundary values in 'invalid..value' are not valid for property 'price' of type 'Decimal'."
    ],
    "unsupportedField": [
      "Filtering by field 'unsupportedField' is not allowed."
    ],
    "secretColumn": [
      "Sorting by field 'secretColumn' is not allowed."
    ]
  }
}
```

---

## Type Conversions & Testing

`Query<TEntity>` supports implicit operators for seamless use in tests and queries:

```csharp
// Implicit unwrap to QueryRequest
Query<Product> query = ...;
QueryRequest request = query;

// Implicit wrap from QueryRequest (useful in unit tests)
QueryRequest testRequest = QueryRequest.Parse("price[gte]=10");
Query<Product> wrapped = testRequest;
```
