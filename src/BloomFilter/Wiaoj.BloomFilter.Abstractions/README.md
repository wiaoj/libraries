# Wiaoj.BloomFilter.Abstractions

Core contracts, interfaces, and value primitives for the `Wiaoj.BloomFilter` library.

This package defines the public API surface, storage provider interfaces, and domain value objects required to consume or extend the Bloom Filter system without referencing concrete storage implementations or runtime hosting packages.

---

## Installation

```bash
dotnet add package Wiaoj.BloomFilter.Abstractions
```

---

## Domain Primitives and Types

### `FilterName` (`readonly record struct`)
- Validated immutable identifier for named Bloom Filter instances.
- Restricts length between 1 and 128 characters and enforces valid characters (`[a-zA-Z0-9_.-]`).
- Implements `ISpanParsable<FilterName>`, `IUtf8SpanParsable<FilterName>`, and `ISpanFormattable` for allocation-free parsing and formatting.

### `BloomFilterConfiguration` (`sealed record`)
- Immutable configuration model encapsulating:
  - `ExpectedItems` ($n$): Capacity limit before degradation.
  - `ErrorRate` ($p$): Target false positive probability strictly between 0.0 and 1.0.
  - `SizeInBits` ($m$): Total bit array length calculated by optimal math formulas.
  - `HashFunctionCount` ($k$): Number of hash iterations derived from $m/n \cdot \ln(2)$.
  - `HashSeed`: 64-bit seed value for hashing engines.
  - `ShardCount`: Number of underlying partitioned shards.

### `GrowthRate` (`readonly record struct`)
- Capacity multiplier for subsequent layers in scalable filters.
- Built-in constants: `GrowthRate.Double` (2.0), `GrowthRate.Quadruple` (4.0).

### `Percentage` (`readonly record struct`)
- Represents normalized floating-point percentages between 0.0 and 1.0.
- Used for error rates, saturation thresholds, and fill ratios.

### `BloomFilterType` (`enum`)
- Architectural variants: `InMemory`, `Scalable`, `Rotating`.

---

## Core Interfaces

| Interface | Role |
| :--- | :--- |
| **`IBloomFilter`** | Primary query and mutation contract: `Add(ReadOnlySpan<byte>)`, `Contains(ReadOnlySpan<byte>)`, `Add(ReadOnlySpan<char>)`, `Contains(ReadOnlySpan<char>)`, and `GetPopCount()`. |
| **`IBloomFilter<TTag>`** | Open-generic tag-based DI wrapper for domain-scoped filter resolution. |
| **`IPersistentBloomFilter`** | Extends `IBloomFilter` with storage synchronization: `IsDirty`, `SaveAsync()`, and `ReloadAsync()`. |
| **`IBloomFilterStorage`** | Persistence backend contract: `SaveAsync()`, `LoadStreamAsync()`, and `DeleteAsync()`. |
| **`IBloomFilterRegistry`** | Thread-safe discovery registry for tracking and resolving instantiated persistent filters. |
| **`IBloomFilterFactory`** | Engine factory for instantiating and hydrating filters by name or tag. |
| **`IAutoBloomFilterSeeder`** | Interface for cold-start seeding filters from databases or external caches. |

---

## Exception Types

- **`DataIntegrityException`**: Thrown during reload operations if snapshot checksums fail, headers are corrupted, non-seekable streams cannot be rewound, or configuration fingerprints mismatch.

---

## License

This project is licensed under the MIT License.
