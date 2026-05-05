# Wiaoj.Security — Automatic Key Rotation

## Architecture

```
App Startup
    │
    ▼
KeyRingLoader<TContext>
    │  Loads EncryptionKeyRecord rows from DB
    │  Unwraps each key with MasterKeyWrapper (AES-GCM)
    │  → Builds KeyRing<TContext>
    │
    ▼
ManagedSecretProtector<TContext>   ← singleton, hot-reloadable
    │  Wraps SecretProtector<TContext>
    │  Injected as ISecretProtector<TContext>
    │
    │  (every CheckInterval — default 6h)
    ▼
RotationBackgroundService<TContext>
    │  Creates scoped KeyRotationService<TContext>
    │
    ▼
KeyRotationService<TContext>
    ├─ RotateIfNeededAsync()   ← checks key age vs RotationInterval
    └─ ForceRotateAsync()      ← manual endpoint / admin trigger
         │
         ├─ 1. Generate new AES key
         ├─ 2. Wrap with master key → save to DB
         ├─ 3. Retire old key in DB
         ├─ 4. ManagedSecretProtector.ReloadAsync() → atomic swap
         └─ 5. IDataRotator<TContext>.RotateBatchAsync() (if registered)
```

---

## ⚠️ AES Key Sizes

AES supports **128 / 192 / 256-bit keys only**. There is no AES-512.

| Size | Security | Notes |
|------|----------|-------|
| 128-bit | ~128-bit classical | Acceptable for low-sensitivity data |
| 192-bit | ~192-bit classical | Rarely used |
| **256-bit** | **~256-bit classical** | **Default. NSA Suite B, recommended.** |

Configure via `KeySizeInBits` (see Setup below).

---

## Setup

### 1. Apply EF Core migration

Implement `IEncryptionKeyDbContext` on your `DbContext`:

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

Then add and apply a migration:
```bash
dotnet ef migrations add AddEncryptionKeys
dotnet ef database update
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

Store it in your secrets manager, NOT in source control or appsettings.json.

### 3. Register services

```csharp
// Program.cs
builder.Services
    // Master key source (swap for Azure Key Vault / AWS KMS in production)
    .AddEnvironmentMasterKeyProvider("APP_MASTER_KEY")

    // Register the full rotation system for each context
    .AddManagedSecretProtector<WebhookSigningContext, AppDbContext>(opts =>
    {
        opts.RotationInterval      = TimeSpan.FromDays(90); // change key every 90 days
        opts.CheckInterval         = TimeSpan.FromHours(6); // wake up every 6h to check
        opts.KeySizeInBits         = 256;                   // 128, 192, or 256
        opts.AutoRotateData        = true;                  // re-encrypt data after key rotation
        opts.DataRotationBatchSize = 100;
    })

    // Wire up your IDataRotator implementation
    .AddDataRotator<WebhookSigningContext, WebhookDataRotator>();
```

### 4. Define your secret domains

```csharp
// Empty marker classes — enforced by the type system at compile time
public sealed class WebhookSigningContext   : ISecretContext { }
public sealed class PaymentGatewayContext   : ISecretContext { }
public sealed class OAuthClientSecretContext : ISecretContext { }
```

### 5. Implement IDataRotator

```csharp
public sealed class WebhookDataRotator : IDataRotator<WebhookSigningContext>
{
    private readonly AppDbContext _db;
    private readonly ISecretProtector<WebhookSigningContext> _protector;

    public WebhookDataRotator(AppDbContext db, ISecretProtector<WebhookSigningContext> protector)
    {
        _db        = db;
        _protector = protector;
    }

    public async Task<int> RotateBatchAsync(int batchSize, CancellationToken ct)
    {
        var stale = await _db.Webhooks
            .Where(w => w.SecretKeyVersion < (int)_protector.CurrentKeyVersion)
            .Take(batchSize)
            .ToListAsync(ct);

        foreach (var webhook in stale)
        {
            var encrypted = EncryptedSecret<WebhookSigningContext>.FromPersisted(
                webhook.EncryptedSecret, webhook.SecretKeyVersion);

            var rotated = _protector.Rotate(encrypted);

            webhook.EncryptedSecret  = rotated.Blob.ToStorageString();
            webhook.SecretKeyVersion = rotated.KeyVersion.Value;
        }

        await _db.SaveChangesAsync(ct);
        return stale.Count;
    }

    public async Task<bool> IsCompleteAsync(CancellationToken ct)
        => !await _db.Webhooks.AnyAsync(
            w => w.SecretKeyVersion < (int)_protector.CurrentKeyVersion, ct);
}
```

### 6. Manual / admin-triggered rotation

```csharp
// Controller or minimal API endpoint
app.MapPost("/admin/keys/rotate/{context}", async (
    string context,
    KeyRotationService<WebhookSigningContext> svc,
    CancellationToken ct) =>
{
    await svc.ForceRotateAsync(ct);
    return Results.Ok(new { rotated = true });
}).RequireAuthorization("Admin");
```

---

## Custom Master Key Provider (production)

```csharp
// Azure Key Vault example
public sealed class AzureKeyVaultMasterKeyProvider : IMasterKeyProvider
{
    private readonly SecretClient _client;
    private readonly string _secretName;

    public AzureKeyVaultMasterKeyProvider(SecretClient client, string secretName)
    {
        _client     = client;
        _secretName = secretName;
    }

    public async ValueTask<Secret<byte>> GetMasterKeyAsync(CancellationToken ct = default)
    {
        KeyVaultSecret secret = await _client.GetSecretAsync(_secretName, cancellationToken: ct);
        byte[] keyBytes = Convert.FromBase64String(secret.Value);
        try
        {
            return Secret<byte>.From(keyBytes.AsSpan());
        }
        finally
        {
            CryptographicOperations.ZeroMemory(keyBytes);
        }
    }
}

// Registration
builder.Services.AddMasterKeyProvider<AzureKeyVaultMasterKeyProvider>();
```

---

## Key lifecycle diagram

```
Day 0:    Key v1 created  (active)
Day 90:   Key v2 created  (active), v1 retired (can still decrypt old data)
Day 180:  Key v3 created  (active), v2 retired
          Once all v1 data is re-encrypted, v1 can be deleted from DB

          Old retired keys stay in the DB until ALL data referencing them
          has been re-encrypted. After that they are safe to remove.
```

---

## What goes in the DB

| Column | Type | Contents |
|--------|------|----------|
| `context_name` | `varchar(256)` | E.g. `"WebhookSigningContext"` |
| `version` | `int` | `1`, `2`, `3`, … |
| `wrapped_key_material` | `varchar(1024)` | Base64(nonce\|tag\|ciphertext) — encrypted with master key |
| `created_at` | `datetime` | UTC creation time |
| `retired_at` | `datetime?` | UTC retirement time (null = still active) |

The master key **never** touches the database.