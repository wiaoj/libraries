# Wiaoj.Identifiers.Abstractions

The types and the source generator of [Wiaoj.Identifiers](../Wiaoj.Identifiers/README.md), with no `Microsoft.Extensions.*` dependency. This is the package a **domain layer** references.

```csharp
[Identifier("usr")]
public readonly partial record struct UserId;

UserId id = UserId.New();
```

## Installation

```bash
dotnet add package Wiaoj.Identifiers.Abstractions
```

The source generator is included, so declaring identifiers needs nothing else.

## What is here

- **Declaring:** `[Identifier("…")]`, `IIdentifier<TSelf>`, and the source generator with its diagnostics `WIAOJID001`–`WIAOJID004`.
- **Codecs:** `IdCodec` (`Current`, `Install`, `Override`), `PlainIdCodec` and `AesIdCodec`.

**Dependencies:** `Wiaoj.Primitives` (for `SnowflakeId`) and `Wiaoj.Preconditions`.

The types are in the `Wiaoj.Identifiers` namespace, the same as the rest of the family, so moving code between layers doesn't change any `using`.

## Which package where

| Project | Package |
| --- | --- |
| Domain: declares `UserId`, `OrderId`, … | `Wiaoj.Identifiers.Abstractions` |
| Host / composition root: `AddIdentifiers().UseAesCodec()` | [`Wiaoj.Identifiers`](../Wiaoj.Identifiers/README.md) |
| Persistence with EF Core | [`Wiaoj.Identifiers.EntityFrameworkCore`](../Wiaoj.Identifiers.EntityFrameworkCore/README.md) |
| Keys from a Wiaoj.Security key ring | [`Wiaoj.Identifiers.Security`](../Wiaoj.Identifiers.Security/README.md) |

The other packages depend on this one and carry the generator along, so a single-project application can reference only `Wiaoj.Identifiers`.

**Without a host:** a codec can be installed without any DI, for tools and tests: `IdCodec.Install(PlainIdCodec.Instance)` or `IdCodec.Install(new AesIdCodec(key))`.

How identifiers, codecs and the generator behave is described in the [Wiaoj.Identifiers README](../Wiaoj.Identifiers/README.md).
