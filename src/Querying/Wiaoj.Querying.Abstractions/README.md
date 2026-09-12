# Wiaoj.Querying.Abstractions

The query language on its own: `QueryRequest`, its filter and sort nodes, the bracket and JSON parsers, and the validation result types — with no dependency injection, options or expression engine.

Reference this from **contract** assemblies. A request or message that carries a query does not need the machinery that applies one:

```csharp
// Verba.Lifecycle.Contracts
using Wiaoj.Querying;

public sealed record GetBatchTranslationsRpcRequest(QueryRequest Query);
```

The service that answers it references `Wiaoj.Querying`, which brings schemas, `ApplyQuery` and registration, and depends on this package.

Types keep the `Wiaoj.Querying` namespace. Moving an existing reference from `Wiaoj.Querying` to this package changes no `using` directives.

## License

MIT
