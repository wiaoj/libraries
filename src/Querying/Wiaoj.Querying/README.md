# Wiaoj.Querying

A type-safe, Native AOT–ready query parsing, validation, and LINQ execution engine for .NET.

Parses URL bracket-syntax strings and JSON query payloads into strongly typed AST structures (`QueryRequest`, `Sort`, `Q`, `FilterConditionNode`) and compiles them into validated `IQueryable<T>` expressions without runtime code generation (`Reflection.Emit`).

---

## Features

- **Format-Agnostic Query AST:** Central immutable structures (`QueryRequest`, `Sort`, `Q`, `FilterConditionNode`) representing query criteria independently of transport format.
- **Dual Wire Format Parsing:** High-performance parsers for URL bracket syntax (`BracketQueryParser`) and UTF-8 JSON payloads (`JsonQueryParser`).
- **Pluggable Payload Strategies:** Extensible body parser abstraction (`IQueryPayloadParser`) supporting custom format additions (JSON, Text, FormUrlEncoded, XML, YAML).
- **Strict Schema Whitelisting (`QuerySchema<T>`):** Disallow querying unmapped properties, unauthorized operators, or unindexed fields.
- **Invariants & Defaults:** Enforce mandatory invariants (`RequireFilter`) and contingent fallbacks (`DefaultFilter`, `DefaultSort`).
- **Deterministic Validation:** Diagnostics (`QueryValidationResult`) compatible with RFC 7807 problem details dictionaries.
- **Abuse & Security Safeguards:** Configurable ceilings for filter counts, `IN` clause sizes, sort criteria counts, and raw value string lengths.
- **Deterministic Hashing:** SIMD-accelerated `XxHash3` query fingerprinting (`QueryRequest.QueryHash`) for caching keys and ETag generation.
- **Zero-Allocation Hot Paths:** Built on `ReadOnlySpan<char>`, `ReadOnlySpan<byte>`, `ValueBuffer`, and `Utf8JsonReader`.

---

## Query Syntax Specification

### 1. Bracket Syntax (URL / Plain Text / Form-UrlEncoded)

Filters use the bracket operator syntax: `field[operator]=value`.

```http
q=workstation&price[gte]=1000&category[in]=Electronics,Office&sort=-price,createdAt
```

#### Supported Operators (18 Total)

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

### 2. JSON Payload Syntax (`application/json`)

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

### 3. Sorting (`sort=...`)

Multiple fields separated by commas. Prefix with `-` for descending, optional `+` for ascending:

```http
sort=-price,+createdAt,id
```

### 4. Free-Text Search (`q=...`)

Performs multi-column substring searches across all selectors configured via `.SearchIn(...)`:

```http
q=wireless+mouse
```

---

## Schema Definition (`QuerySchema<T>`)

Schemas enforce a strict whitelist. Unmapped properties and unauthorized operators are rejected during validation.

```csharp
using Wiaoj.Querying;

public sealed class ProductQuerySchema : QuerySchema<Product>
{
    public ProductQuerySchema()
    {
        // 1. Whitelist filterable properties and allowed operators
        AllowFilter(x => x.Category);
        Property(x => x.Price)
            .AllowFilter(QueryOperator.Equal, QueryOperator.GreaterThanOrEqual, QueryOperator.Between)
            .AllowSort();

        // 2. Property Aliasing (exposes field as ?name=... in queries)
        Property(x => x.Title)
            .HasName("name")
            .AllowFilter(QueryOperator.Contains, QueryOperator.StartsWith)
            .AllowSort();

        // 3. Allowed sort fields
        AllowSort(x => x.CreatedAt);

        // 4. Free-text search configuration (q=term)
        SearchIn(x => x.Title, x => x.Category);

        // 5. Invariants (always applied, cannot be bypassed by caller)
        RequireFilter(x => !x.IsDeleted);

        // 6. Default filter (applied only when field is absent in request)
        DefaultFilter(x => x.Status, x => x.Status == Status.Active);

        // 7. Default sort (applied only when request specifies no sort)
        DefaultSort(x => x.CreatedAt, SortDirection.Descending);

        // 8. Ignored parameters (exempt from whitelist validation & AST filter compilation)
        IgnoreParameters("preview", "export");

        // 9. Security & Abuse Limits
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

## Dependency Injection Registration

Register the engine and schemas using the `IQueryingBuilder` API:

```csharp
using Microsoft.Extensions.DependencyInjection;
using Wiaoj.Querying;

// 1. Standard registration with class-based schema and ignored parameters (e.g. PaginationParameters.All)
services.AddQuerying()
    .IgnoreParameters(PaginationParameters.All)
    .Configure(options => options.AllowBodyPayloads = true)
    .AddSchema<Product, ProductQuerySchema>();

// 2. Or scan an assembly for all QuerySchema<T> implementations:
// services.AddQuerying()
//     .IgnoreParameters(PaginationParameters.All)
//     .AddSchemasFromAssemblyContaining<Program>();

// 3. Or inline configuration:
// services.AddQuerying()
//     .AddSchema<Product>(schema => schema.AllowFilter(x => x.Price));
```

---

## Programmatic Usage & LINQ Execution

```csharp
using Wiaoj.Querying;
using Wiaoj.Querying.Parsers;
using Wiaoj.Querying.Extensions;

// 1. Parse query input (URL string or JSON payload)
string rawQuery = "?name[contains]=desk&price[between]=100..500&sort=-price";

if (QueryRequest.TryParse(rawQuery, out QueryRequest request))
{
    // 2. Validate against schema
    QueryValidationResult validation = schema.Validate(request);
    if (!validation.IsValid)
    {
        Dictionary<string, string[]> errors = validation.ToDictionary();
        // Handle validation failure...
        return;
    }

    // 3. Apply to IQueryable<T> (EF Core, InMemory, LINQ to Objects)
    IQueryable<Product> query = dbContext.Products.ApplyQuery(request, schema);

    List<Product> results = await query.ToListAsync();
}
```

---

## Custom Payload Parsers (`IQueryPayloadParser`)

Extend the engine to support custom request body formats (e.g. XML, YAML) via the strategy pattern:

```csharp
using Wiaoj.Querying.Parsers;

public sealed class YamlQueryPayloadParser : IQueryPayloadParser
{
    public bool CanParse(string mediaType) =>
        string.Equals(mediaType, "application/x-yaml", StringComparison.OrdinalIgnoreCase);

    public bool TryParse(ReadOnlySpan<byte> utf8Payload, out QueryRequest result)
    {
        // Custom YAML parsing implementation...
        return YamlParser.TryParse(utf8Payload, out result);
    }
}

// Register via DI builder:
services.AddQuerying()
    .AddPayloadParser<YamlQueryPayloadParser>();
```

---

## Execution Pipeline

When `query.ApplyQuery(request, schema)` is executed on an `IQueryable<T>`:

```text
[ Incoming QueryRequest ]
         │
         ▼
 1. RequireFilter predicates (always applied, even if request is empty)
         │
         ▼
 2. SearchIn predicate (q=...) combined via OrElse across text selectors
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

## Diagnostic Error Codes (`QueryValidationErrorCode`)

| Error Code | Meaning |
| :--- | :--- |
| `FieldNotFilterable` | Field is not configured for filtering in the schema. |
| `OperatorNotAllowed` | Operator is not permitted for the target property. |
| `InvalidValueFormat` | Value could not be parsed into the property type. |
| `MalformedRange` | Range syntax does not contain valid boundaries separated by `..`. |
| `FieldNotSortable` | Field is not configured for sorting in the schema. |
| `MaxFilterCountExceeded` | Total filters exceeded `MaxFilterCount`. |
| `MaxInValuesCountExceeded` | Elements in `in`/`notIn` collection exceeded `MaxInValuesCount`. |
| `MaxSortFieldsCountExceeded` | Sort fields count exceeded `MaxSortFieldsCount`. |
| `FilterValueTooLong` | Filter value character length exceeded `MaxFilterValueLength`. |
| `SearchTermTooLong` | Search term character length exceeded `MaxSearchTermLength`. |

---

## License

This project is licensed under the MIT License.
