# Wiaoj.Primitives

**Wiaoj.Primitives** is a high-performance, security-focused .NET library engineered to eliminate "Primitive Obsession" in domain-driven applications. It replaces generic primitives (`string`, `byte[]`, `double`, `long`) with strongly-typed, validation-guaranteed value objects.

Designed for **.NET 10** and beyond, it leverages advanced runtime features like `ref structs`, `Span<T>`, `unsafe` memory manipulation, SIMD vectorization, and pinned memory to deliver zero-allocation performance with military-grade security.

## 🌟 Why Use Wiaoj.Primitives?

*   **🛡️ Secure Memory:** `Secret<T>` keeps sensitive data (keys, passwords) in unmanaged, pinned memory. It is immune to GC relocation, prevents memory dump leaks, and guarantees deterministic zeroing (shredding) upon disposal.
*   **❄️ Distributed Identity:** Use `SnowflakeId` for generating k-sorted, 64-bit unique IDs without database coordination. Perfect for distributed systems.
*   **🚀 Zero-Allocation:** Parsing methods (`Parse`, `TryParse`) use `ReadOnlySpan<char>` and `ReadOnlySpan<byte>` to process data directly from network buffers without string allocation overhead.
*   **📦 Type Safety:** Eliminate errors like passing a Base64 string into a Hex parameter. Types like `Base64String`, `HexString`, and `Base32String` guarantee format validity at the type level.
*   **⚡ Modern Optimizations:** SIMD-accelerated validation using `.NET 8 SearchValues`, lock-free concurrency, and `stackalloc` optimizations.

## 📦 Installation

```bash
dotnet add package Wiaoj.Primitives
```

> **Note:** This library requires `AllowUnsafeBlocks` to be enabled in your `.csproj` for advanced memory manipulation.

---

## ❄️ Distributed Identity (`SnowflakeId`)

A high-performance, lock-free implementation of the Twitter Snowflake algorithm. Generates 64-bit unique IDs that are roughly sorted by time.

*   **Thread-Safe:** Uses `Interlocked` operations for lock-free generation.
*   **Crash Resistant:** Handles system clock rollbacks and sequence overflows gracefully.
*   **Interoperable:** Native support for `Hex`, `Base62`, `Base64`, and `Urn` formats.

```csharp
using Wiaoj.Primitives.Snowflake;

// 1. Configure once (e.g., in Program.cs)
SnowflakeId.Configure(nodeId: 1);

// 2. Generate ID (Allocates nothing, just returns a struct wrapping a long)
SnowflakeId id = SnowflakeId.NewId(); 
Console.WriteLine(id); // "123456789012345678"

// 3. Convert to efficient formats
Console.WriteLine(id.ToHexString());    // "1B6F..."
Console.WriteLine(id.ToBase62String()); // "3k7Za..." (URL-Shortener friendly)
```

### `Urn` (Uniform Resource Name)
Implements RFC 8141 for identifying resources in microservices.

```csharp
// Type-safe creation
Urn userUrn = Urn.Create("user", SnowflakeId.NewId());
Console.WriteLine(userUrn); // "urn:user:123456789..."

// Zero-allocation parsing
if (Urn.TryParse("urn:order:55", null, out Urn orderUrn)) {
    Console.WriteLine(orderUrn.Namespace.ToString()); // "order"
}
```

---

## 🔒 Secure Memory (`Secret<T>`)

Never store passwords, API keys, or encryption keys in standard `string` or `byte[]`. These managed types linger in memory, are copied by the GC, and cannot be explicitly erased.

`Secret<T>` allocates unmanaged memory that is **pinned** and **zero-initialized**. It implements `IDisposable` to securely wipe data immediately after use.

```csharp
using Wiaoj.Primitives;

// Create a secret from a string (String is converted to bytes and wiped from stack)
using Secret<byte> apiKey = Secret.From("super-secret-key-123");

// Generate cryptographically strong random bytes directly into secure memory
using Secret<byte> masterKey = Secret.Generate(32);

// ACCESSING DATA:
// The 'Expose' pattern prevents data from escaping the secure scope.
apiKey.Expose(span => {
    // 'span' is a ReadOnlySpan<byte> valid only within this block.
    // It points to unmanaged memory. Passing it to cryptographic functions is safe.
    Console.WriteLine($"Key length: {span.Length}");
});

// Securely derive keys (HKDF-SHA256) without intermediate allocations
using Secret<byte> derivedKey = masterKey.DeriveKey(salt: apiKey, outputByteCount: 64);
```

---

## 🔠 Strongly-Typed Encodings

Stop treating encoding strings as generic `string`. These value objects validate their format upon creation and offer optimized conversions.

### `Base64String`, `Base32String`, `Base62String`, `HexString`

*   **Validation:** Throws immediately if invalid characters or padding are detected.
*   **SIMD:** Uses `SearchValues<T>` for ultra-fast validation.
*   **Stack-Friendly:** Supports encoding/decoding directly to `Span<byte>`.

```csharp
// Safe Parsing
Base64String b64 = Base64String.Parse("SGVsbG8gV29ybGQ=");
Base32String b32 = Base32String.Parse("JBSWY3DPEBLW64TMMQ======"); // RFC 4648
HexString hex    = HexString.Parse("48656C6C6F");

// Zero-Allocation Decoding
byte[] data = b64.ToBytes(); 

// Or decode directly into stack memory
Span<byte> buffer = stackalloc byte[64];
if (hex.TryDecode(buffer, out int bytesWritten)) {
    // Process raw bytes...
}

// Helpers
Base64String fromText = Base64String.FromUtf8("Hello World");
```

---

## ⚡ High-Performance Hashing (`Sha256Hash`)

A 32-byte **fixed-size struct** wrapper. Unlike `byte[]`, this is a value type that lives on the stack, preventing heap allocations for hash storage and reducing GC pressure.

```csharp
// Compute hash (Zero Allocation)
Sha256Hash hash = Sha256Hash.Compute("Hello World");

// Hex representation
Console.WriteLine(hash.ToString()); 

// Constant-Time Equality (Prevents Timing Attacks)
if (hash == otherHash) {
    // Verified securely
}

// Async Stream Hashing
using var stream = File.OpenRead("large_file.iso");
Sha256Hash fileHash = await Sha256HashExtensions.ComputeAsync(stream);
```

---

## 🔢 Domain Primitives

### `UnixTimestamp`
Wraps a `long` representing milliseconds since Epoch. Provides `DateTime` interoperability without the overhead.

```csharp
UnixTimestamp now = UnixTimestamp.Now;
UnixTimestamp future = now + TimeSpan.FromMinutes(5);

Console.WriteLine(now.ToDateTimeUtc()); // Standard .NET DateTime
```

### `Percentage`
A value type wrapping a `double` (0.0 to 1.0) with clamped arithmetic operations.

```csharp
Percentage p1 = Percentage.FromInt(50); // 0.5
Percentage p2 = Percentage.FromDouble(0.6);

// Clamped Addition (Max 1.0)
Percentage sum = p1.AddClamped(p2); // Returns 1.0, not 1.1

// Formatting
Console.WriteLine(p1); // "50%"
```

### `SemVer` (Semantic Versioning)
Strict SemVer 2.0.0 implementation. Allocates significantly less than `System.Version` and supports pre-release/build metadata correctly.

```csharp
var v1 = SemVer.Parse("1.0.0-alpha.1");
var v2 = SemVer.Parse("1.0.0-beta");

if (v2 > v1) {
    // True: Beta is newer than Alpha
}
```

### `OperationTimeout`
Simplifies the pattern of having both a `TimeSpan` timeout and a `CancellationToken`.

```csharp
// Defines a timeout of 5 seconds OR cancellation token trigger
var timeout = OperationTimeout.From(TimeSpan.FromSeconds(5), cancellationToken);

// Execute logic with combined token
await timeout.ExecuteAsync(async (t) => {
    // t.Token cancels if 5s passes OR parent token cancels
    await Task.Delay(1000, t.Token);
});
```

## 🛠️ Requirements

*   **.NET 10** (or compatible modern .NET runtime)
*   **Unsafe Blocks:** Must be enabled in project settings.

## 📄 License

Licensed under the MIT License.