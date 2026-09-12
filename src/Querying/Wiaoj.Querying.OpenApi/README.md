# Wiaoj.Querying.OpenApi

Publishes the filter and sort surface a schema-validated endpoint actually accepts.

---

## Installation

```bash
dotnet add package Wiaoj.Querying.OpenApi
```

```csharp
builder.Services.AddOpenApi(options => options.AddWiaojQuerying());
```

Endpoints without `WithQueryValidation<T>()` are left untouched, so it is safe to add once for the entire document.

---

## The problem it solves

`QueryRequest` binds through `BindAsync`, and document generation treats a `BindAsync` parameter as opaque: it emits **no** parameters for it.

So an endpoint accepting a rich filter language over a fixed set of fields documents *nothing at all*. The reader of your API sees an endpoint with no inputs; the reality is `?Price[gte]=100&sort=-Name&q=widget`, and a query that steps outside the schema comes back 400.

---

## What it adds

For each endpoint marked with `WithQueryValidation<T>()`, resolved from that entity's registered `QuerySchema<T>`:

| | |
| --- | --- |
| One query parameter per **filterable** field | named as callers write it, with its permitted operators spelled out |
| `sort` | listing the **sortable** fields |
| `q` | free-text search |
| `400` response | with a note that the body is a `ProblemDetails` carrying the validation errors |

Fields the endpoint ignores via `IgnoreQueryParameters(...)` — or that are ignored globally with `AddQuerying(q => q.IgnoreParameters(...))`, unless the schema or endpoint opts out of global rules — are left out, because the validator does not accept them. The rule is the same one the validation filter applies.

The field list, the sortable set and the operator list all come from `QuerySchema<T>.DescribeFields()` — the same rules the validator enforces. What is published and what is enforced have one source, so the document cannot describe a filter the endpoint would reject.

### It follows the application's configuration

- **Names** — as the schema publishes them. With `UseJsonNamingPolicy()` (or `UseFieldNamingPolicy`) that is `contentType` / `content_type`, matching the bodies; the schema accepts those names, so the document never advertises one the server rejects.
- **Types** — from the field's CLR type: `bool` is `boolean`, `long` is `integer/int64`, dates are `date-time`, an enum lists the names the engine parses.
- **Operator tokens** — exactly what the parser reads (`isNull`, `notBetween`).
- **Descriptions** — from `.Describe("...")` on the schema.
- **Custom filters** declared with `CustomFilter<TValue>` are described like any field.

---

## Shaping the output

```csharp
builder.Services.AddOpenApi(options => options.AddWiaojQuerying(querying => {
    querying.FilterStyle = QueryFilterStyle.DeepObject;
    querying.ConfigureFilter = (field, parameter) => parameter.Deprecated = field.Name == "legacyCode";
    querying.ConfigureOperation = (operation, fields) => { /* anything else */ };
}));
```

**`QueryFilterStyle.DeepObject`** describes `field[op]=value` as OpenAPI's `deepObject`, so a generated client gets `ContentType?: { eq?: string; contains?: string }` and cannot name an operator the field refuses. Generator support varies — check yours before switching. The bare `field=value` shorthand keeps working on the wire but is not described. The default, `Prose`, types the parameter as the equality value and lists the operators in its description; every generator renders it.

The hooks receive the full field descriptors, so output can be extended with complete information instead of patched by a transformer that runs afterwards and depends on this one's output staying the same.

---

## Example

A schema like this:

```csharp
schema.Property(p => p.Name).AllowFilter(QueryOperator.Equal, QueryOperator.Contains).AllowSort();
schema.Property(p => p.Price).AllowFilter(QueryOperator.GreaterThanOrEqual, QueryOperator.LessThanOrEqual);
```

produces a `Name` parameter documented as accepting `eq, contains`, a `Price` parameter accepting `gte, lte`, and a `sort` parameter listing `Name` only.

---

## License

MIT
