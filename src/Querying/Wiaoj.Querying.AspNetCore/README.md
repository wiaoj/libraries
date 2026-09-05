# Wiaoj.Querying.AspNetCore

ASP.NET Core integration for `Wiaoj.Querying`. Provides Minimal API parameter binding (`Query<TEntity>`), RFC 10008 HTTP `QUERY` request body handling, DI registration extensions, and automatic RFC 7807 validation endpoint filters.

---

## Features

- **Minimal API Parameter Binding:** Strongly typed `Query<TEntity>` parameter binding via `IBindableFromHttpContext<T>`.
- **RFC 10008 HTTP `QUERY` & Body Support:** Binds query payloads from request bodies (`application/json`, `text/plain`, `application/x-www-form-urlencoded`) on `QUERY` and `POST` methods, with automatic fallback to URL query strings on `GET`.
- **DI-Driven Endpoint Validation:** `.WithQueryValidation<TEntity>()` endpoint filter automatically resolves `QuerySchema<TEntity>` from the DI container and validates incoming requests before handler execution.
- **RFC 7807 Validation Responses:** Automatically returns standard `400 Bad Request` (`ValidationProblemDetails`) when requests violate schema rules, limits, or types.
- **Protocol Status Codes:** Enforces `415 Unsupported Media Type` (with `Accept-Query` response header), `413 Payload Too Large` (via `IHttpMaxRequestBodySizeFeature`), and `400 Bad Request` (on malformed syntax).
- **Route Group Support:** Applies schema validation across individual endpoints (`RouteHandlerBuilder`) and route groups (`RouteGroupBuilder`).
- **Native AOT Compatible:** Reflection-free parameter binding and stream parsing.
- **Zero Boilerplate:** Implicit conversions allow `Query<TEntity>` to be passed directly to `.ApplyQuery(...)`.

---

## Installation

```shell
dotnet add package Wiaoj.Querying.AspNetCore
```

---

## Quick Start (Minimal APIs)

### 1. Define Entity & Query Schema

```csharp
using Wiaoj.Querying;

public sealed record Product(int Id, string Name, decimal Price, string Category, bool IsDeleted);

public sealed class ProductQuerySchema : QuerySchema<Product>
{
    public ProductQuerySchema()
    {
        AllowFilter(x => x.Category);
        Property(x => x.Price)
            .AllowFilter(QueryOperator.Equal, QueryOperator.GreaterThanOrEqual, QueryOperator.Between)
            .AllowSort();

        Property(x => x.Name)
            .HasName("name")
            .AllowFilter(QueryOperator.Contains, QueryOperator.StartsWith)
            .AllowSort();

        SearchIn(x => x.Name);
        RequireFilter(x => !x.IsDeleted);
        ConfigureLimits(maxFilters: 10, maxInValues: 20, maxSortFields: 3);
    }
}
```

### 2. Configure Dependency Injection (`Program.cs`)

```csharp
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Wiaoj.Querying;
using Wiaoj.Querying.AspNetCore;
using Wiaoj.Querying.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(opt => opt.UseNpgsql(...));

// Register querying engine, global ignored parameters (e.g. pagination), and schemas
builder.Services.AddQuerying()
    .IgnoreParameters("page", "size", "cursor", "direction")
    .AddSchema<Product, ProductQuerySchema>();

// Native ASP.NET Core RFC 7807 Problem Details customization
builder.Services.AddProblemDetails(options => {
    options.CustomizeProblemDetails = ctx => {
        ctx.ProblemDetails.Instance = ctx.HttpContext.Request.Path;
    };
});

var app = builder.Build();
```

### 3. Expose Minimal API Endpoint

Declare `Query<Product>` in your route handler and attach `.WithQueryValidation<Product>()`:

```csharp
app.MapMethods("/api/products", ["GET", "QUERY"], async (
    Query<Product> query,
    QuerySchema<Product> schema, //or ProductQuerySchema schema, 
    AppDbContext db,
    CancellationToken ct) =>
{
    // `query` implicitly converts to `QueryRequest`
    List<Product> products = await db.Products
        .AsNoTracking()
        .ApplyQuery(query, schema)
        .ToListAsync(ct);

    return Results.Ok(products);
})
.WithQueryValidation<Product>(); // Resolves schema from DI and validates

app.Run();
```

---

## How It Works

### 1. Parameter Binding (`Query<TEntity>` & `QueryRequestBinder`)

The `Query<TEntity>` record implements `IBindableFromHttpContext<Query<TEntity>>`. During request binding, `QueryRequestBinder` inspects the request:

1. **HTTP `QUERY` / `POST` Requests:**
   - Reads `Content-Type` header.
   - If `application/json` -> parses body via `JsonQueryParser`.
   - If `text/plain` or `application/x-www-form-urlencoded` -> parses body via `BracketQueryParser`.
   - If custom format registered (e.g. YAML) -> delegates to matching `IQueryPayloadParser`.
   - If body is empty -> falls back to URL query parameters.
2. **HTTP `GET` / Other Requests:**
   - Reads directly from URL query collection (`IQueryCollection`).

> **Note:** `Query<TEntity>` performs parsing only. Schema validation occurs during the endpoint filter pipeline to produce standard RFC 7807 responses.

### 2. Validation Endpoint Filter (`WithQueryValidation`)

The `.WithQueryValidation<TEntity>()` extension adds an endpoint filter factory that:

1. Resolves `QuerySchema<TEntity>` as a singleton from DI (or uses an explicitly passed instance).
2. Locates the bound `Query<TEntity>` argument in `EndpointFilterInvocationContext.Arguments`.
3. Validates the request against `QuerySchema<TEntity>`.
4. If validation fails, short-circuits the pipeline and returns `Results.ValidationProblem(...)` (`400 Bad Request`).
5. If valid, passes execution to the endpoint handler.

---

## HTTP Status Codes & Error Handling

`QueryRequestBinder` and `QueryValidationEndpointFilter` return standardized status codes based on request state:

| Status Code | Condition | Behavior / Headers |
| :--- | :--- | :--- |
| **`200 OK`** | Successful binding & validation | Request proceeds to handler. |
| **`400 Bad Request`** | Malformed JSON or bracket syntax | Returns `BadHttpRequestException(400)` before reaching handler. |
| **`400 Validation Problem`** | Schema rule or limit violation | Returns RFC 7807 `ValidationProblemDetails` dictionary. |
| **`413 Payload Too Large`** | Body exceeds `IHttpMaxRequestBodySizeFeature` | Aborts body reading to protect server memory. |
| **`415 Unsupported Media Type`** | Unrecognized body `Content-Type` on `QUERY`/`POST` | Sets `Accept-Query: application/json, text/plain, application/x-www-form-urlencoded`. |

---

## Validation Response Format (RFC 7807)

When query parameters fail schema validation, the endpoint returns an RFC 7807 `application/problem+json` payload:

#### Example Request

```http
GET /api/products?price[between]=invalid..value&unregisteredField=test&sort=-secretColumn
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
    "unregisteredField": [
      "Filtering by field 'unregisteredField' is not allowed."
    ],
    "secretColumn": [
      "Sorting by field 'secretColumn' is not allowed."
    ]
  }
}
```

> **Enriching Problem Details (`instance`, `traceId`, etc.):**
> `WithQueryValidation` produces standard ASP.NET Core `Results.ValidationProblem` responses that natively route through the platform's `IProblemDetailsService`. Configure `builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = ctx => ctx.ProblemDetails.Instance = ctx.HttpContext.Request.Path)` centrally in `Program.cs` to enrich validation responses without ad-hoc library settings.

---

## Route Groups (`MapGroup`)

`WithQueryValidation<TEntity>()` can be applied to route groups to enforce validation across grouped endpoints:

```csharp
var productApi = app.MapGroup("/api/products");

productApi.MapMethods("/", ["GET", "QUERY"], async (Query<Product> query, QuerySchema<Product> schema, AppDbContext db) =>
    Results.Ok(await db.Products.ApplyQuery(query, schema).ToListAsync()))
.WithQueryValidation<Product>();
```

---

## Type Conversions & Testing

`Query<TEntity>` provides implicit conversion operators for use in unit and integration tests:

```csharp
// Implicit unwrap to QueryRequest
Query<Product> query = ...;
QueryRequest request = query;

// Implicit wrap from QueryRequest (construct in tests without HttpContext)
QueryRequest testRequest = QueryRequest.Parse("price[gte]=100&sort=-createdAt");
Query<Product> wrapped = testRequest;
```

---

## License

This project is licensed under the MIT License.
