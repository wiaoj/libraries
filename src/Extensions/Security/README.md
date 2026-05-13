# Wiaoj.Security

A strongly-typed, AES-GCM envelope encryption library for .NET with automatic key rotation, EF Core persistence, and OpenTelemetry metrics.

---

## Table of Contents

- [Overview](#overview)
- [Packages](#packages)
- [How It Works](#how-it-works)
- [Architecture](#architecture)
- [Quick Start](#quick-start)
  - [1. Implement IEncryptionKeyDbContext](#1-implement-iencryptionkeydbcontext)
  - [2. Generate a master key](#2-generate-a-master-key)
  - [3. Register services](#3-register-services)
  - [4. Define secret domains](#4-define-secret-domains)
  - [5. Use the protector](#5-use-the-protector)
  - [6. Implement IDataRotator](#6-implement-idatarotator)
  - [7. Add a migration](#7-add-a-migration)
- [Key Rotation](#key-rotation)
  - [Automatic rotation](#automatic-rotation)
  - [Forced rotation](#forced-rotation)
  - [Key lifecycle](#key-lifecycle)
- [Master Key Providers](#master-key-providers)
  - [Environment variable (dev/staging)](#environment-variable-devstaging)
  - [IConfiguration / appsettings](#iconfiguration--appsettings)
  - [File (Docker Secrets, Kubernetes Secrets)](#file-docker-secrets-kubernetes-secrets)
  - [Custom provider (Azure Key Vault, AWS KMS)](#custom-provider-azure-key-vault-aws-kms)
- [EF Core Mapping](#ef-core-mapping)
  - [Single-column storage](#single-column-storage)
  - [Two-column storage](#two-column-storage)
  - [Convention-based mapping](#convention-based-mapping)
- [Health Checks](#health-checks)
- [Observability (OpenTelemetry)](#observability-opentelemetry)
- [Configuration Reference](#configuration-reference)
- [Database Schema](#database-schema)
- [Security Notes](#security-notes)
- [AES Key Sizes](#aes-key-sizes)

---

## Overview

Wiaoj.Security solves the problem of encrypting sensitive fields (webhook secrets, OAuth tokens, payment keys, PII) stored in a relational database, while keeping the ability to rotate keys automatically without downtime.

Key design goals:

- **Type safety** — phantom type contexts (`ISecretContext`) prevent secrets from different domains from being mixed up at compile time.
- **Context binding (AAD)** — Every ciphertext is cryptographically bound to its `ISecretContext` name using AES-GCM Associated Data. This prevents cross-domain attacks even if keys are leaked or reused.
- **Log safety** — `CipherBlob` and `EncryptedSecret<T>` override `ToString()` to return safe sentinels; raw ciphertext never leaks into logs.
- **Key wrapping** — Data Encryption Keys (DEKs) are stored in the database wrapped (encrypted) with a master key. The master key never touches the database.
- **Automatic rotation** — a background service checks key age on a configurable interval and rotates when a key exceeds its `RotationInterval`.
- **Hot reload** — after a rotation, the in-memory key ring is atomically swapped without restarting the application or dropping any in-flight requests.
- **Zero-copy secrets** — key material lives in unmanaged memory (`Secret<T>`) and is zeroed on disposal.

---

## Packages

| Package | Purpose |
|---------|---------|
| `Wiaoj.Security.Abstractions` | Core types: `EncryptedSecret<T>`, `CipherBlob`, `KeyVersion`, `ISecretProtector<T>`, `ISecretContext`, `IDataRotator<T>` |
| `Wiaoj.Security` | `SecretProtector<T>`, `KeyRing<T>`, `MasterKey`, `EncryptionKeyRecord`, master key providers |
| `Wiaoj.Security.DependencyInjection` | `ISecurityBuilder`, `AddWiaojSecurity()`, master key provider registration extensions |
| `Wiaoj.Security.EntityFrameworkCore` | `EfEncryptionKeyStore`, `EncryptedSecretValueConverter`, EF configuration helpers |
| `Wiaoj.Security.Rotation` | `ManagedSecretProtector<T>`, `KeyRotationService<T>`, `RotationBackgroundService<T>`, health check |

---

## How It Works

```
┌─────────────────────────────────────────────────────────────────────────┐
│                          Envelope encryption                            │
│                                                                         │
│  Master Key (env / KMS)                                                 │
│       │  wraps / unwraps                                                │
│       ▼                                                                 │
│  EncryptionKeyRecord (DB)  ←→  KeyRing<TContext> (in-memory)           │
│       wrapped DEK                    │                                  │
│                                      │  encrypts / decrypts             │
│                                      ▼                                  │
│                         EncryptedSecret<TContext> (DB column)           │
└─────────────────────────────────────────────────────────────────────────┘
```

1. On startup, `KeyRingLoader<TContext>` loads all `EncryptionKeyRecord` rows for the context from the database and unwraps each DEK with the master key, building an in-memory `KeyRing<TContext>`.
2. `ManagedSecretProtector<TContext>` (singleton) wraps the key ring and exposes `Protect` / `Unprotect` / `Rotate`.
3. A `RotationBackgroundService<TContext>` wakes up every `CheckInterval` (default 6 h), compares the active key's age to `RotationInterval` (default 90 days), and triggers `KeyRotationService<TContext>` if rotation is needed.
4. Rotation generates a new DEK, wraps it with the master key, saves it to the database, retires the old key, and hot-reloads the in-memory key ring atomically.
5. If an `IDataRotator<TContext>` is registered and `AutoRotateData` is `true`, the background service re-encrypts all stale application records in batches after each rotation.

---

## Architecture

```
App Startup
    │
    ▼
SecurityInitializationService<TContext>  (IHostedService — runs before requests)
    │  calls EnsureInitializedAsync()
    ▼
ManagedSecretProtector<TContext>         (singleton, lazy, hot-reloadable)
    │  AsyncLazy<SecretProtector<TContext>>
    │
    │  on first access or reload:
    ▼
KeyRingLoader<TContext>                  (scoped)
    │  LoadKeysAsync()  → IEncryptionKeyStore
    │  UnwrapToKey()    → IMasterKeyProvider + MasterKey
    │  → KeyRing<TContext>
    │
    ▼
SecretProtector<TContext>                (owns the KeyRing)
    │  Protect(plaintext)  → EncryptedSecret<TContext>
    │  Unprotect(secret)   → Secret<byte>
    │  Rotate(secret)      → EncryptedSecret<TContext>

Background loop (every CheckInterval):
    ▼
RotationBackgroundService<TContext>      (BackgroundService singleton)
    │  creates scoped KeyRotationService per tick
    ▼
KeyRotationService<TContext>             (scoped)
    ├─ RotateIfNeededAsync()   — checks key age vs RotationInterval
    └─ ForceRotateAsync()      — manual / admin endpoint
         │
         ├─ 1. Generate new AES DEK
         ├─ 2. Wrap with master key → save to DB
         ├─ 3. Retire old key in DB
         ├─ 4. ManagedSecretProtector.ReloadAsync()  (atomic ring swap)
         └─ 5. IDataRotator<TContext>.RotateBatchAsync()  (if registered)
```

---

## Quick Start

### 1. Implement IEncryptionKeyDbContext

Add the `EncryptionKeys` set to your `DbContext` and apply the EF configuration:

```csharp
public class AppDbContext : DbContext, IEncryptionKeyDbContext
{
    public DbSet<EncryptionKeyRecord> EncryptionKeys { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new EncryptionKeyRecordConfiguration());
        // ... your other configs
    }
}
```

### 2. Generate a master key

```bash
# bash
openssl rand -base64 32

# PowerShell
[Convert]::ToBase64String([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(32))

# C#
Console.WriteLine(Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
```

> **Important:** Store this value in your secrets manager (Azure Key Vault, AWS Secrets Manager, HashiCorp Vault, Kubernetes Secret, Docker Secret). Never commit it to source control or put it in `appsettings.json`.

### 3. Register services

```csharp
// Program.cs
builder.Services
    .AddWiaojSecurity(opts =>
    {
        opts.RotationInterval      = TimeSpan.FromDays(90);
        opts.CheckInterval         = TimeSpan.FromHours(6);
        opts.KeySizeInBits         = 256;        // 128, 192, or 256
        opts.AutoRotateData        = true;
        opts.DataRotationBatchSize = 100;
        opts.DataRotationBatchDelay = TimeSpan.FromMilliseconds(50);
    })
    .AddEnvironmentMasterKey("APP_MASTER_KEY")        // reads from env var
    .AddEntityFrameworkKeyStore<AppDbContext>()        // store DEKs in EF Core
    .AddManagedProtector<WebhookSigningContext>()      // full rotation lifecycle
    .AddDataRotator<WebhookSigningContext, WebhookDataRotator>(); // re-encrypt data
```

You can also bind options from `appsettings.json`:

```csharp
builder.Services
    .AddWiaojSecurity(builder.Configuration.GetSection("Security"))
    .AddEnvironmentMasterKey()
    .AddEntityFrameworkKeyStore<AppDbContext>()
    .AddManagedProtector<WebhookSigningContext>();
```

```json
// appsettings.json
{
  "Security": {
    "RotationInterval": "90.00:00:00",
    "CheckInterval": "06:00:00",
    "KeySizeInBits": 256,
    "AutoRotateData": true,
    "DataRotationBatchSize": 100
  }
}
```

You can call `AddManagedProtector<TContext>()` once per secret domain:

```csharp
builder.Services
    .AddWiaojSecurity()
    .AddEnvironmentMasterKey()
    .AddEntityFrameworkKeyStore<AppDbContext>()
    .AddManagedProtector<WebhookSigningContext>()
    .AddManagedProtector<PaymentGatewayContext>()
    .AddManagedProtector<OAuthClientSecretContext>();
```

### 4. Define secret domains

```csharp
// Empty marker classes — the type parameter enforces domain separation at compile time.
public sealed class WebhookSigningContext    : ISecretContext { }
public sealed class PaymentGatewayContext    : ISecretContext { }
public sealed class OAuthClientSecretContext : ISecretContext { }
```

An `EncryptedSecret<WebhookSigningContext>` cannot be passed to an
`ISecretProtector<PaymentGatewayContext>` — the mismatch is a compile error.

### 5. Use the protector

Inject `ISecretProtector<TContext>` wherever you need to encrypt or decrypt:

```csharp
public class WebhookService(ISecretProtector<WebhookSigningContext> protector)
{
    public async Task StoreSecretAsync(Webhook webhook, string rawSecret)
    {
        // Encrypt
        EncryptedSecret<WebhookSigningContext> encrypted = protector.Protect(rawSecret);

        webhook.EncryptedSecret  = encrypted.Blob.ToStorageString();
        webhook.SecretKeyVersion = encrypted.KeyVersion.Value;

        // (or use the EF value converter below — no manual serialization needed)
    }

    public string RevealSecret(Webhook webhook)
    {
        var encrypted = EncryptedSecret<WebhookSigningContext>.FromPersisted(
            webhook.EncryptedSecret, webhook.SecretKeyVersion);

        // Decrypts into secure unmanaged memory — dispose when done.
        using Secret<byte> plain = protector.Unprotect(encrypted);
        return plain.Expose(bytes => System.Text.Encoding.UTF8.GetString(bytes));
    }
}
```

### 6. Implement IDataRotator

The data rotator re-encrypts records that were encrypted with an older key version:

```csharp
public sealed class WebhookDataRotator(
    AppDbContext db,
    ISecretProtector<WebhookSigningContext> protector) : IDataRotator<WebhookSigningContext>
{
    public async Task<int> RotateBatchAsync(int batchSize, CancellationToken ct = default)
    {
        var stale = await db.Webhooks
            .Where(w => w.SecretKeyVersion < (int)protector.CurrentKeyVersion)
            .Take(batchSize)
            .ToListAsync(ct);

        foreach (var webhook in stale)
        {
            var encrypted = EncryptedSecret<WebhookSigningContext>.FromPersisted(
                webhook.EncryptedSecret, webhook.SecretKeyVersion);

            var rotated = protector.Rotate(encrypted);

            webhook.EncryptedSecret  = rotated.Blob.ToStorageString();
            webhook.SecretKeyVersion = rotated.KeyVersion.Value;
        }

        await db.SaveChangesAsync(ct);
        return stale.Count;
    }

    public async Task<bool> IsCompleteAsync(CancellationToken ct = default)
        => !await db.Webhooks.AnyAsync(
            w => w.SecretKeyVersion < (int)protector.CurrentKeyVersion, ct);
}
```

### 7. Add a migration

```bash
dotnet ef migrations add AddEncryptionKeys
dotnet ef database update
```

---

## Key Rotation

### Automatic rotation

`RotationBackgroundService<TContext>` runs in the background. Every `CheckInterval` (default 6 h) it:

1. Loads the current active key from the store.
2. Checks if its age exceeds `RotationInterval` (default 90 days).
3. If yes, calls `KeyRotationService<TContext>.RotateIfNeededAsync()`, which:
   - Generates a new random AES key.
   - Wraps it with the master key and persists it to the database.
   - Marks the previous key as retired in the database.
   - Hot-reloads the in-memory key ring (atomic swap, no downtime).
   - Optionally runs `IDataRotator<TContext>` in batches.

Transient errors (database unavailable, network blip) are caught, logged, and retried after `RetryIntervalOnError` (default 5 min) rather than crashing.

### Forced rotation

Trigger an immediate rotation from an admin endpoint:

```csharp
app.MapPost("/admin/keys/rotate", async (
    [FromServices] KeyRotationService<WebhookSigningContext> rotationService,
    CancellationToken ct) =>
{
    await rotationService.ForceRotateAsync(ct);
    return Results.Ok(new { rotated = true });
}).RequireAuthorization("Admin");
```

> `KeyRotationService<TContext>` is **scoped**. Resolve it from a scoped context (e.g. a controller or a manually created scope) — do not inject it into a singleton.

### Key lifecycle

```
Day   0:  Key v1 created (active)
Day  90:  Key v2 created (active), v1 retired → still used for decryption
Day 180:  Key v3 created (active), v2 retired
          Once all v1 ciphertext has been re-encrypted, v1 can be deleted from DB.
```

Retired keys stay in the database until **all** records referencing them have been re-encrypted by `IDataRotator<TContext>`. Only then is it safe to delete them.

---

## Master Key Providers

### Environment variable (dev/staging)

```csharp
.AddEnvironmentMasterKey("APP_MASTER_KEY")  // default variable name: APP_MASTER_KEY
```

```bash
export APP_MASTER_KEY="$(openssl rand -base64 32)"
```

### IConfiguration / appsettings

```csharp
.AddConfigurationMasterKey("Security:MasterKey")  // default config path
```

> Never commit a real key to source control. Use [.NET User Secrets](https://docs.microsoft.com/aspnet/core/security/app-secrets) for local development.

### File (Docker Secrets, Kubernetes Secrets)

```csharp
.AddFileMasterKey("/run/secrets/app_master_key")
```

The file must contain a Base64-encoded key, optionally with leading/trailing whitespace.

### Custom provider (Azure Key Vault, AWS KMS)

Implement `IMasterKeyProvider`:

```csharp
public sealed class AzureKeyVaultMasterKeyProvider(SecretClient client, string secretName)
    : IMasterKeyProvider
{
    public async ValueTask<MasterKey> GetMasterKeyAsync(CancellationToken ct = default)
    {
        KeyVaultSecret kvSecret = await client.GetSecretAsync(secretName, cancellationToken: ct);

        byte[] keyBytes = Convert.FromBase64String(kvSecret.Value);
        try
        {
            return new MasterKey(Secret<byte>.From(keyBytes));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(keyBytes);
        }
    }
}
```

Register it:

```csharp
.AddMasterKeyProvider<AzureKeyVaultMasterKeyProvider>()
```

or with a factory:

```csharp
builder.Services.AddSingleton<IMasterKeyProvider>(sp =>
{
    var client = sp.GetRequiredService<SecretClient>();
    return new AzureKeyVaultMasterKeyProvider(client, "app-master-key");
});
```

---

## EF Core Mapping

### Single-column storage

`EncryptedSecretValueConverter<TContext>` serialises an `EncryptedSecret<TContext>` into a single string column using the format `{version}:{base64blob}`.

```csharp
// In OnModelCreating or IEntityTypeConfiguration:
builder.Property(x => x.EncryptedApiKey)
       .HasEncryptedSecretConversion<ApiKeyContext>();
```

For nullable properties:

```csharp
builder.Property(x => x.EncryptedApiKey)   // EncryptedSecret<ApiKeyContext>?
       .HasEncryptedSecretConversion<ApiKeyContext>();
```

### Two-column storage

If you prefer storing the version and blob in separate columns (better for queries / indexes), skip the converter and map manually:

```csharp
// Entity
public string  EncryptedSecret  { get; set; } = string.Empty;
public int     SecretKeyVersion { get; set; }

// Reading
var secret = EncryptedSecret<WebhookSigningContext>.FromPersisted(
    entity.EncryptedSecret, entity.SecretKeyVersion);

// Writing
entity.EncryptedSecret  = encrypted.Blob.ToStorageString();
entity.SecretKeyVersion = encrypted.KeyVersion.Value;
```

### Convention-based mapping

To automatically apply the converter to all `EncryptedSecret<TContext>` properties in the model:

```csharp
protected override void ConfigureConventions(ModelConfigurationBuilder configBuilder)
{
    configBuilder.Properties<EncryptedSecret<WebhookSigningContext>>()
                 .HaveEncryptedSecretConversion<WebhookSigningContext>();
}
```

---

## Health Checks

Register a health check for each protected context:

```csharp
builder.Services
    .AddWiaojSecurity()
    // ...
    .AddManagedProtector<WebhookSigningContext>()
    .AddSecurityHealthCheck<WebhookSigningContext>(
        name: "security_webhook",
        tags: ["ready", "security"]);

builder.Services.AddHealthChecks(); // required if not already registered
```

Map health endpoints:

```csharp
app.MapHealthChecks("/health/live",  new() { Predicate = r => r.Tags.Contains("live") });
app.MapHealthChecks("/health/ready", new() { Predicate = r => r.Tags.Contains("ready") });
```

Reported statuses:

| Status | Meaning |
|--------|---------|
| `Healthy` | Key ring loaded; active key is within its rotation window |
| `Degraded` | Key ring loaded but the active key is overdue for rotation, or the store is temporarily unreachable |
| `Unhealthy` | Key ring was never successfully initialized (startup failure) |

---

## Observability (OpenTelemetry)

All instruments are exposed on the meter named **`Wiaoj.Security`**.

```csharp
services.AddOpenTelemetry()
        .WithMetrics(m => m.AddMeter(SecurityMeter.Name));
```

| Instrument | Type | Description |
|------------|------|-------------|
| `wiaoj.security.protect.count` | Counter | Successful Protect calls |
| `wiaoj.security.protect.error.count` | Counter | Failed Protect calls |
| `wiaoj.security.protect.duration` | Histogram (ms) | Protect latency |
| `wiaoj.security.unprotect.count` | Counter | Successful Unprotect calls |
| `wiaoj.security.unprotect.error.count` | Counter | Failed Unprotect calls (auth failures, tampering) |
| `wiaoj.security.unprotect.duration` | Histogram (ms) | Unprotect latency |
| `wiaoj.security.rotation.count` | Counter | Completed key rotation cycles |
| `wiaoj.security.rotation.error.count` | Counter | Failed rotation cycles |
| `wiaoj.security.rotation.duration` | Histogram (ms) | Full rotation cycle duration |
| `wiaoj.security.keyring.reload.count` | Counter | Key ring reload operations (startup + post-rotation) |

All instruments carry a `context` tag (e.g. `"WebhookSigningContext"`) so you can break down dashboards per domain.

---

## Configuration Reference

All options live in `KeyRotationOptions`. Configure via the delegate or bind from `IConfiguration`:

| Property | Default | Description |
|----------|---------|-------------|
| `RotationInterval` | `90 days` | How long a key stays active before being rotated |
| `CheckInterval` | `6 hours` | How often the background service checks if rotation is needed |
| `RetryIntervalOnError` | `5 minutes` | Wait time after a failed rotation check before retrying |
| `KeySizeInBits` | `256` | AES key size: `128`, `192`, or `256` |
| `AutoRotateData` | `true` | Whether to run `IDataRotator<T>` after each rotation |
| `DataRotationBatchSize` | `100` | Records re-encrypted per batch |
| `DataRotationBatchDelay` | `50 ms` | Delay between batches to avoid saturating the database |

Validation runs at startup (`ValidateOnStart`). Invalid values throw during `IHost.Build()`.

---

## Database Schema

The `EncryptionKeyRecord` entity maps to a single table (default name: class name, customizable via EF conventions).

| Column | Type | Description |
|--------|------|-------------|
| `Id` | `uuid` | UUIDv7 primary key (time-ordered) |
| `ContextName` | `varchar(256)` | Name of the `ISecretContext` type, e.g. `"WebhookSigningContext"` |
| `Version` | `int` | Monotonically increasing version number per context |
| `WrappedKeyMaterial` | `varchar(1024)` | Base64Url(nonce[12] \| auth_tag[16] \| ciphertext[N]) — the DEK encrypted with the master key |
| `CreatedAt` | `datetimeoffset` | UTC creation timestamp |
| `RetiredAt` | `datetimeoffset?` | UTC retirement timestamp; `NULL` = still active |

Indexes created automatically by `EncryptionKeyRecordConfiguration`:

- **Unique** on `(ContextName, Version)` — prevents duplicate versions.
- **Lookup** on `ContextName` — for fast per-context queries.

The master key **never** appears in the database.

---

## Security Notes

- **AES-GCM authentication:** Every ciphertext includes a 128-bit authentication tag. Tampered or corrupt blobs throw `CryptographicException` on decryption.
- **Strict Context Identity:** Since the `TContext` type name is used as Associated Authenticated Data (AAD), renaming a context class (e.g. `WebhookPayloadContext` to `PayloadContext`) will cause decryption to fail for all existing records. Do not rename context classes without a data migration plan.
- **Master key storage:** In production, always source the master key from a KMS (Azure Key Vault, AWS KMS, HashiCorp Vault). The environment variable and configuration providers are for development and staging only.
- **Key material in memory:** DEKs and the master key are held in unmanaged memory (`Secret<T>`) and zeroed on disposal via `CryptographicOperations.ZeroMemory`. Do not copy key bytes into managed arrays without zeroing them immediately after use.
- **Log safety:** `CipherBlob.ToString()` returns `"[CIPHER_BLOB]"`. `EncryptedSecret<T>.ToString()` returns `"[ENCRYPTED_SECRET<TContext> key_vN]"`. Neither ever includes ciphertext.
- **Retired keys:** Keep retired keys in the database until `IDataRotator<T>.IsCompleteAsync()` returns `true`. Deleting a retired key before all data has been re-encrypted will make those records permanently unreadable.
- **Compile-time domain isolation:** An `EncryptedSecret<WebhookSigningContext>` cannot be accidentally decrypted by an `ISecretProtector<PaymentGatewayContext>` — the type system prevents this at compile time.

---

## AES Key Sizes

AES supports **128 / 192 / 256-bit keys only**.

| Size | Notes |
|------|-------|
| 128-bit | Acceptable for low-sensitivity data |
| 192-bit | Rarely used in practice |
| **256-bit** | **Default. NSA Suite B, recommended for all new deployments.** |

Configure via `KeySizeInBits` in `KeyRotationOptions`.