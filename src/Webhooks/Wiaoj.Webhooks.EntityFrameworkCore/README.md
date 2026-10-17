# Wiaoj.Webhooks.EntityFrameworkCore

Durable stores for Wiaoj.Webhooks. The in-memory stores lose pending jobs, retries and subscriptions when the process restarts; these keep them in your application's own `DbContext`, so stale-job recovery has something to recover.

| Store | Keeps |
|---|---|
| `IWebhookStore` | delivery jobs, their attempts, and the lease that says which instance is delivering a job |
| `IWebhookSubscriptionStore` | publishing subscriptions (Wiaoj.Webhooks.Publishing) |
| `IWebhookBatchStore` | publish batches, so a crash in the middle of fanning an event out to its subscribers does not lose the rest |

## Setup

**1. Map the tables.** Call `ApplyWebhooks()` from your context's `OnModelCreating`. The context needs no `DbSet` properties for them.

```csharp
public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options) {
    protected override void OnModelCreating(ModelBuilder modelBuilder) {
        modelBuilder.ApplyWebhooks(options => options.Schema = "webhooks");
    }
}
```

`WebhookModelOptions` sets the schema, the four table names and the column lengths. They are fixed when the model is built.

**2. Register the stores.**

```csharp
services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));

services.AddWebhooks(webhooks => {
    webhooks
        .UseEntityFrameworkCoreStore<AppDbContext>()
        .UseStaleJobRecovery()
        .AddPublishing(publishing => publishing.UseEntityFrameworkCoreStore<AppDbContext>());
});
```

`UseEntityFrameworkCoreStore` on the webhook builder registers the job store. On the publishing builder it registers the subscription and batch stores, and batch recovery. Create the tables with your normal migrations.

Register your context with `AddDbContext` as usual. The stores are singletons, and a `DbContext` is scoped and not thread-safe, so each store call opens its own service scope and resolves a fresh context from it; a context is never shared between concurrent deliveries. A context that did not call `ApplyWebhooks()` fails on first use with a message that says so.

## What the stores guarantee

These are the semantics of the in-memory stores, held by one shared contract suite that runs against both:

- **A lease is claimed atomically.** The whole rule is the `WHERE` clause of one `UPDATE`: the job is unfinished and its lease is free, expired, or already yours. Of any number of racing instances, exactly one gets an affected row. A delivered or dead-lettered job can never be leased.
- **Leaving in-flight releases the lease.** Moving a job to retrying, delivered or dead-lettered clears its holder.
- **Stale jobs are found in the database.** An in-flight job whose lease expired, a queued job nobody picked up, and a retrying job whose retry is due all come back from the stale query, which recovery after a crash uses. A queued or retrying job that still holds an unexpired lease is being handled and is not stale.
- **Timestamps compare correctly.** They are stored as UTC ticks, not provider date types, so lease and recovery comparisons mean the same on SQLite and on PostgreSQL.

## Tests

`tests/Webhooks/Wiaoj.Webhooks.EntityFrameworkCore.Tests.Integration` runs the store contract against the in-memory stores, EF Core on SQLite in memory, and EF Core on PostgreSQL. The PostgreSQL run is off unless `WIAOJ_TEST_POSTGRES` holds a connection string to a server the tests may create databases on:

```bash
podman run -d --rm -p 55432:5432 -e POSTGRES_PASSWORD=wiaoj postgres:17-alpine
export WIAOJ_TEST_POSTGRES="Host=localhost;Port=55432;Username=postgres;Password=wiaoj"
dotnet test --project tests/Webhooks/Wiaoj.Webhooks.EntityFrameworkCore.Tests.Integration
```

A test fails when a public property is added to `WebhookJobRecord` and the store does not persist it.
