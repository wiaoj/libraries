# Wiaoj.Identifiers.Security

Encrypts [Wiaoj.Identifiers](../Wiaoj.Identifiers/README.md) under a [Wiaoj.Security](../../Extensions/Security/README.md) key ring, so identifier keys are stored, wrapped by the master key and rotated like the rest of your secrets.

## Installation

```bash
dotnet add package Wiaoj.Identifiers.Security
```

## Usage

```csharp
public sealed class IdentifierContext : ISecretContext;

builder.Services.AddWiaojSecurity()
    .AddEnvironmentMasterKey()
    .AddEntityFrameworkKeyStore<AppDbContext>()
    .AddManagedProtector<IdentifierContext>();

builder.Services.AddIdentifiers().UseKeyRingCodec<IdentifierContext>();
```

Generated identifiers then work as usual (`id.ToString()`, `UserId.Parse(text)`, JSON, binding), with their text written as `usr_` + key version + 22 characters.

## Compared with `UseAesCodec()`

| | `UseAesCodec()` | `UseKeyRingCodec<TContext>()` |
| --- | --- | --- |
| Key | One base64 key in configuration | The domain's key ring, stored and wrapped by the master key |
| Rotation | Changing the key breaks every issued identifier | New identifiers use the new version, and earlier ones still parse |
| Construction | AES-128 block with an HMAC tag | The same, keyed per version by a subkey |

## How it works

- **Per-version key:** each key version gets an `AesIdCodec`, keyed by a 32-byte subkey derived with `ISubkeyDeriver<TContext>.DeriveSubkey(version, "wiaoj.identifiers", …)`. The ring's keys are never exposed. The subkey is derived once per version and cached.
- **Writing:** uses `CurrentKeyVersion`.
- **Reading:** looks at the identifier's version character and decodes with every version in `KeyVersions` that has that character. The tag check refuses a version that didn't write the identifier.
- **Version character:** the base62 digit of the version modulo 62: `1`…`9`, `A`…`Z`, `a`…`z`, then `0`. After version 61 the characters repeat; versions sharing a character are both tried, so reading still works.

## Rotation

`AddManagedProtector<TContext>()` reloads the key ring on rotation, and the codec follows the reload. No restart is needed.

```csharp
await keyRotationService.ForceRotateAsync();   // KeyRotationService<IdentifierContext>

id.ToString();              // now "usr_2…"
UserId.Parse(oldText);      // an identifier written as "usr_1…" still parses
```

**Keep retired versions in the ring for as long as their identifiers are in circulation**: in URLs, in other systems' databases, in emails. An identifier whose version is removed from the ring can no longer be read.

This is different from secrets encrypted with `ISecretProtector`, which `IDataRotator` re-encrypts: identifiers are held by clients, so they can't be rewritten. What you store is `Value`, the Snowflake (see [Wiaoj.Identifiers.EntityFrameworkCore](../Wiaoj.Identifiers.EntityFrameworkCore/README.md)), so rotation never touches your database.

## Startup

Using `UseKeyRingCodec<TContext>()` without `AddManagedProtector<TContext>()` fails when the host starts, because there is no `ISubkeyDeriver<TContext>`. Keys are loaded lazily, on the first identifier written or read, and `AddManagedProtector` pre-warms them while the host starts.
