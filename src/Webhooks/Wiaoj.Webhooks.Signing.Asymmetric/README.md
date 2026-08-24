# Wiaoj.Webhooks.Signing.Asymmetric

[![.NET 10.0](https://img.shields.io/badge/.NET-10.0-512BD4?style=flat&logo=dotnet)](https://dotnet.microsoft.com/)
[![Tests Passing](https://img.shields.io/badge/Unit%20Tests-Passing-success)](https://github.com/wiaoj/libraries)
[![License](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

Asymmetric cryptographic signing and verification engine for **Wiaoj.Webhooks** supporting **RSA (RS256/PS256)**, **ECDSA (ES256/ES384/ES512)**, and **Ed25519 (Standard Webhooks)**.

Enables zero-shared-secret webhook architectures where senders sign using private keys and receivers verify using public keys only.

---

## 📑 Supported Algorithms

| Algorithm | Key Type | Signature Scheme | Standard / Use Case | Status |
|---|---|---|---|---|
| **`RSA PS256`** | RSA 2048/3072/4096-bit | `v1_ps256` | RFC 7518 RSASSA-PSS (Recommended RSA) | ✅ Production |
| **`RSA RS256`** | RSA 2048/3072/4096-bit | `v1_rs256` | RFC 7518 RSASSA-PKCS1-v1_5 (PayPal Webhooks) | ✅ Production |
| **`ECDSA ES256`** | NIST P-256 | `v1_es256` | RFC 7518 / IEEE P1363 (Apple Notifications, DPoP) | ✅ Production |
| **`ECDSA ES384`** | NIST P-384 | `v1_es384` | High-Security Government / Financial | ✅ Production |
| **`ECDSA ES512`** | NIST P-521 | `v1_es512` | Ultra-High Security | ✅ Production |
| **`Ed25519`** | Curve25519 (32B Public Key) | `v1a` | RFC 8032 / Standard Webhooks (Svix) | 🧪 `[Experimental]` |

---

## 🚀 Quick Start

### Outbound Signing Registration
```csharp
using Wiaoj.Webhooks;
using Wiaoj.Webhooks.Signing.Asymmetric;

// 1. Register ECDSA (NIST P-256) Outbound Signer
builder.Services.AddWiaojWebhooks(webhooks =>
{
    webhooks.UseEcdsaSigning(EcdsaAlgorithm.ES256);
});

// 2. Or Register RSA (PS256) Outbound Signer
builder.Services.AddWiaojWebhooks(webhooks =>
{
    webhooks.UseRsaSigning(RsaAlgorithm.PS256);
});
```

### Inbound Public-Key Verification
```csharp
// Verifying directly with public key in ASP.NET Core:
RsaWebhookSigner signer = new(RsaAlgorithm.RS256);

bool isAuthentic = signer.Verify(
    payload: rawPayloadBytes,
    signatureHeader: request.Headers["Webhook-Signature"],
    publicKey: receiverPublicKey,
    tolerance: TimeSpan.FromMinutes(5),
    currentTimestamp: UnixTimestamp.Now);
```

---

## 📄 License

This package is part of the **Wiaoj.Webhooks** ecosystem and is licensed under the [MIT License](../../LICENSE).