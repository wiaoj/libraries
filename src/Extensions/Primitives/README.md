Here is a highly detailed, professional, and strictly technical `README.md`. All emojis have been removed, the tone is purely engineering-focused, and all experimental buffer structures have been excluded. It explains the "why" and "how" for each feature in depth.

---

# Wiaoj.Primitives

**Wiaoj.Primitives** is a high-performance, security-focused .NET foundation library engineered to eliminate **Primitive Obsession** in domain-driven applications. 

In standard .NET development, developers frequently rely on generic primitives (`string`, `byte[]`, `double`, `long`) to represent complex domain concepts like cryptographic keys, identifiers, semantic versions, and encoded payloads. This leads to defensive coding, memory leaks in the managed heap, and severe performance bottlenecks.

This library replaces these generic types with self-validating, strongly-typed value objects. Designed for modern .NET, it heavily utilizes `ref struct`, `Span<T>`, SIMD vectorization (`SearchValues<T>`), fixed-size buffers, and unmanaged memory manipulation to deliver zero-allocation performance and cryptographic-grade security.

## Installation & Requirements

```bash
dotnet add package Wiaoj.Primitives
```

## 1. Secure Memory Management (`Secret<T>`)

Standard .NET types like `string` and `byte[]` are managed by the Garbage Collector (GC). They are immutable, meaning any modification creates a copy, and they linger in memory until a GC cycle collects them. This makes them highly vulnerable to memory dumps and timing attacks.

`Secret<T>` solves this by allocating memory outside the managed heap using `NativeMemory.AllocZeroed`. The memory is pinned, inaccessible to the GC, and guaranteed to be cryptographically wiped (`CryptographicOperations.ZeroMemory`) when disposed.

```csharp
using Wiaoj.Primitives;

// 1. Generate a cryptographically strong 256-bit AES key directly into secure memory
using Secret<byte> masterKey = Secret.Factory.Aes256Key();

// 2. Parse a secret from an external source (intermediate strings are wiped)
using Secret<byte> apiKey = Secret.From("super-secret-key-123");

// 3. Controlled Access (The Expose Pattern)
// Data cannot escape this scope. It prevents accidental logging or leaking.
apiKey.Expose(span => 
{
    // 'span' is a ReadOnlySpan<byte> pointing directly to the unmanaged memory.
    // It is safe to pass this to cryptographic algorithms.
    Console.WriteLine($"Key length: {span.Length}");
});

// 4. Secure Key Derivation (HKDF-SHA256)
using Secret<byte> derivedKey = masterKey.DeriveKey(salt: apiKey, outputByteCount: 64);
```

---

## 2. Distributed Identity & Obfuscation

Relying on standard auto-incrementing `int` or random `Guid` (v4) for primary keys leads to database B-Tree index fragmentation and leaks business intelligence (e.g., how many orders you process daily).

### SnowflakeId & GuidV7 (Time-Ordered IDs)
`SnowflakeId` is a lock-free, thread-safe implementation of the Twitter Snowflake algorithm. It generates 64-bit, k-sorted identifiers that eliminate database fragmentation without requiring a central coordination server. `GuidV7` provides the 128-bit RFC 9562 equivalent.

```csharp
using Wiaoj.Primitives.Snowflake;

// Configure the node identity at application startup
SnowflakeId.Configure(nodeId: 1);

// Generate a 64-bit ID (Zero allocation)
SnowflakeId internalId = SnowflakeId.NewId();
UnixTimestamp creationTime = internalId.ToUnixTimestamp();
```

### OpaqueId (Secure ID Obfuscation)
You should never expose internal sequential database IDs to the public. `OpaqueId` wraps a `SnowflakeId` or `long` and scrambles it using a Format-Preserving Encryption approach (e.g., Feistel Cipher). It generates short, YouTube-like URL-safe strings while remaining an integer under the hood.

```csharp
using Wiaoj.Primitives.Obfuscation;

// Configure the global obfuscation strategy using a secret seed
var options = new FeistelObfuscatorOptions { Seed = mySecretSeed };
OpaqueId.Configure(new FeistelBase62Obfuscator(options));

// Create an opaque representation of the internal ID
OpaqueId publicId = new OpaqueId(internalId);

// Expose to API responses or URLs
Console.WriteLine(publicId.ToString()); // Outputs a stable string like "7xk9A2"

// Parse back from incoming HTTP requests directly to the internal ID
OpaqueId parsedId = OpaqueId.Parse("7xk9A2");
SnowflakeId originalId = parsedId.AsSnowflake();
```

### NanoId
For completely random, highly collision-resistant public identifiers.
```csharp
// Generates a 21-character URL-safe string. 
// Uses a default profanity-safe alphabet (vowels removed).
NanoId videoId = NanoId.NewId();
```

---

## 3. Allocation-Free Cryptographic Hashing

Traditional hashing APIs return a `byte[]`, forcing a heap allocation on every single hash computation. 

Wiaoj.Primitives provides `Sha256Hash`, `Sha512Hash`, `Md5Hash`, and `Hmac` variants as **fixed-size struct wrappers** containing inline arrays (e.g., `fixed byte[32]`). They live entirely on the stack. Furthermore, their `Equals` operators use `CryptographicOperations.FixedTimeEquals` to prevent timing attacks.

```csharp
using Wiaoj.Primitives.Cryptography.Hashing;

// Compute hash on the stack (Zero heap allocations)
Sha256Hash documentHash = Sha256Hash.Compute("document content");

// Constant-time equality comparison
if (documentHash == expectedHash) {
    // Valid
}

// Memory-efficient async stream hashing (e.g., large file uploads)
using var stream = File.OpenRead("large_data.bin");
Sha256Hash streamHash = await Sha256HashExtensions.ComputeAsync(stream);
```

---

## 4. Strongly-Typed Encodings

A method requiring a Base64 payload should not accept a standard `string`. By using specialized types, the format is validated instantly upon instantiation using `.NET 8 SearchValues<T>` for SIMD-accelerated character scanning.

Available types: `Base64String`, `Base64UrlString`, `Base32String`, `Base62String`, `HexString`.

```csharp
// Validates format instantly. Throws FormatException if invalid.
Base64String payload = Base64String.Parse("SGVsbG8gV29ybGQ=");
HexString signature = HexString.Parse("48656C6C6F");

// Decode directly to stack memory without allocating intermediate byte arrays
Span<byte> buffer = stackalloc byte[64];
if (signature.TryDecode(buffer, out int bytesWritten)) {
    // Process raw bytes securely
}
```

---

## 5. Domain Primitives & Value Objects

### Range&lt;T&gt;
A structural pattern that guarantees `Min <= Max` at creation. It provides deep domain-specific extensions based on the generic type.

```csharp
// Numeric Ranges
Range<int> validAges = new Range<int>(18, 65);
int userAge = validAges.Clamp(70); // Returns 65
int span = validAges.Length();     // Returns 47

// Temporal Ranges
Range<DateTime> promotionPeriod = Range<DateTime>.Between(startDate, endDate);
bool isActive = promotionPeriod.IsNowWithin(); // Automatically checks against UTC Now

// Semantic Version Ranges
Range<SemVer> requiredVersion = new Range<SemVer>(SemVer.Parse("1.0.0"), SemVer.Parse("2.0.0"));
SemVer? latest = requiredVersion.GetLatestCompatible(availableVersions);
```

### UnixTimestamp
Eliminates the ambiguity of passing `long` variables around by explicitly representing UTC milliseconds since Epoch. Provides low-overhead date manipulation without instantiating `DateTime` objects.

```csharp
UnixTimestamp now = UnixTimestamp.Now;

// High-performance truncation using integer math
UnixTimestamp startOfDay = now.TruncateToDay(); 

// Temporal logic
bool isExpired = cacheExpiry.IsOlderThan(TimeSpan.FromMinutes(10));
TimeSpan remaining = cacheExpiry.TimeUntil();
```

### SemVer
A strict, allocation-free implementation of Semantic Versioning 2.0.0. It supports pre-release and build metadata parsing without the heavy overhead of `System.Version`.

```csharp
SemVer current = SemVer.Parse("1.2.3-beta.1");
SemVer next = current.BumpMinor(); // 1.3.0

if (next.IsBackwardCompatibleWith(current)) {
    // Validates Major/Minor precedence rules
}
```

### Percentage
Wraps a `double` representing a value between 0.0 and 1.0. Prevents logic errors involving out-of-bounds percentages.

```csharp
Percentage discount = Percentage.FromInt(15); // Represents 0.15
Percentage remaining = discount.Remaining;    // Represents 0.85

double finalPrice = remaining.ApplyTo(100.0); // 85.0
```

### OperationTimeout
Unifies API parameters that historically required passing both a `TimeSpan` (for absolute timeouts) and a `CancellationToken` (for external cancellation). 

```csharp
// Method signature expects a single unified object
public async ValueTask FetchDataAsync(OperationTimeout timeout) 
{
    // Throws instantly if already expired or cancelled
    timeout.ThrowIfExpired(); 

    // Create a combined CancellationTokenSource automatically
    using var cts = timeout.CreateCancellationTokenSource();
    await _httpClient.GetAsync("/api/data", cts.Token);
}

// Caller can pass either type, and it implicitly converts:
await FetchDataAsync(TimeSpan.FromSeconds(5)); 
await FetchDataAsync(HttpContext.RequestAborted); 
```

## System.Text.Json Integration

All primitives (`SnowflakeId`, `OpaqueId`, `Base64String`, `SemVer`, `UnixTimestamp`, etc.) are decorated with high-performance `JsonConverter` implementations. They serialize directly to primitive JSON formats (strings or numbers) using UTF-8 span writers, bypassing intermediate string allocations entirely.

## License

Licensed under the MIT License.