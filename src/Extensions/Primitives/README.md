# Wiaoj.Primitives

**Wiaoj.Primitives** is a high-performance, security-focused .NET library designed to combat "Primitive Obsession". It provides a suite of strongly-typed value objects and structs that replace standard primitives (`string`, `byte[]`, `double`, `long`) with semantically meaningful, validation-guaranteed types.

Built for .NET 10, it leverages `ref structs`, `Span<T>`, `unsafe` code, and pinned memory to ensure maximum efficiency and security.

## 🌟 Key Features

*   **🛡️ Military-Grade Security:** `Secret<T>` keeps sensitive data (passwords, keys) in pinned, unmanaged memory, immune to Garbage Collector relocation and memory dumps. Automatically zeroes memory on disposal.
*   **❄️ Distributed Identity:** `SnowflakeId` generates unique, k-sorted, time-based 64-bit IDs (Twitter Snowflake algorithm) without collisions. Includes `Urn` support for DDD/Microservices resources.
*   **🚀 Zero-Allocation Encodings:** `Base32String`, `Base64String`, and `HexString` ensure format validity at the type level and provide allocation-free conversion to/from bytes.
*   **📦 Semantic Types:** `SemVer`, `Percentage`, and `Sha256Hash` provide domain-specific logic, optimized parsing, and arithmetic operations.
*   **⚡ Modern .NET Optimization:** Heavy usage of `ISpanParsable`, `ISpanFormattable`, `SIMD`, and lock-free concurrency.

## 📦 Installation

```bash
dotnet add package Wiaoj.Primitives
```

---

## ❄️ Distributed Identity

### `SnowflakeId`
A high-performance, lock-free implementation of the Twitter Snowflake algorithm. Generates 64-bit unique IDs that are roughly sorted by time.

*   **Thread-Safe:** Lock-free generation loop.
*   **Overflow Protection:** Handles clock rollback and sequence overflow gracefully.
*   **Integrations:** Converts to/from `Hex`, `Base64`, `Base32`, and `Urn`.

```csharp
// 1. Configure once at startup (e.g., in Program.cs)
SnowflakeId.Configure(nodeId: 1);

// 2. Generate IDs (Thread-safe, static access)
SnowflakeId id = SnowflakeId.NewId();
Console.WriteLine(id); // "123456789012345678"

// 3. Format for API/Logs
Console.WriteLine(id.ToHexString());    // "1B6F..."
Console.WriteLine(id.ToBase64String()); // Compact URL-safe string
```

### `Urn` (Uniform Resource Name)
Implements RFC 8141 for identifying resources in distributed systems without checking their location.

```csharp
// Create type-safe URNs
SnowflakeId orderId = SnowflakeId.NewId();
Urn resource = Urn.Create("orders", orderId);

Console.WriteLine(resource); // "urn:orders:123456789..."

// Parse and validate
if (Urn.TryParse("urn:user:55", null, out Urn userUrn)) {
    Console.WriteLine($"Namespace: {userUrn.Namespace}"); // "user"
}
```

---

## 🔒 Secure Memory Management

### `Secret<T>`
Never store passwords, API keys, or encryption keys in standard `string` or `byte[]` instances. These are managed by the GC, cannot be explicitly erased, and may linger in memory dumps.

`Secret<T>` allocates unmanaged, zero-initialized memory. It implements `IDisposable` to deterministically zero-out (shred) the data.

```csharp
using Wiaoj.Primitives;
using System.Text;

// Create a secret from a string (immediately converted to bytes in unmanaged memory)
using Secret<byte> apiKey = Secret.From("super-secret-key-123");

// Generate a cryptographically strong random key
using Secret<byte> masterKey = Secret.Generate(32);

// ACCESSING DATA:
// You cannot access the raw pointer directly. You must use the 'Expose' pattern.
// This ensures the scope of sensitive data usage is strictly controlled.
apiKey.Expose(span => {
    // 'span' is a ReadOnlySpan<byte> valid only within this block.
    // Pass 'span' to cryptographic functions here.
    Console.WriteLine($"Key length: {span.Length}");
});

// Deriving a new key (HKDF-SHA256) securely
using Secret<byte> derived = masterKey.DeriveKey(salt: apiKey, outputByteCount: 64);
```

---

## 🔠 Strongly-Typed Encodings

Stop passing `string` around and hoping it's valid Base64. These types validate data upon construction and allow efficient zero-allocation decoding.

### `Base64String`, `Base32String`, `HexString`

```csharp
// Parsing (Validates format immediately, throws FormatException if invalid)
Base64String b64 = Base64String.Parse("SGVsbG8gV29ybGQ=");
HexString hex = HexString.Parse("48656C6C6F");
Base32String b32 = Base32String.Parse("JBSWY3DPEBLW64TMMQ======");

// Zero-allocation decoding
byte[] data = b64.ToBytes(); 

// Or decode directly into a stack buffer
Span<byte> buffer = stackalloc byte[64];
if (hex.TryDecode(buffer, out int bytesWritten)) {
    // Process bytes...
}

// Helpers for plain text
Base64String fromText = Base64String.FromUtf8("Hello World");
```

---

## 🔢 Domain Primitives

### `SemVer` (Semantic Versioning 2.0.0)
A high-performance struct for parsing and comparing versions. Allocates significantly less than `System.Version` and supports pre-release/build metadata strictly.

```csharp
var v1 = SemVer.Parse("1.0.0-alpha.1");
var v2 = SemVer.Parse("1.0.0-beta");

if (v2 > v1) {
    Console.WriteLine($"{v2} is newer"); // Outputs: 1.0.0-beta is newer
}

// Allocation-free formatting
Span<char> dest = stackalloc char[32];
v1.TryFormat(dest, out int charsWritten, "G", null);
```

### `Percentage`
A value type wrapping a `double` (0.0 to 1.0) with operator overloading for intuitive math.

```csharp
var progress = Percentage.FromInt(50); // 0.5
var completed = Percentage.Full;       // 1.0

// Math operations
var half = Percentage.FromDouble(0.5);
var result = 100.0 * half; // Returns 50.0

// Formatting
Console.WriteLine(half); // Outputs: 50%
```

### `OperationTimeout`
Simplifies the common pattern of having both a `TimeSpan` timeout and a `CancellationToken`.

```csharp
// Create a timeout that fires after 5 seconds OR when the token is cancelled
var timeout = OperationTimeout.From(TimeSpan.FromSeconds(5), cancellationToken);

// Create a linked source easily
using var cts = timeout.CreateCancellationTokenSource();
await SomeAsyncOperation(cts.Token);
```

---

## ⚡ High-Performance Hashing

### `Sha256Hash`
A 32-byte fixed-size struct wrapper. Unlike `byte[]`, this is a value type that lives on the stack, preventing heap allocations for hash storage.

```csharp
// Compute hash without allocating a byte array
Sha256Hash hash = Sha256Hash.Compute("Hello World");

// Hex representation
Console.WriteLine(hash.ToString()); 

// Timing-attack safe equality comparison
if (hash == otherHash) {
    // ...
}

// Async stream hashing (Extension method)
using var stream = File.OpenRead("file.txt");
Sha256Hash fileHash = await Sha256HashExtensions.ComputeAsync(stream);
```

## ⚠️ Requirements

*   Allow `unsafe` blocks (Required for `Secret<T>` and `Sha256Hash` pointer manipulation).

## 📄 License

Licensed under the MIT License.