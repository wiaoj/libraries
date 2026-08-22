# Wiaoj Webhooks Engine — Sample & Testing Guide

Once the application is running (`dotnet run`), you can test all the capabilities of the engine (**Store-First Persistence**, **BloomFilter Deduplication**, **Distributed Rate Limiting**, **HMAC-SHA256 Signing**, **Exponential Backoff**, **SSRF Defense**, and **Dead-Lettering with Manual Replay**) directly from your terminal using the scenarios below.

---

### 🧪 Scenario 1: Successful Delivery (Happy Path & Store-First Fast Path)

Create an order and dispatch a webhook to the `acme-corp` endpoint:

```bash
curl -X POST "http://127.0.0.1:5210/api/orders/checkout?endpointId=acme-corp"
```

**Response (`202 Accepted`):**
```json
{
  "message": "Webhook dispatched (Store-First -> In-Memory Fast-Path Accepted)!",
  "jobId": "job_01918a2b3c4d5e6f7a8b9c0d1e2f3a4b",
  "targetEndpoint": "acme-corp",
  "event": {
    "orderId": "ORD-4821",
    "amount": 234.50,
    "createdAt": "2026-08-22T03:45:00Z"
  }
}
```

**Inspect Job State:**
```bash
curl -X GET "http://127.0.0.1:5210/api/webhooks/jobs/job_01918a2b3c4d5e6f7a8b9c0d1e2f3a4b"
```
*(The job status will be `Delivered`, and the `Attempts` array will record the HTTP 200 result).*

---

### 🧪 Scenario 2: BloomFilter Duplicate Event Suppression ($O(1)$ Deduplication)

Dispatch an order with a fixed ID twice in succession:

**1st Call (Successfully Delivered):**
```bash
curl -X POST "http://127.0.0.1:5210/api/orders/checkout-duplicate?orderId=ORD-FIXED-99&endpointId=acme-corp"
```

**2nd Call (Suppressed by BloomFilter in $O(1)$ Time):**
```bash
curl -X POST "http://127.0.0.1:5210/api/orders/checkout-duplicate?orderId=ORD-FIXED-99&endpointId=acme-corp"
```

**Console Log Output:**
```text
info: Wiaoj.Webhooks.BloomFilter.BloomFilterDeduplicationMiddleware[3201]
      Duplicate webhook event detected for endpoint 'acme-corp' with deduplication key 'acme-corp:{"OrderId":"ORD-FIXED-99"...}'. Delivery skipped.
```
*(No redundant outbound HTTP request is issued; the duplicate event is intercepted and suppressed directly in memory).*

---

### 🧪 Scenario 3: Transient Failure & Exponential Backoff Retries (Flaky Receiver)

Dispatch to a flaky receiver that fails with `503 Service Unavailable` on the first 2 attempts, then recovers on the 3rd attempt:

```bash
curl -X POST "http://127.0.0.1:5210/api/orders/checkout?endpointId=flaky-corp"
```

**Console Log Stream:**
```text
warn: Program[0]
      Flaky Receiver simulated HTTP 503 on attempt #1. Retrying soon...
warn: Wiaoj.Webhooks.Internal.RetryMiddleware[4005]
      Webhook delivery attempt #1 for endpoint 'flaky-corp' failed. Next retry scheduled in 2000ms.
warn: Program[0]
      Flaky Receiver simulated HTTP 503 on attempt #2. Retrying soon...
warn: Wiaoj.Webhooks.Internal.RetryMiddleware[4005]
      Webhook delivery attempt #2 for endpoint 'flaky-corp' failed. Next retry scheduled in 4000ms.
info: Program[0]
      Flaky Receiver recovered on attempt #3! Webhook delivered.
info: Wiaoj.Webhooks.Diagnostics.WebhookLoggerExtensions[3002]
      Webhook delivery attempt #3 for job 'job_...' to endpoint 'flaky-corp' succeeded with HTTP 200.
```

---

### 🧪 Scenario 4: Distributed Rate Limiting (Throttling & Delayed Re-enqueue)

The configured rate limit window allows a maximum of **5 requests per 3 seconds**. Dispatch 8 requests rapidly:

```bash
for i in {1..8}; do curl -s -X POST "http://127.0.0.1:5210/api/orders/checkout?endpointId=acme-corp" & done; wait
```

**Console Log Output:**
```text
warn: Wiaoj.Webhooks.DistributedCounter.DistributedRateLimitingMiddleware[4301]
      Rate limit of 5 requests per 3000ms exceeded for endpoint 'acme-corp'. Re-enqueuing delivery.
```
*(The first 5 requests are delivered immediately. The remaining 3 requests are throttled and re-enqueued with a `RetryAfter: 3s` delay, then delivered automatically once the window opens).*

---

### 🧪 Scenario 5: Permanent Failure (400 Bad Request) ➔ Dead-Letter ➔ Manual Replay

Dispatch to an endpoint that consistently returns a non-retryable `400 Bad Request`:

**1. Dispatch Event (Immediately Transitions to Dead-Letter):**
```bash
curl -X POST "http://127.0.0.1:5210/api/orders/checkout?endpointId=broken-corp"
```

**2. Query Dead-Lettered Jobs:**
```bash
curl -X GET "http://127.0.0.1:5210/api/webhooks/dead-letters"
```

**Response:**
```json
{
  "totalCount": 1,
  "deadLetters": [
    {
      "id": "job_01918a99887766554433221100aabbcc",
      "endpointId": "broken-corp",
      "eventType": "order.created",
      "status": "DeadLettered",
      "attempts": [
        {
          "attemptNumber": 1,
          "isSuccess": false,
          "result": {
            "isSuccess": false,
            "errorMessage": "HTTP request permanently rejected with status code 400."
          }
        }
      ]
    }
  ]
}
```

**3. Operator Triggered Manual Replay:**
```bash
curl -X POST "http://127.0.0.1:5210/api/webhooks/jobs/job_01918a99887766554433221100aabbcc/replay"
```
*(The job transitions back to `Queued` and is re-enqueued onto the execution transport using the original pre-serialized JSON payload with zero reflection overhead).*

---

### 🧪 Scenario 6: HMAC-SHA256 Signature Verification & Tampering Protection

Test direct calls to the receiver endpoint with missing or invalid signatures:

```bash
# Unsigned Request -> 401 Unauthorized
curl -i -X POST "http://127.0.0.1:5210/api/webhooks/receiver" \
     -d '{"orderId":"HACK-1"}'

# Tampered / Invalid Signature Request -> 401 Unauthorized
curl -i -X POST "http://127.0.0.1:5210/api/webhooks/receiver" \
     -H "Webhook-Signature: t=1724300000,v1=fake_tampered_signature_hash" \
     -d '{"orderId":"HACK-1"}'
```