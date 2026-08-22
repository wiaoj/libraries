# Wiaoj.Webhooks.Abstractions

Core contracts, value objects, and abstraction layer for the **Wiaoj.Webhooks** ecosystem.

## 📦 Features & Design
- **Zero-Allocation Value Objects**: `WebhookEndpointId` and `WebhookSignature` are strongly-typed readonly structs implementing `.NET 10` zero-allocation span parsing (`ISpanParsable<T>`, `IUtf8SpanParsable<T>`, `ISpanFormattable`, `IUtf8SpanFormattable`).
- **Dictionary Performance**: Fully implements `IAlternateEqualityComparer<ReadOnlySpan<char>, T>` enabling allocation-free lookups using `.GetAlternateLookup<ReadOnlySpan<char>>()`.
- **Pure Abstraction**: Zero external runtime dependencies.

## 📚 Key Interfaces
- `IWebhookEvent`: Marker contract for dispatchable webhook events.
- `IWebhookDispatcher`: Ingress interface for enqueuing outbound webhook events.
- `IWebhookTransport`: Pluggable delivery queuing mechanism.
- `IWebhookSigner`: Cryptographic HMAC-SHA256 / SHA512 signing contract.
- `IWebhookRetryPolicy`: Transient error classification and backoff delay generator.
- `IWebhookEndpointResolver`: Decoupled destination endpoint store lookup.

## 🔗 Main Ecosystem Documentation
For complete architecture and pipeline documentation, see the [Main Webhooks README](../README.md).
