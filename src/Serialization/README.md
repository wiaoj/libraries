# Wiaoj.Serialization

One serializer abstraction for .NET, with several formats and behaviours side by side in one application. Each configuration is registered under a **key type**: the API writes camel-case JSON, the cache writes compressed MessagePack, secrets are encrypted with a Wiaoj.Security key ring, and code asks for `ISerializer<CacheKey>` without knowing which library is behind it.

## Packages

| Package | What it adds |
|---|---|
| `Wiaoj.Serialization.Abstractions` | `ISerializer`, `ISerializer<TKey>`, `ISerializerKey`, the exception types |
| `Wiaoj.Serialization.DependencyInjection` | `AddWiaojSerializer`, `ISerializationBuilder`, the default `ISerializer` |
| `Wiaoj.Serialization.SystemTextJson` | `UseSystemTextJson`: JSON, including `IAsyncEnumerable` streaming |
| `Wiaoj.Serialization.MessagePack` | `UseMessagePack`: compact binary, readable from other languages |
| `Wiaoj.Serialization.MemoryPack` | `UseMemoryPack`: compact binary between .NET applications ([limits](#memorypack)) |
| `Wiaoj.Serialization.Bson` | `UseBson`: MongoDB BSON documents |
| `Wiaoj.Serialization.YamlDotNet` | `UseYamlDotNet`: YAML |
| `Wiaoj.Serialization.Compression` | `WithGzipCompression`, `WithBrotliCompression` |
| `Wiaoj.Serialization.Security` | `WithEncryption<TKey, TContext>`: AES-GCM with a Wiaoj.Security key ring |
| `Wiaoj.Serialization.Transcoding` | `ITranscoder`: convert bytes from one registered format to another |

```bash
dotnet add package Wiaoj.Serialization.DependencyInjection
dotnet add package Wiaoj.Serialization.SystemTextJson   # and any other format you need
```

**Namespaces:**
- `AddWiaojSerializer` is in `Microsoft.Extensions.DependencyInjection`.
- The contracts and every registration method (`Use…`, `TryUse…`, `With…`, `AddTranscoding`) are in `Wiaoj.Serialization`.
- Each package's own types are in its namespace, for example `Wiaoj.Serialization.Security.DecryptionFailedException` and `Wiaoj.Serialization.Transcoding.ITranscoder`.

## Quick start

### 1. Define keys

A key is a marker type that names one configuration.

```csharp
using Wiaoj.Serialization;

public readonly struct ApiKey : ISerializerKey;
public readonly struct CacheKey : ISerializerKey;
```

### 2. Register

```csharp
builder.Services.AddWiaojSerializer(serializers => {
    // Keyless: resolved as the non-keyed ISerializer
    serializers.UseSystemTextJson(new JsonSerializerOptions(JsonSerializerDefaults.Web));

    serializers.UseSystemTextJson<ApiKey>();

    serializers.UseMessagePack<CacheKey>()
        .WithBrotliCompression(CompressionLevel.Fastest);
});
```

`AddWiaojSerializer()` without a callback returns the builder instead, for one or two calls.

### 3. Use

```csharp
public sealed class ProductCache(ISerializer<CacheKey> serializer, IDistributedCache cache) {
    public Task SetAsync(Product product, CancellationToken ct) =>
        cache.SetAsync($"product:{product.Id}", serializer.Serialize(product), ct);
}
```

**The non-keyed `ISerializer`** is the keyless serializer when one is registered, otherwise the only registered serializer. With several keyed serializers and no keyless one there is none: `GetService<ISerializer>()` returns `null`.

**Registering the same key twice throws.**
- `TryUse…` (every format) registers only when the key is free.
- `Replace…` (SystemTextJson and MemoryPack) swaps an existing registration.

## Behaviours

`With…` methods wrap the serializer in a decorator, in the order they are called. Compressing and then encrypting runs as follows:

```
value → format serializer → compression → encryption → bytes
```

Compress before you encrypt: ciphertext does not compress.

### Compression

```csharp
serializers.UseSystemTextJson<LogKey>().WithGzipCompression();
serializers.UseMessagePack<CacheKey>().WithBrotliCompression(CompressionLevel.Fastest);
```

Data that does not decompress throws `DeserializationFormatException`.

### Encryption with a Wiaoj.Security key ring

Encrypts everything a serializer writes with the `ISecretProtector<TContext>` of a Wiaoj.Security context. That gives authenticated AES-GCM, keys held in your key store under a master key, and rotation. Data written under an older key stays readable while the key ring still holds that key.

```csharp
public readonly struct SecureDataKey : ISerializerKey;
public sealed class SecureDataContext : ISecretContext;

builder.Services.AddWiaojSecurity()
    .AddEnvironmentMasterKey()
    .AddEntityFrameworkKeyStore<AppDbContext>()
    .AddManagedProtector<SecureDataContext>();

builder.Services.AddWiaojSerializer(serializers => serializers
    .UseSystemTextJson<SecureDataKey>()
    .WithEncryption<SecureDataKey, SecureDataContext>());
```

- **Bytes** are the key version (4 bytes, big-endian) followed by the ciphertext. **Text** is the `EncryptedSecret` compact form, `v{version}.{blob}`.
- **Anything that cannot be decrypted throws `DecryptionFailedException`:** data that was tampered with or truncated, written under another context, or written under a key the ring no longer holds.
- **Encryption is not deterministic.** The same value gives different bytes each time, so encrypted output cannot be used as a cache key or compared for equality.

### Transcoding

Convert bytes from one registered format to another, for example to re-encode cached JSON as MessagePack:

```csharp
serializers.AddTranscoding().UseSystemTextJson<JsonKey>();
serializers.UseMessagePack<PackKey>();

byte[] pack = transcoder.From<JsonKey>(jsonBytes).To<PackKey, Order>();
```

### Streaming a JSON array

`SystemTextJsonSerializer` also implements `IAsyncEnumerableSerializer`, which reads the items of a JSON array one at a time:

```csharp
if(serializer is IAsyncEnumerableSerializer streaming) {
    await foreach(Product? product in streaming.DeserializeAsyncEnumerable<Product>(stream, ct)) {
        // ...
    }
}
```

The other formats, and a JSON serializer wrapped in compression or encryption, do not implement it.

## Formats

### YAML and BSON

```csharp
serializers.UseYamlDotNet<ConfigKey>(yaml => yaml.WithNamingConvention(CamelCaseNamingConvention.Instance));
serializers.UseBson<MongoKey>();
```

BSON needs a representation configured before it can write a `Guid`, and it writes a `DateTimeOffset` as an array. If your types use either, configure MongoDB's serializers for them.

### MemoryPack

`UseMemoryPack<TKey>()` writes a compact binary format with very little work per value, but it only suits data exchanged between .NET applications:

- Every type it serializes must be `[MemoryPackable]` and `partial` (a source generator writes the serializer).
- By default, members are read by position. Adding, removing or reordering a member makes data written earlier unreadable. For anything that outlives a deployment, such as outbox rows, cache entries or messages in a queue, declare the type `[MemoryPackable(GenerateType.VersionTolerant)]` and number its members with `[MemoryPackOrder]`, or use JSON.
- There are no readers outside .NET. Use MessagePack when another language reads the data.

## Guarantees

Every format passes the same contract suite (`tests/Serialization/Wiaoj.Serialization.Tests.Unit`). So do JSON behind compression and JSON and MessagePack behind encryption. The suite checks:

- round trips through `byte[]`, `string`, `IBufferWriter<byte>` and single- and multi-segment `ReadOnlySequence<byte>`, generic and non-generic
- round trips through `Stream`
- malformed input throws a `WiaojSerializationException`, and the `Try…` methods return `false` for it

To cover a new format or behaviour, derive one test class from `SerializerContractTests`.

## Contributing

See [CONTRIBUTING.md](../../CONTRIBUTING.md). Licensed under MIT; see [LICENSE](../../LICENSE).
