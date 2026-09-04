# Wiaoj.BloomFilter.Testing

Test doubles, controllable fake storage, and testing helpers for the `Wiaoj.BloomFilter` ecosystem.

This package allows developers to write fast, deterministic unit and integration tests for filter-dependent application services without requiring local filesystem I/O, background threads, or real hash seeds.

---

## Installation

```bash
dotnet add package Wiaoj.BloomFilter.Testing
```

---

## Key Features

- **`FakeBloomFilter`**: A fast in-memory test double implementing `IPersistentBloomFilter` backed by a `ConcurrentDictionary<string, byte>`. Correctly distinguishes invalid binary UTF-8 sequences via strict decoder fallbacks, tracks dirty state, and supports save/reload simulation.
- **`FakeBloomFilterStorage`**: Thread-safe in-memory storage double for `IBloomFilterStorage` that captures saved binary snapshots in memory buffers and supports inspection, deletion, and reload tests.

---

## Usage Examples

### 1. Unit Testing Application Services with `FakeBloomFilter`

```csharp
using Wiaoj.BloomFilter;
using Wiaoj.BloomFilter.Testing;
using Xunit;

public sealed class PaymentServiceTests {
    [Fact]
    public void Should_RejectDuplicatePayment_When_TransactionIdWasAlreadyProcessed() {
        // Arrange: Use FakeBloomFilter instead of real hashing engine
        FakeBloomFilter fakeFilter = new("payment-dedup");
        PaymentService service = new(fakeFilter);

        string transactionId = "tx_987654321";

        // Act 1: First attempt succeeds
        bool firstAttempt = service.TryProcessPayment(transactionId);

        // Act 2: Duplicate attempt is rejected
        bool secondAttempt = service.TryProcessPayment(transactionId);

        // Assert
        Assert.True(firstAttempt);
        Assert.False(secondAttempt);
        Assert.True(fakeFilter.Contains(transactionId));
    }
}
```

### 2. Testing Persistence and Hydration with `FakeBloomFilterStorage`

```csharp
[Fact]
public async Task Should_PersistAndReloadState_ViaFakeStorage() {
    // Arrange
    FakeBloomFilterStorage storage = new();
    FilterName filterName = FilterName.Parse("audit-filter");
    BloomFilterConfiguration config = new() {
        Name = filterName,
        ExpectedItems = 1_000,
        ErrorRate = 0.01,
        SizeInBits = 10_000,
        HashFunctionCount = 7
    };

    byte[] payload = [1, 2, 3, 4, 5];
    using (MemoryStream ms = new(payload)) {
        await storage.SaveAsync(filterName, config, ms);
    }

    // Act
    (BloomFilterConfiguration? Config, Stream DataStream)? result = await storage.LoadStreamAsync(filterName);

    // Assert
    Assert.NotNull(result);
    using MemoryStream loaded = new();
    await result.Value.DataStream.CopyToAsync(loaded);
    Assert.Equal(payload, loaded.ToArray());
}
```

---

## License

This project is licensed under the MIT License.
