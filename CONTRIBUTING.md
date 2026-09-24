# Contributing

Conventions every package in this repository follows. A pull request that changes a public API is checked against them.

## Dependency injection

### Registration shape

A package that has a builder offers **both** registration overloads on `IServiceCollection`:

```csharp
// Fluent: returns the builder, for one or two calls.
public static IXBuilder AddX(this IServiceCollection services);

// Block: configures the builder inside the callback and returns the service collection.
public static IServiceCollection AddX(this IServiceCollection services, Action<IXBuilder> configure);
```

```csharp
services.AddIdentifiers().UseAesCodec();

services.AddWebhooks(webhooks => {
    webhooks.AddPublishing(...);
    webhooks.UseInMemoryTransport();
});
```

- **The callback overload returns `IServiceCollection`, never the builder,** so `services.AddX(...).AddY(...)` keeps chaining on the collection. The fluent overload is the one that returns the builder.
- **The callback overload is written in terms of the fluent one:** `configure(services.AddX()); return services;`. All registration happens in `AddX()`, so both overloads register exactly the same services.
- **Calling `AddX` twice is safe.** Use `TryAdd*` and `TryAddEnumerable` for infrastructure. Return the existing builder when the package keeps one per collection, as `AddDdd()` does.
- **Nested builders follow the same shape:** `AddY(this IXBuilder)` returns `IYBuilder`, and `AddY(this IXBuilder, Action<IYBuilder>)` returns `IXBuilder`. See `AddPublishing` on `IWebhookBuilder`.
- **Options belong on the builder,** not in a lambda on `AddX`. Use `ConfigureKeyRotation(...)` on `ISecurityBuilder`, `Configure(...)` on `IQueryingBuilder`, or `services.Configure<TOptions>()`. A second `Action<TOptions>` overload of `AddX` would compete with `Action<IXBuilder>`: `AddX(_ => { })` then fails to compile.
- **A package without a builder** returns `IServiceCollection` and takes its options as an `Action<TOptions>`, like `AddRedisIdempotencyStore(Action<RedisIdempotencyOptions>?)`.

#### When the configuration must be complete

The fluent overload has no moment at which the configuration is known to be finished: calls can follow it at any time. Anything that depends on the whole configuration therefore happens **when the service is resolved**, not when `AddX` returns.

- **Serialization:** the default `ISerializer` is chosen on first resolution (`SerializationBuilder.ResolveDefault`).
- **BloomFilter:** the builder's options are copied inside a `Configure<BloomFilterOptions>` delegate, which runs when options are first read.

A callback overload may additionally validate on the registration line, because its configuration *is* complete when the callback returns. `AddIdentifiers(configure)` fails immediately when no codec was chosen, while `AddIdentifiers()` reports the same mistake when the host starts.

A package may offer **only** the callback overload when the complete configuration is needed while the service collection is still being built, so it cannot be deferred to resolution. It says why in the method's `<remarks>`. This applies today only to `AddModulith`: it instantiates the active modules and calls each module's `Register(services, …)` as soon as the callback returns.

### Builder types

- **`IXBuilder`** is a public interface in the package's root namespace, for example `Wiaoj.Resilience.IResilienceBuilder`. It holds `IServiceCollection Services { get; }` and only the primitive operations everything else is built on, for example `IIdentifiersBuilder.UseCodec(...)`.
- **`XBuilder`** is `internal sealed` and lives in `<Package>.DependencyInjection` or a namespace below it.
- **Conveniences are extension methods** in `XBuilderExtensions`, for example `UsePlainCodec` and `UseAesCodec` over `UseCodec`. An integration package extends the builder the same way (`UseKeyRingCodec`, `AddEntityFrameworkKeyStore`). It never needs access to the implementation.

### Namespaces

An extension method lives in the namespace of the type it extends:

| Extends | Namespace | Examples |
|---|---|---|
| `IServiceCollection` (`AddX`) | `Microsoft.Extensions.DependencyInjection` | `AddIdentifiers`, `AddWiaojResilience`, `AddInMemoryIdempotencyStore` |
| The package's builder (`IXBuilder`) | The builder's namespace | `IdentifiersBuilderExtensions` → `Wiaoj.Identifiers`, `ResilienceBuilderExtensions` → `Wiaoj.Resilience` |
| Another package's builder | That builder's namespace, even from a sibling package | `RedisBloomFilterBuilderExtensions` → `Wiaoj.BloomFilter`, `UseKeyRingCodec` → `Wiaoj.Identifiers` |
| A framework builder (`IHttpClientBuilder`, `IEndpointRouteBuilder`, `ModelBuilder`) | The framework's namespace | `AddOutboundNetworkPolicy` → `Microsoft.Extensions.DependencyInjection` |

Files in a framework namespace suppress the folder-mismatch analyzer:

```csharp
#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Microsoft.Extensions.DependencyInjection;
#pragma warning restore IDE0130 // Namespace does not match folder structure
```

## Not yet aligned

These predate the rules above. Align them when you next change their public API, and remove them from this list.

- `Wiaoj.Mediator`: `AddMediator` offers only the callback overload.
- `Wiaoj.Webhooks.AspNetCore`: `AddInbound(Action<WebhookInboundBuilder>? configure = null)` is a nested builder that takes an optional callback, and `WebhookInboundBuilder` is a class, not an interface. `WebhookBuilderInboundExtensions` is in `Microsoft.Extensions.DependencyInjection` rather than `Wiaoj.Webhooks`.
