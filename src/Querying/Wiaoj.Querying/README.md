# Wiaoj.Querying

A type-safe, Native AOT–ready query parsing, validation, and LINQ execution engine for .NET.

Parses URL bracket-syntax strings and JSON query payloads into strongly typed AST structures (`QueryRequest`, `Sort`, `Q`, `FilterConditionNode`) and compiles them into validated `IQueryable<T>` expressions without runtime code generation (`Reflection.Emit`).

> The query language itself — `QueryRequest` and its nodes, the parsers, `QueryRequestBuilder`, validation results — lives in **`Wiaoj.Querying.Abstractions`**, which this package depends on. A contract assembly that only *carries* a query references the abstractions and takes no dependency injection with it. Namespaces are the same in both.

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

### Several schemas for one entity

A schema is a query contract, and one entity can have several: an admin surface and a public surface, for example. Register each one as a class, and inject the class you mean:

```csharp
services.AddQuerying()
    .AddSchema<Asset, AdminAssetSchema>()
    .AddSchema<Asset, PublicAssetSchema>();

public sealed class ListAssetsHandler(PublicAssetSchema schema) { ... }
```

`QuerySchema<Asset>` resolves only while the entity has a single schema. With two or more, resolving it throws and names the schemas, so nothing silently picks one. Registering a second inline or instance schema for the same entity, or mixing one with a class schema, throws at registration.

### Binding a response: `QuerySchema<TEntity, TResponse>`

```csharp
public sealed class PublicAssetSchema : QuerySchema<Asset, AssetSummaryResponse> {
    public PublicAssetSchema() {
        Project(a => new AssetSummaryResponse(a.Id.Encode(), a.FileName, a.FileSize));
        AllowFilter(a => a.FileName);
    }
}
```

It is still a `QuerySchema<Asset>`. `VerifyContract()` runs when the container hands the schema out, and you can call it yourself on a schema built by hand. It throws in two cases:

- no projection was set;
- a filterable or sortable field is never read by the projection, and is not marked `NotInResponse()`.

A field counts as read when its member path appears anywhere in the projection. For example, `a.Id.Encode()` reads `Id`, `a.Id.Value` reads `Id`, and `a.Owner` reads `Owner.Name`. Custom filters are exempt from the check.

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

Steps 2 and 3 in one call, throwing `QueryValidationException` when the request is not valid:

```csharp
IQueryable<Product> query = dbContext.Products.ApplyValidatedQuery(request, schema);
```

> **`ApplyQuery` does not validate.** It silently skips a filter on an unknown field or with a refused operator, stops at the filter limit, and truncates an `in` list past its limit — and every skip *widens* the result. Behind an HTTP endpoint with `WithQueryValidation` that is covered. Anywhere else — an RPC handler, a message consumer, a stored query — use `ApplyValidatedQuery`, or a misspelt field name returns every row.

---

## Field Names That Follow a Naming Policy

A field's name defaults to its CLR member path — `ContentType` — while an application's bodies are usually camelCase or snake_case:

```csharp
builder.Services.AddQuerying(querying => querying
    .UseFieldNamingPolicy(JsonNamingPolicy.SnakeCaseLower)   // or UseJsonNamingPolicy() in ASP.NET Core
    .AddSchema<Product, ProductQuerySchema>());
```

The rendered name is accepted as an **alias** — `content_type` and `ContentType` both work — and is what `DescribeFields()` reports, so a generated document uses it. That is what makes a non-case-only policy safe: `content_type` does not match `ContentType` case-insensitively, so publishing it without accepting it would describe a parameter the server rejects. Fields named with `HasName` are left as written. The policy is applied on every registration path, including a schema injected into a constructor. A schema can also set its own: `schema.UseFieldNamingPolicy(...)`.

---

## Custom Filters

A filter that is not an entity member — computed through a subquery, answered by another service — belongs on the schema, not beside it:

```csharp
public sealed class KeyQuerySchema : QuerySchema<TranslationKey> {
    public KeyQuerySchema() {
        Property(k => k.Namespace).AllowFilter(QueryOperator.Equal, QueryOperator.In).Describe("Logical grouping of keys.");

        CustomFilter<bool>("hasScreenshot").AllowFilter(QueryOperator.Equal);
        CustomFilter<EntryStatus>("statuses")
            .AllowFilter(QueryOperator.In)
            .WithParser(raw => Enum.Parse<EntryStatus>(raw.Replace("-", ""), ignoreCase: true));
    }
}
```

Declared this way it is validated like any field (operator, value type, limits), described in the document, kept on this side by `Partition`, and read back typed:

```csharp
if(schema.TryGetFilterValue(request, "hasScreenshot", out bool hasScreenshot)) { /* apply it */ }
IReadOnlyList<EntryStatus> statuses = schema.GetFilterValues<EntryStatus>(request, "statuses");
```

`ApplyQuery` does not apply such a filter — the endpoint does, with the value it reads. To have the engine apply it, give a predicate: `CustomFilter<bool>("hasScreenshot", (key, has) => key.Screenshots.Any() == has)`. Predicates support `eq`, `neq`, `in` and `notIn`. Custom filters cannot be sorted.

---

## Building and Composing Requests

```csharp
QueryRequest query = QueryRequest.CreateBuilder()
    .In("keyId", pageKeyIds.Select(id => id.Encode()))
    .In("locale", requestedLocales)
    .OrderBy("locale")
    .Build();
```

The builder renders values the way the parser reads them (invariant culture, round-trip dates) and refuses what the language cannot express rather than producing a different query: a comma inside an `in` value, an **empty `in` list — ignored when applied, so it would match every row** — and a range bound containing `..`.

| | |
| --- | --- |
| `request.Partition(schema)` | `(Owned, Remainder)` — the filters a schema declares, and the rest. Split by ownership: a declared field with a refused operator stays owned and fails validation here, instead of being forwarded as a different query. |
| `request.Only("locale")` | only the named fields; the search term is dropped |
| `request.Without("statuses")` | everything but the named fields; the search term is kept |
| `QueryRequest.Merge(a, b)` | filters AND; `a`'s sort wins and `b` refines it; two different search terms throw |

A `QueryRequest` serialises through System.Text.Json as the same body a `QUERY` request sends, so it can cross any JSON transport inside a contract. A malformed payload throws rather than reading as an empty — unfiltered — request.

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
