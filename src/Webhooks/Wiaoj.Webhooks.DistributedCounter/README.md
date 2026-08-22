# Wiaoj.Webhooks.DistributedCounter

Distributed per-endpoint rate limiting middleware for **Wiaoj.Webhooks** powered by `Wiaoj.DistributedCounter`.

## 📦 Overview
Enforces rate limits and traffic smoothing across distributed webhook worker nodes.
- **Sliding Window Counters**: Guarantees accurate rate limiting across multi-instance clusters.
- **Automatic Delayed Re-queuing**: When an endpoint's rate limit is reached, the job is not dropped; instead, it is safely re-enqueued with a delay equal to the sliding window duration.
- **Graceful Throttling**: Emits HTTP 429 diagnostic outcomes and warning telemetry.

## 🚀 Usage
```csharp
builder.Services.AddDistributedCounter(dc => dc.UseInMemory()); // or dc.UseRedis(...)

builder.Services.AddWebhooks(webhooks =>
{
    webhooks.UseDistributedRateLimiting(maxRequestsPerWindow: 50, window: TimeSpan.FromSeconds(1));
});
```

## 🔗 Main Ecosystem Documentation
For complete architecture and ecosystem packages, see the [Main Webhooks README](../README.md).
