# Wiaoj.Querying

A type-safe query parsing, validation, and LINQ execution engine for .NET. 

Translates URL bracket-syntax query strings and RFC 10008 HTTP `QUERY` body payloads into validated LINQ expressions for `IQueryable<T>` without runtime code emission (`Reflection.Emit`).

---

## Packages

| Package | Target | Description |
| :--- | :--- | :--- |
| `Wiaoj.Querying` | `.NET 10+` | Core query AST (`QueryRequest`, `Sort`, `Q`), schema engine (`QuerySchema<T>`), payload parser strategies (`IQueryPayloadParser`), and `IQueryable<T>` LINQ expression compiler (`ApplyQuery`). Zero external dependencies. |
| `Wiaoj.Querying.AspNetCore` | `ASP.NET Core 8+` | ASP.NET Core parameter binding (`Query<TEntity>`), RFC 10008 payload binder (`QueryRequestBinder`), DI registration (`AddQuerying`), and endpoint validation filters (`WithQueryValidation`). |

---

## Query Protocols and Wire Formats

The engine accepts query parameters via URL query strings (`GET`) or request bodies (`QUERY` / `POST`).

### 1. URL Query String (`GET`)

Parameters are expressed as key-value pairs using bracket operators:

```http
GET /api/products?q=workstation&price[gte]=1000&category[in]=Electronics,Office&sort=-price,createdAt
```

### 2. Request Body (`QUERY` / `POST` - RFC 10008)

When sending queries via request body, the engine inspects the `Content-Type` header:

#### A) JSON Payload (`Content-Type: application/json`)

```json
{
  "q": "workstation",
  "sort": "-price,createdAt",
  "filters": [
    { "field": "category", "op": "in", "value": "Electronics,Office" },
    { "field": "price", "op": "gte", "value": 1000 },
    { "field": "deletedAt", "op": "isNull" }
  ]
}
```

#### B) Plain Text / Form URL Encoded (`Content-Type: text/plain` or `application/x-www-form-urlencoded`)

```http
q=workstation&price[gte]=1000&sort=-price
```

---

## Supported Query Operators

The engine supports 18 operators:

| Operator | Syntax | Description | Example |
| :--- | :--- | :--- | :--- |
| `Equal` | `field=val` or `field[eq]=val` | Equality comparison | `status=Active` or `status[eq]=Active` |
| `NotEqual` | `field[neq]=val` | Inequality comparison | `status[neq]=Archived` |
| `GreaterThan` | `field[gt]=val` | Greater than | `price[gt]=100` |
| `GreaterThanOrEqual` | `field[gte]=val` | Greater than or equal | `price[gte]=100` |
| `LessThan` | `field[lt]=val` | Less than | `stock[lt]=5` |
| `LessThanOrEqual` | `field[lte]=val` | Less than or equal | `stock[lte]=5` |
| `Contains` | `field[contains]=val` | Substring match | `title[contains]=desk` |
| `NotContains` | `field[notContains]=val` | Substring exclusion | `title[notContains]=outlet` |
| `StartsWith` | `field[startsWith]=val` | Prefix match | `sku[startsWith]=PRO-` |
| `NotStartsWith` | `field[notStartsWith]=val` | Prefix exclusion | `sku[notStartsWith]=TEMP-` |
| `EndsWith` | `field[endsWith]=val` | Suffix match | `email[endsWith]=@corp.com` |
| `NotEndsWith` | `field[notEndsWith]=val` | Suffix exclusion | `email[notEndsWith]=.tmp` |
| `In` | `field[in]=v1,v2` | Set inclusion | `category[in]=Books,Games` |
| `NotIn` | `field[notIn]=v1,v2` | Set exclusion | `status[notIn]=Banned,Deleted` |
| `Between` | `field[between]=low..high` | Inclusive range | `price[between]=100..500` |
| `NotBetween` | `field[notBetween]=low..high` | Exclusive range | `price[notBetween]=100..500` |
| `IsNull` | `field[isNull]` | Unary null check | `deletedAt[isNull]` |
| `IsNotNull` | `field[isNotNull]` | Unary not-null check | `assignedTo[isNotNull]` |

---

## Schema Definition (`QuerySchema<T>`)

`QuerySchema<T>` enforces a strict whitelist policy. Only explicitly configured properties and operators are permitted.

```csharp
using Wiaoj.Querying;

public sealed class ProductQuerySchema : QuerySchema<Product>
{
    public ProductQuerySchema()
    {
        // 1. Whitelist filterable properties
        AllowFilter(x => x.Category);
        Property(x => x.Price)
            .AllowFilter(QueryOperator.Equal, QueryOperator.GreaterThanOrEqual, QueryOperator.Between)
            .AllowSort();

        // 2. Property Aliasing (exposes property as ?name=... in query)
        Property(x => x.Title)
            .HasName("name")
            .AllowFilter(QueryOperator.Contains, QueryOperator.StartsWith)
            .AllowSort();

        // 3. Allowed sort fields
        AllowSort(x => x.CreatedAt);

        // 4. Free-text search fields (q=...)
        SearchIn(x => x.Title, x => x.Category);

        // 5. Invariants (always applied, cannot be overridden by caller)
        RequireFilter(x => !x.IsDeleted);

        // 6. Default filter (applied only when caller does not filter on this field)
        DefaultFilter(x => x.Status, x => x.Status == Status.Active);

        // 7. Default sort (applied only when request specifies no sort)
        DefaultSort(x => x.CreatedAt, SortDirection.Descending);

        // 8. Ignored parameters (exempt from whitelist validation & AST filter compilation)
        IgnoreParameters("preview", "export");

        // 9. Abuse limits
        ConfigureLimits(
            maxFilters: 10,
            maxInValues: 20,
            maxSortFields: 3,
            maxFilterValueLength: 512,
            maxSearchTermLength: 256
        );
    }
}
```

---

## ASP.NET Core & Dependency Injection Integration

### 1. Registration (`Program.cs`)

Register the query infrastructure, ignored parameters, and schemas using the `IQueryingBuilder` API:

```csharp
var builder = WebApplication.CreateBuilder(args);

// Register querying engine, global ignored parameters (e.g. PaginationParameters.All or custom strings), and schemas
builder.Services.AddQuerying()
    .IgnoreParameters(PaginationParameters.All)
    .AddSchema<Product, ProductQuerySchema>();

// Native ASP.NET Core RFC 7807 Problem Details customization
builder.Services.AddProblemDetails(options => {
    options.CustomizeProblemDetails = ctx => {
        ctx.ProblemDetails.Instance = ctx.HttpContext.Request.Path;
    };
});

// Or scan an assembly:
// builder.Services.AddQuerying()
//     .IgnoreParameters(PaginationParameters.All)
//     .AddSchemasFromAssemblyContaining<Program>();
```

### 2. Endpoint Definition

Bind `Query<TEntity>` directly in Minimal API route handlers. Apply `.WithQueryValidation<TEntity>()` to validate input and return RFC 7807 responses on errors.

```csharp
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Wiaoj.Querying;
using Wiaoj.Querying.AspNetCore;
using Wiaoj.Querying.Extensions;

var app = builder.Build();

app.MapMethods("/api/v1/products", ["GET", "QUERY"], async (
    Query<Product> query,
    QuerySchema<Product> schema,
    AppDbContext db,
    CancellationToken ct) =>
{
    // query implicitly casts to QueryRequest
    var products = await db.Products
        .AsNoTracking()
        .ApplyQuery(query, schema)
        .ToListAsync(ct);

    return Results.Ok(products);
})
.WithQueryValidation<Product>(); // Automatically resolves schema from DI and validates

app.Run();
```

---

## Extensibility: Custom Payload Parsers (`IQueryPayloadParser`)

The engine uses a strategy pattern for request body parsing. Custom formats (such as XML or YAML) can be registered via `IQueryPayloadParser`:

```csharp
using Wiaoj.Querying.Parsers;

public sealed class YamlQueryPayloadParser : IQueryPayloadParser
{
    public bool CanParse(string mediaType) =>
        string.Equals(mediaType, "application/x-yaml", StringComparison.OrdinalIgnoreCase);

    public bool TryParse(ReadOnlySpan<byte> utf8Payload, out QueryRequest result)
    {
        // Parse YAML bytes into QueryRequest
        return YamlParser.TryParse(utf8Payload, out result);
    }
}

// Register in DI:
builder.Services.AddQuerying()
    .AddPayloadParser<YamlQueryPayloadParser>();
```

---

## HTTP Status Codes & Error Handling

`QueryRequestBinder` and `QueryValidationEndpointFilter` return standardized status codes:

| Status Code | Trigger Condition | Behavior |
| :--- | :--- | :--- |
| **`200 OK`** | Successful query evaluation | Query applied to `IQueryable<T>`. |
| **`400 Bad Request`** | Malformed JSON/bracket syntax | Thrown by `QueryRequestBinder` on syntax failure. |
| **`400 Validation Problem`** | Schema rule violation | Returned as RFC 7807 `ValidationProblemDetails` by `QueryValidationEndpointFilter`. |
| **`413 Payload Too Large`** | Body exceeds `IHttpMaxRequestBodySizeFeature` | Request aborted before exhausting server memory. |
| **`415 Unsupported Media Type`** | Request body with unmapped `Content-Type` | Accompanied by the `Accept-Query` response header. |

### Validation Error Codes (`QueryValidationErrorCode`)

- `FieldNotFilterable`: Field is not registered in schema.
- `OperatorNotAllowed`: Operator is not permitted for this field.
- `InvalidValueFormat`: Type conversion failed for the target property type.
- `MalformedRange`: Invalid delimiter or missing boundary in `between`/`notBetween`.
- `FieldNotSortable`: Field is not allowed for sorting.
- `MaxFilterCountExceeded`: Number of filters exceeded configured limit.
- `MaxInValuesCountExceeded`: Items in `in`/`notIn` clause exceeded limit.
- `MaxSortFieldsCountExceeded`: Sort criteria count exceeded limit.
- `FilterValueTooLong`: Raw filter string exceeded character length limit.
- `SearchTermTooLong`: `q` parameter exceeded character length limit.

---

## Execution Pipeline

When `ApplyQuery(query, schema)` is executed on an `IQueryable<T>`:

```text
[ Incoming QueryRequest ]
         │
         ▼
 1. RequireFilter predicates (always applied, even if request is empty)
         │
         ▼
 2. SearchIn predicate (q=...) combined via OrElse across selectors
         │
         ▼
 3. Client Filter conditions combined via AndAlso
         │
         ▼
 4. DefaultFilter predicates applied for fields absent in request
         │
         ▼
 5. Sorting: Client sort applied; falls back to DefaultSort if unspecified
         │
         ▼
[ Resulting IQueryable<T> / SQL Translation ]
```

---

## License

This project is licensed under the MIT License.
