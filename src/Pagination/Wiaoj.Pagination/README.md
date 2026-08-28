# Wiaoj.Pagination

A foundational .NET library that provides domain primitives, request models, and response structures for offset-based and keyset (cursor-based) pagination.

`Wiaoj.Pagination` contains no dependencies on ASP.NET Core or Entity Framework Core, making its contracts safe for use in Domain, Application, and Infrastructure layers.

---

## Features

- **Offset Pagination:** Lightweight `PageRequest`, `PageMetadata`, and `PagedResult<T>` structures with `long` total record support.
- **Keyset / Cursor Pagination:** Forward and backward navigation models (`CursorRequest`, `CursorToken`, `CursorMetadata`, `CursorResult<T>`).
- **Cryptographic Cursor Signing:** HMAC-SHA256 signed cursor tokens (`SignedCursorToken`) to prevent parameter tampering.
- **Framework-Agnostic Binding:** Implements `ISpanParsable<T>` and `IUtf8SpanParsable<T>` for route and query string binding in Minimal APIs without framework dependencies.
- **Collection Value Equality:** Powered by `EquatableArray<T>` for content-based equality comparisons.
- **System.Text.Json Support:** Built-in converters for all metadata, token, and generic result types.

---

## Installation

```bash
dotnet add package Wiaoj.Pagination
```

---

## Core Types Overview

| Category | Model | Description |
| :--- | :--- | :--- |
| **Offset** | `PageRequest` | Represents an input request containing `PageNumber` and `PageSize`. Clamps limits and calculates query offsets (`CalculateSkip`). |
| | `PageMetadata` | Contains pagination state: `TotalCount` (`long`), `TotalPages` (`long`), `PageNumber`, `PageSize`, `HasPrevious`, and `HasNext`. |
| | `PagedResult<T>` | Wraps `EquatableArray<T>` and `PageMetadata`. Supports projections via `.Select(...)`. |
| **Keyset** | `CursorToken` | An opaque Base64Url-encoded token representing a sequence position. |
| | `CursorRequest` | Input request containing `CursorToken`, `Limit`, and `CursorDirection` (`Forward` / `Backward`). |
| | `CursorMetadata` | Stores window boundaries: `StartCursor`, `EndCursor`, `HasPrevious`, and `HasNext`. |
| | `CursorResult<T>` | Wraps `EquatableArray<T>` and `CursorMetadata`. Supports projections via `.Select(...)`. |
| **Security** | `SignedCursorToken` | Encapsulates a `CursorToken` and its 32-byte HMAC-SHA256 signature (`Payload.Signature`). |

---

## Usage Examples

### 1. Offset-Based Pagination

```csharp
using Wiaoj.Pagination;
using Wiaoj.Primitives.Collections;

// 1. Create or bind a request (clamped to MaxPageSize: 100)
var request = new PageRequest(pageNumber: 2, pageSize: 20);
int skip = request.CalculateSkip(); // 20

// 2. Fetch data and construct metadata (TotalCount supports 64-bit integers)
long totalRecords = 150;
var metadata = new PageMetadata(totalRecords, request.PageNumber, request.PageSize);

// 3. Wrap items into PagedResult
EquatableArray<string> items = new[] { "Product A", "Product B" };
var result = new PagedResult<string>(items, metadata);

// 4. Project elements while preserving metadata
PagedResult<string> mapped = result.Select(p => p.ToUpperInvariant());
```

---

### 2. Keyset / Cursor-Based Pagination

```csharp
using Wiaoj.Pagination;
using Wiaoj.Primitives.Collections;

// 1. Parse a cursor from client input
var token = CursorToken.FromUtf8("order_id_10920");
var request = new CursorRequest(token, limit: 25, CursorDirection.Forward);

// 2. Build metadata with window boundaries
var metadata = new CursorMetadata(
    startCursor: CursorToken.FromUtf8("order_id_10900"),
    endCursor: CursorToken.FromUtf8("order_id_10925"),
    hasPrevious: true,
    hasNext: true
);

// 3. Return results
EquatableArray<int> ids = new[] { 10901, 10902, 10903 };
var cursorResult = new CursorResult<int>(ids, metadata);
```

---

### 3. Cryptographically Signed Cursors (HMAC-SHA256)

Use `SignedCursorToken` to prevent clients from forging pagination keys:

```csharp
using Wiaoj.Pagination;

ReadOnlySpan<byte> secretKey = "your-32-byte-secret-key-here-!!!"u8;

// 1. Sign a cursor token on emission
var rawToken = CursorToken.FromUtf8("user_id_4401");
SignedCursorToken signed = SignedCursorToken.Sign(rawToken, secretKey);

string clientPayload = signed.ToString(); 
// Output format: "Payload.Signature" (e.g. "dXNlcl9pZF80NDAx.f8A2..._k")

// 2. Verify and extract upon receiving a request
if (SignedCursorToken.TryParse(clientPayload, out var incoming) && 
    incoming.TryUnsign(secretKey, out var verifiedToken))
{
    // Token is authentic and unmodified
    ReadOnlySpan<char> cursorValue = verifiedToken.Value;
}
```

---

### 4. String Parsing & Span Formatting

All request and token types implement standard BCL formatting and parsing interfaces (`ISpanParsable<T>`, `ISpanFormattable`, `IUtf8SpanFormattable`).

```csharp
// Parsing from strings / spans
PageRequest parsedRequest = PageRequest.Parse("2:50");
CursorRequest parsedCursor = CursorRequest.Parse("dG9rZW4:25:Backward");

// Zero-allocation span formatting
Span<char> charBuffer = stackalloc char[64];
if (parsedRequest.TryFormat(charBuffer, out int charsWritten))
{
    ReadOnlySpan<char> output = charBuffer[..charsWritten]; // "2:50"
}
```

---

### 5. Alternate Span Lookups in Collections

`CursorToken` supports .NET alternate equality comparisons, allowing lookups in `Dictionary<CursorToken, TValue>` and `HashSet<CursorToken>` directly using `ReadOnlySpan<char>` or `ReadOnlySpan<byte>`:

```csharp
var cache = new Dictionary<CursorToken, string>(CursorToken.OrdinalComparer);
cache[CursorToken.FromUtf8("item_99")] = "CachedData";

var lookup = cache.GetAlternateLookup<ReadOnlySpan<char>>();
bool exists = lookup.TryGetValue("item_99".AsSpan(), out string? cached);
```

---

## JSON Serialization

Pre-configured `System.Text.Json` converters are registered directly on types.

### `PagedResult<T>` JSON Payload

```json
{
  "items": [
    "Item 1",
    "Item 2"
  ],
  "metadata": {
    "totalCount": 150,
    "pageNumber": 1,
    "pageSize": 2,
    "totalPages": 75,
    "hasPrevious": false,
    "hasNext": true
  }
}
```

### `CursorResult<T>` JSON Payload

```json
{
  "items": [
    "Item 1",
    "Item 2"
  ],
  "metadata": {
    "startCursor": "bXlfY3Vyc29yXzE",
    "endCursor": "bXlfY3Vyc29yXzI",
    "hasPrevious": false,
    "hasNext": true
  }
}
```

---

## License

This project is licensed under the MIT License.
