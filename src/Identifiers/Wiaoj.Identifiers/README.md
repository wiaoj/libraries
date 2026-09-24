# Wiaoj.Identifiers

Strongly typed, prefixed identifiers over `SnowflakeId`, written publicly as `usr_…`, either **plain** or **AES-encrypted**.

```csharp
[Identifier("usr")]
public readonly partial record struct UserId;

UserId id = UserId.New();
string text = id.ToString();       // "usr_11bNq7n40PwSW4duiTdFuw6" (AES) or "usr_1bKz3eQ9pX" (plain)
UserId back = UserId.Parse(text);
```

A `UserId` can't be passed where an `OrgId` is expected, and neither can its text: `OrgId.TryParse(userText)` returns `false`.

## Installation

The family is split so a domain layer doesn't take on hosting dependencies:

| Package | Contains | Reference it from |
| --- | --- | --- |
| [`Wiaoj.Identifiers.Abstractions`](../Wiaoj.Identifiers.Abstractions/README.md) | `[Identifier]`, `IIdentifier<T>`, `IdCodec`, `PlainIdCodec`, `AesIdCodec`, and the source generator. Depends only on `Wiaoj.Primitives`. | The domain project that declares identifiers |
| `Wiaoj.Identifiers` (this package) | `AddIdentifiers()`, `IdentifiersOptions`, and the startup codec installer. Adds `Microsoft.Extensions.DependencyInjection`, `Hosting` and `Options`. | The host / composition root |

```bash
dotnet add Domain package Wiaoj.Identifiers.Abstractions
dotnet add Api package Wiaoj.Identifiers
```

- **Namespace:** every type is in the `Wiaoj.Identifiers` namespace, including the codec choices (`UseAesCodec`, `UsePlainCodec`, `UseKeyRingCodec`). Only `AddIdentifiers` itself is in `Microsoft.Extensions.DependencyInjection`, like every `Add…` method.
- **Generator:** it comes with every package in the family. A single-project application can reference only `Wiaoj.Identifiers`.

## Setup

Choose a codec. It is installed as `IdCodec.Current` when the host starts:

```csharp
using Wiaoj.Identifiers;

// Encrypted: the text reveals nothing and forged identifiers are refused
builder.Services.AddIdentifiers().UseAesCodec();
builder.Services.Configure<IdentifiersOptions>(builder.Configuration.GetSection("Identifiers"));
```

```json
"Identifiers": { "AesKey": "<base64 of 32 random bytes, from a secret store>" }
```

```csharp
// Plain: short and readable, but reveals when each identifier was created
builder.Services.AddIdentifiers().UsePlainCodec();
```

**There is no default.** Starting without a codec, or with a missing, invalid or shorter than 16-byte key, fails at startup. To generate a key, use `Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))`.

## The two codecs

| | `PlainIdCodec` | `AesIdCodec` |
| --- | --- | --- |
| Text | `usr_` + base62 of the value (up to 11 characters) | `usr_` + key version + 22 base62 characters |
| Needs a key | No | Yes |
| Reveals creation time and volume | **Yes**: a Snowflake contains its timestamp | No |
| Refuses forged or altered text | No: any valid base62 is some identifier | Yes, except with probability 2⁻⁶⁴ |
| Refuses a `usr_` identifier re-prefixed as `org_` | Prefix is checked, but the value is still read | Yes |
| Spellings per identifier | Exactly one | Exactly one |

The plain codec is not an obfuscation. Use it where revealing creation time is acceptable, for example between internal services.

### How the AES codec works

The value is encrypted as a single AES-128 block laid out as `[first 8 bytes of HMAC-SHA256(tag key, prefix)][value, big-endian]`:

- **Keys:** the AES and HMAC keys are derived from the configured key with HKDF-SHA256, so the configured key itself is never used directly.
- **Decoding:** the codec decrypts the block and compares the tag in constant time. A made-up identifier, one written under another key, or one moved to another prefix fails this check and is refused.
- **Deterministic:** the same identifier always gives the same text.

**Changing the key changes every identifier's text.** The version character (`AesKeyVersion`, default `1`) marks which key wrote an identifier. To rotate the key without breaking issued identifiers, use [Wiaoj.Identifiers.Security](../Wiaoj.Identifiers.Security/README.md): it keys identifiers by a Wiaoj.Security key ring, writes with the current version and reads every version still in the ring.

## Where identifiers work without injecting anything

Generated identifiers use `IdCodec.Current`, so these all work without injection:

- `ToString()` and string interpolation.
- `Parse` / `TryParse` for strings, spans and UTF-8, through `IParsable<T>`, `ISpanParsable<T>` and `IUtf8SpanParsable<T>`. This means minimal API route and query binding work directly (`/users/{id}` with `UserId id`).
- `TryFormat` into `char` and UTF-8 buffers.
- System.Text.Json, as a value and as a dictionary key. The converter is reflection-free, so it works under Native AOT.
- `TypeConverter`, for configuration binding.
- Comparison by value (creation order) and `IsEmpty`.

**Rules for the codec:**
- **Install once:** the codec is installed once per process. Installing a different codec afterwards throws, because identifiers already written would no longer be readable. Installing an equivalent one (the same key and version) is allowed, so several hosts can start in one process, as in integration tests.
- **Before installation:** using an identifier before a codec is installed throws a clear `InvalidOperationException` instead of silently falling back to a default key.
- **Without a host:** call `serviceProvider.UseIdentifiers()`.
- **In tests:** override the codec for the current async flow. Parallel tests with different codecs don't affect each other:

  ```csharp
  using(IdCodec.Override(PlainIdCodec.Instance)) { ... }
  ```

- **Explicit use:** a codec can also be used directly, for tools that handle several keys: `codec.Encode(id)`, `codec.TryDecode<UserId>(text, out UserId id)`.

## Declaring identifiers

```csharp
[Identifier("api_key")]
internal readonly partial record struct ApiKeyId;
```

The generator reports these as compile errors:

| Rule | Id |
| --- | --- |
| Prefix: 1–32 lowercase ASCII letters and digits, starting with a letter, with single underscores between parts | `WIAOJID001` |
| Declared as `readonly partial record struct` with no parameter list | `WIAOJID002` |
| Each prefix used by only one identifier | `WIAOJID003` |
| Not nested, not generic | `WIAOJID004` |

The database stores `Value`, the `SnowflakeId`, never the codec text. For Entity Framework Core, [Wiaoj.Identifiers.EntityFrameworkCore](../Wiaoj.Identifiers.EntityFrameworkCore/README.md) maps identifiers to `bigint` columns and generates keys.

## Replacing `OpaqueId`

`Wiaoj.Primitives.OpaqueId` and its obfuscators were removed: they looked like encryption but weren't. To migrate:

1. **Declare a type:** create an `[Identifier("…")]` type for each kind of identifier.
2. **Register a codec:** call `AddIdentifiers().UseAesCodec()` with a new random key.
3. **Plan for the text change:** existing public identifier strings change, because the construction and the key are different. Store and compare `Value`, never the text.
