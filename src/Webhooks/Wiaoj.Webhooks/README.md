# Wiaoj.Webhooks

The core pipeline execution engine, HTTP delivery transport, cryptographic signers, and resilience middleware for **Wiaoj.Webhooks**.

## 📦 Features
- **Middleware Pipeline Runner**: Extensible ASP.NET Core-like pipeline model for outbound webhook delivery.
- **Striped Partitioning**: Built-in `PartitionedDeliveryMiddleware` using `Wiaoj.Concurrency` to serialize deliveries per endpoint ID while keeping high cross-endpoint concurrency.
- **Cryptographic Security**: Constant-time `HmacSha256WebhookSigner` and `HmacSha512WebhookSigner` with dual-secret rotation and anti-tampering protection.
- **Resilient Retries**: `ExponentialBackoffPolicy`, `LinearBackoffPolicy`, and `FixedIntervalBackoffPolicy` with full jitter support and HTTP status classifier.
- **OpenTelemetry Instrumentation**: Distributed tracing spans (`ActivitySource`) and runtime delivery metrics (`Meter`).

## 🚀 Installation & Registration
```csharp
builder.Services.AddWebhooks(webhooks =>
{
    webhooks.UsePartitionedDelivery(64)
            .UseHmacSha256Signing()
            .UseExponentialBackoffRetry(new ExponentialBackoffOptions
            {
                MaxAttempts = 5,
                InitialDelay = TimeSpan.FromSeconds(2),
                Multiplier = 2.0,
                UseJitter = true
            });
});
```

## 🔗 Main Ecosystem Documentation
For complete architecture and ecosystem packages, see the [Main Webhooks README](../README.md).
