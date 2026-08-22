# Wiaoj.Webhooks.BloomFilter

High-performance, zero-database duplicate event deduplication middleware for **Wiaoj.Webhooks** powered by `Wiaoj.BloomFilter`.

## 📦 Overview
Intercepts duplicate events at the middleware layer using memory-efficient probabilistic Bloom filters.
- **O(1) Memory Efficiency**: Prevents repeated event delivery with zero database or cache network calls.
- **Configurable Key Selector**: Customize deduplication keys based on endpoint, payload hash, or domain IDs.
- **Short-Circuit Handling**: Gracefully returns a 200 OK audit result when duplicate events are discarded.

## 🚀 Usage
```csharp
builder.Services.AddBloomFilter(bf => bf.AddFilter("webhook-dedup", expectedItems: 1_000_000, errorRate: 0.001));
builder.Services.AddSingleton<IBloomFilter>(sp => sp.GetRequiredKeyedService<IBloomFilter>("webhook-dedup"));

builder.Services.AddWebhooks(webhooks =>
{
    webhooks.UseBloomFilterDeduplication(new BloomFilterDeduplicationOptions
    {
        KeySelector = ctx => $"{ctx.Endpoint.Id.Value}:{ctx.SerializedPayload}"
    });
});
```

## 🔗 Main Ecosystem Documentation
For complete architecture and ecosystem packages, see the [Main Webhooks README](../README.md).
