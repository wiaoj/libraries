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

Fields the endpoint ignores via `IgnoreQueryParameters(...)` are left out, because the endpoint does not accept them.

The field list, the sortable set and the operator list all come from `QuerySchema<T>.DescribeFields()` — the same rules the validator enforces. What is published and what is enforced have one source, so the document cannot describe a filter the endpoint would reject.

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
