# Wiaoj.Serialization

[![NuGet](https://img.shields.io/badge/nuget-v1.0.0-blue.svg)](https://www.nuget.org/packages/Wiaoj.Serialization)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![.NET](https://img.shields.io/badge/.NET-10.0-purple)](https://dotnet.microsoft.com/download)

**Wiaoj.Serialization** is a high-performance, unified abstraction layer for serialization in .NET. It decouples your application from specific serialization libraries, allowing you to manage multiple formats (JSON, MessagePack, BSON, YAML) and behaviors (Compression, Encryption) within a single project using a type-safe, key-based architecture.

## 🚀 Mission & Vision

Modern applications often require different serialization strategies for different contexts:
*   **Public APIs** need standard, readable JSON.
*   **Distributed Caches** need compact, fast binary formats (like MessagePack).
*   **Databases** might need BSON or specific JSON configurations.
*   **Secure Logs** need encryption and PII masking.

**Wiaoj.Serialization** solves this by treating serialization as a configurable pipeline. It provides a "Zero-Overhead" abstraction that focuses on streaming and asynchronous operations.

## ✨ Key Features

*   **Polyglot Serialization:** Use System.Text.Json, MessagePack, BSON, and YAML side-by-side.
*   **Context-Aware Registry:** Register different configurations for different purposes using `ISerializerKey` (e.g., `ICacheSerializer`, `IApiSerializer`).
*   **Pipeline Decorators:** Easily chain behaviors like **Gzip/Brotli Compression** and **AES-GCM Encryption** without changing your business logic.
*   **Async-First:** Built from the ground up for `Stream` and `IAsyncEnumerable` efficiency.
*   **Dependency Injection Friendly:** seamless integration with `Microsoft.Extensions.DependencyInjection`.

---

## 📦 Installation

Install the core package and the providers you need:

```bash
# Core Abstractions
dotnet add package Wiaoj.Serialization

# Providers (Choose what you need)
dotnet add package Wiaoj.Serialization.SystemTextJson
dotnet add package Wiaoj.Serialization.MessagePack
dotnet add package Wiaoj.Serialization.Bson
dotnet add package Wiaoj.Serialization.YamlDotNet
```

---

## 🏁 Quick Start

### 1. Define Your Keys (Optional but Recommended)
Instead of relying on a single global serializer, define "keys" to represent the *context* of serialization.

```csharp
using Wiaoj.Serialization.Abstractions;

// Marker interface for API responses
public struct ApiKey : ISerializerKey;

// Marker interface for Redis/Caching
public struct CacheKey : ISerializerKey;
```

### 2. Configure Services
In your `Program.cs`, register the serializers and configure their pipelines.

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddWiaojSerializer(config =>
{
    // 1. Default (Keyless) Serializer -> System.Text.Json
    config.UseSystemTextJson(options => 
    {
        options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.WriteIndented = true;
    });

    // 2. Specific "Api" Serializer -> System.Text.Json (Optimized)
    config.UseSystemTextJson<ApiKey>();

    // 3. Specific "Cache" Serializer -> MessagePack + Brotli Compression
    config.UseMessagePack<CacheKey>()
          .WithBrotliCompression(System.IO.Compression.CompressionLevel.Fastest);
});
```

### 3. Inject and Use
Inject `ISerializer<TKey>` where needed. The correct implementation (with all compression/encryption logic applied) will be injected.

```csharp
[ApiController]
[Route("products")]
public class ProductController : ControllerBase
{
    private readonly ISerializer<ApiKey> _apiSerializer;
    private readonly ISerializer<CacheKey> _cacheSerializer;

    public ProductController(ISerializer<ApiKey> apiSerializer, ISerializer<CacheKey> cacheSerializer)
    {
        _apiSerializer = apiSerializer;
        _cacheSerializer = cacheSerializer;
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var product = new { Id = 1, Name = "Laptop" };

        // Use the Cache Serializer (Binary + Compressed)
        byte[] cachedData = _cacheSerializer.Serialize(product);
        await _distributedCache.SetAsync("product:1", cachedData);

        // Use the API Serializer (JSON)
        // You can write directly to the HTTP Response Body Stream for max performance
        Response.ContentType = "application/json";
        await _apiSerializer.SerializeAsync(Response.Body, product);
        
        return Empty; 
    }
}
```

---

## 🛠 Advanced Scenarios

### 🔐 Authenticated Encryption (AES-GCM)
Secure your sensitive data at rest or in transit transparently. The serializer handles encryption during serialization and decryption during deserialization.

```csharp
// Define a key for secure data
public struct SecureDataKey : ISerializerKey;

// Generate a secure key (store this safely in KeyVault/Env Vars!)
var secretKey = Secret.From(Base64String.Parse("...your-32-byte-base64-key..."));

builder.Services.AddWiaojSerializer(config =>
{
    config.UseSystemTextJson<SecureDataKey>()
          .WithAesGcmEncryption(secretKey);
});

// Usage
// _secureSerializer.Serialize(data) -> Returns Encrypted JSON bytes
// _secureSerializer.Deserialize(bytes) -> Decrypts and returns Object
```

### 📄 YAML and BSON Support
Easily handle configuration files or MongoDB documents.

```csharp
// YAML for Config Export
config.UseYamlDotNet<ConfigKey>(serializer => 
{
    serializer.WithNamingConvention(CamelCaseNamingConvention.Instance);
});

// BSON for Binary Documents
config.UseBson<MongoKey>();
```

### ⚡ Async Enumerable Streaming
Efficiently process large datasets without loading everything into memory.

```csharp
public async Task ProcessLargeData(ISerializer serializer, Stream stream)
{
    // Reads items one by one from the stream (JSON Array)
    IAsyncEnumerable<Product> products = serializer.DeserializeAsyncEnumerable<Product>(stream);

    await foreach (var product in products)
    {
        // Process item immediately
    }
}
```

---

## 🧩 Architecture

### The `ISerializerKey` Pattern
Traditional abstraction libraries often force a single global configuration (`IGlobalSerializer`). Wiaoj.Serialization uses **Marker Types** (`TKey`) to differentiate configurations in the Dependency Injection container.

*   `ISerializer<KeylessRegistration>`: The default serializer.
*   `ISerializer<TKey>`: A specific named serializer (e.g., `CacheKey`, `LogKey`).

### The Pipeline (Decorators)
Features like Compression and Encryption are implemented as **Decorators**. When you call `.WithGzipCompression()`, the library wraps your chosen serializer (e.g., Json) in a `CompressionSerializerDecorator`.

Flow:
`Input Object` -> **Compressor** -> **Encryptor** -> **JsonSerializer** -> `Stream/Bytes`

---

## ❓ FAQ

**Q: Can I use this with Newtonsoft.Json?**
A: Support is planned. Currently, we support System.Text.Json, MessagePack, YamlDotNet, and MongoDB.Bson.

**Q: What is the performance overhead?**
A: The abstraction layer is extremely thin. It is designed to delegate directly to the underlying library's `Stream` methods. When using Source Generators (planned for v2), the overhead will be effectively zero.

**Q: How do I handle Key collisions?**
A: The builder throws an exception if you register the same Key twice to prevent accidental misconfiguration. Use `TryAddSerializer` or `ReplaceSerializer` (if available in extensions) for conditional logic.

---

## 🤝 Contributing

We welcome contributions! Please follow these steps:

1.  Fork the repository.
2.  Create a feature branch (`git checkout -b feature/amazing-feature`).
3.  Commit your changes.
4.  Open a Pull Request.

## 📄 License

This project is licensed under the **MIT License**. See the [LICENSE](LICENSE) file for details.