# Wiaoj.Webhooks.Transports.InMemory

High-throughput, in-process channel transport and background worker for **Wiaoj.Webhooks**.

## 📦 Overview
- **Bounded Channel**: Powered by `System.Threading.Channels` for non-blocking asynchronous event ingress.
- **Delayed Scheduling**: Asynchronously holds delayed deliveries (e.g. for retries and throttled rate-limited jobs) using efficient `Task.Delay` tokens.
- **Hosted Consumer**: Background worker that continuously pulls and delivers jobs through `WebhookJobHandler`.

## 🚀 Usage

### Option 1: Fluent Webhook Builder (Recommended)
```csharp
builder.Services.AddWebhooks(webhooks =>
{
    webhooks.UseInMemoryTransport(capacity: 100_000);
});
```

### Option 2: Direct Service Registration
```csharp
builder.Services.AddInMemoryWebhookTransport(capacity: 100_000);
```

## 🔗 Main Ecosystem Documentation
For complete architecture and ecosystem packages, see the [Main Webhooks README](../README.md).
