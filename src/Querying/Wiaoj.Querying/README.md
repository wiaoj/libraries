# Wiaoj.Querying

A type-safe, Native AOT–compatible query engine for parsing, validating, and applying URL-driven filters, search terms, and sort criteria directly to `IQueryable<T>`.

## Features

- **AST-based Query Parsing:** Parses bracket-syntax query strings into strongly typed data structures (`QueryRequest`, `FilterConditionNode`, `Sort`, `Q`) using `ReadOnlySpan<char>` and UTF-8 spans with minimal allocations.
- **Strict Schema-Driven Whitelisting (`QuerySchema<T>`):** Disallow querying unmapped properties or unauthorized operators.
- **Deterministic Validation:** Detailed diagnostic errors (`QueryValidationResult`) compatible with RFC 7807 `ProblemDetails`.
- **LINQ Expression Generation:** Safely translates validated filter, search, and sort parameters into optimized `Expression<Func<T, bool>>` and `OrderBy`/`ThenBy` calls without dynamic runtime code emission (Native AOT safe).
- **Security & Abuse Limits:** Built-in safeguards against nested expression abuse, unbounded `IN` clauses, oversized filter payloads, and expensive multi-column `LIKE` operations.
- **ETag / Caching Support:** Built-in `XxHash3` deterministic query hashing (`QueryRequest.QueryHash`).

---

## Query Syntax Specification

### 1. Filtering

Filters use the bracket operator syntax: `field[operator]=value`.

```http
GET /api/products?price[gte]=100&category[in]=Electronics,Hardware&status[neq]=Archived
```

#### Supported Operators

| Operator | Syntax | Description | Example |
| :--- | :--- | :--- | :--- |
| `Equal` | `field=val` or `field[eq]=val` | Equality check | `status=Active` or `status[eq]=Active` |
| `NotEqual` | `field[neq]=val` | Inequality check | `status[neq]=Archived` |
| `GreaterThan` | `field[gt]=val` | Greater than | `price[gt]=50` |
| `GreaterThanOrEqual` | `field[gte]=val` | Greater than or equal | `price[gte]=50` |
| `LessThan` | `field[lt]=val` | Less than | `stock[lt]=10` |
| `LessThanOrEqual` | `field[lte]=val` | Less than or equal | `stock[lte]=10` |
| `Contains` | `field[contains]=val` | Substring match | `title[contains]=pro` |
| `NotContains` | `field[notContains]=val` | Substring exclusion | `title[notContains]=demo` |
| `StartsWith` | `field[startsWith]=val` | Prefix match | `sku[startsWith]=ABC` |
| `NotStartsWith` | `field[notStartsWith]=val` | Prefix exclusion | `sku[notStartsWith]=TEST` |
| `EndsWith` | `field[endsWith]=val` | Suffix match | `email[endsWith]=@corp.com` |
| `NotEndsWith` | `field[notEndsWith]=val` | Suffix exclusion | `email[notEndsWith]=@temp.com` |
| `In` | `field[in]=val1,val2` | In collection | `category[in]=Books,Games` |
| `NotIn` | `field[notIn]=val1,val2` | Not in collection | `role[notIn]=Guest,Banned` |
| `Between` | `field[between]=low..high` | Inclusive range | `price[between]=10..50` |
| `NotBetween` | `field[notBetween]=low..high` | Exclusive range | `age[notBetween]=18..65` |
| `IsNull` | `field[isNull]` | Field is null | `deletedAt[isNull]` |
| `IsNotNull` | `field[isNotNull]` | Field is not null | `verifiedAt[isNotNull]` |

### 2. Sorting

Sort expressions support multiple fields prefixed with `-` (descending) or optional `+` (ascending), separated by commas.

```http
GET /api/products?sort=-price,+createdAt,id
```

### 3. Free-Text Search

The `q` parameter performs multi-column substring searches on registered text selectors.

```http
GET /api/products?q=wireless+mouse
```

---

## Basic Usage

### 1. Define a Schema

Schemas declare which properties are exposed, allowed operators, custom aliases, default/required rules, and security limits.

```csharp
using Wiaoj.Querying;

public static class ProductQuerySchema
{
    public static readonly QuerySchema<Product> Instance = new QuerySchema<Product>()
        // Whitelist filtering
        .AllowFilter(x => x.Price, QueryOperator.GreaterThan, QueryOperator.LessThan, QueryOperator.Between)
        .AllowFilter(x => x.CategoryId, QueryOperator.Equal, QueryOperator.In)
        
        // Fluent per-property configuration
        .Property(x => x.Title)
            .HasName("name") // URL alias: ?name[contains]=phone
            .AllowFilter(QueryOperator.Contains, QueryOperator.StartsWith, QueryOperator.Equal)
            .AllowSort()
            
        // Sorting
        .AllowSort(x => x.CreatedAt)
        .AllowSort(x => x.Price)

        // Free-text search configuration (q=term)
        .SearchIn(x => x.Title, x => x.Description, x => x.Sku)

        // Mandatory invariants (cannot be bypassed by client)
        .RequireFilter(x => !x.IsDeleted)

        // Fallback filter if not explicitly passed by client
        .DefaultFilter(x => x.IsActive, x => x.IsActive == true)

        // Fallback sorting
        .DefaultSort(x => x.CreatedAt, SortDirection.Descending)

        // Abuse & security limits
        .ConfigureLimits(
            maxFilters: 10,
            maxInValues: 25,
            maxSortFields: 3,
            maxFilterValueLength: 256,
            maxSearchTermLength: 100
        );
}
```

### 2. Parse and Validate Request

```csharp
using Wiaoj.Querying;

string queryString = "?name[contains]=desk&price[between]=50..200&sort=-price";

// 1. Parse
if (QueryRequest.TryParse(queryString, out QueryRequest request))
{
    // 2. Validate against schema
    QueryValidationResult validation = ProductQuerySchema.Instance.Validate(request);
    
    if (!validation.IsValid)
    {
        // Convert to IDictionary<string, string[]> for ProblemDetails
        Dictionary<string, string[]> errors = validation.ToDictionary();
        return;
    }

    // 3. Apply to IQueryable<T> (EF Core, InMemory, LINQ to Objects)
    IQueryable<Product> query = dbContext.Products.ApplyQuery(request, ProductQuerySchema.Instance);
    
    List<Product> results = await query.ToListAsync();
}
```

---

## Architecture & Pipeline

When `ApplyQuery` is invoked:

1. **Required Filters:** Schema-level predicates (`RequireFilter`) are always injected first.
2. **Search (`q`):** Injected via `Expression.OrElse` across all registered `SearchIn` expressions.
3. **Client Filters:** Validated client conditions are combined via `Expression.AndAlso`.
4. **Default Filters:** If a field has a `DefaultFilter` and was not filtered by the client, its default predicate is appended.
5. **Sorting:** Client `sort` fields are applied; if empty, schema `DefaultSort` appliers are executed.

---

## Diagnostics & Validation Codes

`QueryValidationResult` produces strongly typed errors with `QueryValidationErrorCode`:

- `FieldNotFilterable`: Property is not permitted for filtering.
- `OperatorNotAllowed`: Operator is not registered in the property's allowed bitmask.
- `InvalidValueFormat`: Type conversion failed for property type.
- `MalformedRange`: Invalid boundary or missing separator for `between`/`notBetween`.
- `FieldNotSortable`: Sort field not configured.
- `MaxFilterCountExceeded`: Exceeded `MaxFilterCount`.
- `MaxInValuesCountExceeded`: Exceeded `MaxInValuesCount`.
- `MaxSortFieldsCountExceeded`: Exceeded `MaxSortFieldsCount`.
- `FilterValueTooLong`: Filter value length exceeded `MaxFilterValueLength`.
- `SearchTermTooLong`: Search term length exceeded `MaxSearchTermLength`.
