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
| **On `POST`:** a request body | one entry per media type the registered payload parsers declare; `application/json` as a typed schema (below). Not required, since an empty body falls back to the query string |
| **On `POST`:** `413` and `415` | the statuses the binder returns for a body; `415` documents its `Accept` header |
| **Where `QUERY` is accepted:** `Accept-Query` | on every response, listing the query media types |

### The JSON body

```json
{ "q": "widget", "sort": "-price", "filters": [ { "field": "price", "op": "gte", "value": 100 } ] }
```

`filters` is described as a `oneOf` with one alternative per filterable field. Each alternative's `op` is limited to the operators that field permits, and its `value` is typed the way the query parameter is. A generated client therefore can't pair a field with an operator it refuses. `op` is required for a field that does not permit equality, because omitting it means `eq`.

The schema's limits carry over:
- `filters` gets `maxItems`.
- `q` gets `maxLength`.
- A limit of `0` leaves the property out.

`QueryFilterStyle` does not apply to the body.

By default both the query parameters and the body are described. Set `RequestBodyDescription = QueryRequestBodyDescription.BodyOnly` to describe only the body.

### `QUERY` endpoints

ASP.NET Core generates OpenAPI 3.0 or 3.1 documents (through Microsoft.OpenApi 2.x). Neither version has a `query` operation, so the generator **leaves the `QUERY` method out of the document**. An endpoint mapped for `QUERY` alone does not appear, and one mapped for `QUERY` and `POST` appears as `post` only. The document signals `QUERY` support through the `Accept-Query` header on the operations it does describe.

Fields the endpoint ignores via `IgnoreQueryParameters(...)` — or that are ignored globally with `AddQuerying(q => q.IgnoreParameters(...))`, unless the schema or endpoint opts out of global rules — are left out, because the validator does not accept them. The rule is the same one the validation filter applies.

The field list, the sortable set and the operator list all come from `QuerySchema<T>.DescribeFields()` — the same rules the validator enforces. What is published and what is enforced have one source, so the document cannot describe a filter the endpoint would reject.

### It follows the application's configuration

- **Names.** Fields are published under the names the schema uses.
  - With `UseJsonNamingPolicy()` (or `UseFieldNamingPolicy`) that is `contentType` or `content_type`, matching the response bodies. The schema accepts those names, so the document never advertises one the server rejects.
  - **Without it, names are the CLR member names (`UsageCount`, `LastUsedAt`).** The query string then does not match camelCase bodies. Naming policy is opt-in because it renames parameters and regenerates clients; the old names keep working as aliases.
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

`QueryFilterStyle` has three values:

| Style | Generated client (TypeScript) | Needs |
| --- | --- | --- |
| **`Prose`** (default) | `usageCount?: number`. The operators are listed only in the description, so the client has no typed way to send `usageCount[gte]`. | Nothing |
| **`DeepObject`** | `usageCount?: { eq?: number; gte?: number; lte?: number }` | A query serializer that writes nested objects as brackets, such as `qs`. `URLSearchParams` writes `[object Object]`. |
| **`OperatorParameters`** | `usageCount?: number; "usageCount[gte]"?: number; "usageCount[lte]"?: number` | Nothing. Each operator is an ordinary query parameter. |

All three styles have these properties:

- Only the operators a field permits are described, and each is typed from the field. `in` is a comma-separated string. `isNull` and `isNotNull` accept only `true`, because presence is the condition and a `false` would still filter.
- In `OperatorParameters`, equality is described once, as the bare name. A client that sent both `usageCount` and `usageCount[eq]` would be filtering the same field with the same operator twice, which the validation filter refuses with a 400.

If your generator handles `deepObject` well, prefer it; otherwise use `OperatorParameters`.

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
