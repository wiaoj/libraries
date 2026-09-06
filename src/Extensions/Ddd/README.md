# Wiaoj.Ddd

**Wiaoj.Ddd** is a comprehensive, high-performance Domain-Driven Design (DDD) framework for .NET. It provides the essential building blocks for implementing complex business logic while handling cross-cutting concerns like Domain Events, Audit Logging, and the Transactional Outbox pattern seamlessly.

Built on top of the **Wiaoj Ecosystem** (Primitives, Serialization, Extensions), it ensures type safety, zero-allocation best practices, and modular architecture.

## 🌟 Key Features

*   **🧱 Core Building Blocks:** Robust base classes for `Aggregate<TId>`, `Entity<TId>`, and `ValueObject`.
*   **📣 Domain Events System:**
    *   **Pre-Commit Handlers:** Run logic *within* the same transaction (e.g., validation, cascade updates).
    *   **Post-Commit Handlers:** Run logic *after* the transaction commits (via Outbox).
*   **📦 Transactional Outbox Pattern:**
    *   Automatically captures Domain Events during `SaveChanges`.
    *   Serializes events using **Wiaoj.Serialization** (System.Text.Json, MessagePack, etc.).
    *   Background processor guarantees at-least-once delivery.
*   **🕵️ Audit Logging:** Automatic tracking of `CreatedAt`, `UpdatedAt`, and `DeletedAt` (Soft Delete) via EF Core Interceptors.
*   **🔌 Pluggable Serialization:** Decoupled from specific serialization libraries. Use System.Text.Json, MessagePack, or Bson via configuration.

## 📦 Installation

```bash
# Core Abstractions & Logic
dotnet add package Wiaoj.Ddd

# Entity Framework Core Integration (Outbox & Interceptors)
dotnet add package Wiaoj.Ddd.EntityFrameworkCore
```

## 🚀 Quick Start

### 1. Define Your Domain Model

Create your Aggregates and Domain Events using the provided base classes.

```csharp
using Wiaoj.Ddd.Abstractions;
using Wiaoj.Ddd.Abstractions.DomainEvents;

// 1. Define a Domain Event
public sealed record UserRegisteredEvent(Guid UserId, string Email) : DomainEvent;

// 2. Define an Aggregate Root
public class User : Aggregate<UserId> // UserId is a strong typed Value Object
{
    public string Email { get; private set; }
    public string Name { get; private set; }

    // Enforce invariants in the constructor
    public User(UserId id, string email, string name) : base(id)
    {
        Email = email;
        Name = name;

        // Raise a domain event
        RaiseDomainEvent(new UserRegisteredEvent(id.Value, email));
    }

    public void UpdateName(string newName)
    {
        Name = newName;
        // CreatedAt, UpdatedAt are handled automatically by the AuditInterceptor
    }
}
```

### 2. Implement Event Handlers

Handle events either synchronously before commit or asynchronously after commit.

```csharp
// Runs BEFORE the DB transaction commits.
// Good for: Validations, updating other aggregates in the same transaction.
public class UserValidationHandler : IPreDomainEventHandler<UserRegisteredEvent>
{
    public ValueTask Handle(UserRegisteredEvent @event, CancellationToken ct)
    {
        // Logic here...
        return ValueTask.CompletedTask;
    }
}

// Runs AFTER the DB transaction commits (via Outbox Processor).
// Good for: Sending emails, publishing to Message Bus (RabbitMQ/Kafka).
public class WelcomeEmailHandler : IPostDomainEventHandler<UserRegisteredEvent>
{
    public async ValueTask Handle(UserRegisteredEvent @event, CancellationToken ct)
    {
        await _emailService.SendWelcomeAsync(@event.Email);
    }
}
```

### 3. Configure Dependency Injection

Wire everything up in your `Program.cs`.

```csharp
using Wiaoj.Serialization.DependencyInjection; // For UseSystemTextJson

var builder = WebApplication.CreateBuilder(args);

// Register DDD Services
builder.Services.AddDdd(ddd =>
{
    // Auto-scan assemblies for Event Handlers
    ddd.ScanAssemblies(ServiceLifetime.Scoped, typeof(Program).Assembly);
})
.AddEntityFrameworkCore<MyDbContext>(
    // 1. Configure Serialization (Mandatory for flexibility)
    configureSerializer: serializer => 
    {
        // Use System.Text.Json (or MessagePack/Bson) for Outbox payload
        serializer.UseSystemTextJson<DddEfCoreOutboxSerializerKey>(); 
    },
    // 2. Configure Outbox Options (Optional)
    configureOutbox: options =>
    {
        options.BatchSize = 50;
        options.PollingInterval = TimeSpan.FromSeconds(2);
    }
);
```

### 4. Setup DbContext

Apply the necessary configurations to your `DbContext`.

```csharp
public class MyDbContext : DbContext
{
    public DbSet<User> Users { get; set; }

    public MyDbContext(DbContextOptions<MyDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Maps the outbox table and the indexes the claim query needs.
        modelBuilder.ApplyDddOutbox();
    }
}
```

The table name, schema and index strategy are configurable, because they are fixed when the model is built:

```csharp
modelBuilder.ApplyDddOutbox(outbox =>
{
    outbox.TableName = "outbox_messages";
    outbox.Schema = "messaging";
    outbox.PendingIndexFilter = null;    // partial-index SQL names columns and dialects; supply your own if wanted
});
```

Runtime behaviour — polling interval, batch size, retry policy, lock duration — stays in `OutboxOptions`, since it can change under a running process while the schema cannot.

> **Attach the interceptors when registering the DbContext.** EF Core does not
> reliably auto-discover DI-registered interceptors for every mode (notably
> `AddDbContextFactory` and pooling), so call `UseDddInterceptors(sp)` inside the
> registration delegate. This works the same for `AddDbContext`,
> `AddDbContextPool`, and `AddDbContextFactory`:
>
> ```csharp
> services.AddDbContextFactory<MyDbContext>((sp, options) => options
>     .UseNpgsql(connectionString)
>     .UseDddInterceptors<MyDbContext>(sp)); // attaches only MyDbContext's interceptors
> ```
>
> Only the interceptors belonging to the specified context are attached, so in a
> multi-context app each context opts in independently and no per-save context
> filtering is needed.

---

## 🏗️ Architecture & Concepts

### The Transactional Outbox
When you call `SaveChangesAsync()`:
1.  **AuditInterceptor:** Updates `CreatedAt` / `UpdatedAt` timestamps automatically.
2.  **DomainEventDispatcherInterceptor:**
    *   Detects aggregates with pending events.
    *   Executes `IPreDomainEventHandler`s immediately.
    *   Serializes events and saves them to the `OutboxMessage` table within the **same transaction**.
3.  **Commit:** The Aggregate changes and the Outbox messages are committed atomically.
4.  **Background Processor:** The `OutboxProcessor` background service claims a batch, deserializes each event, and runs the one `IPostDomainEventHandler` its row names.

### One row per (event, handler)

An event with five post-commit handlers writes five rows, not one. Each is claimed, retried and dead-lettered on its own.

This is what confines a failure to the handler that failed. With a single row per event, a handler that throws sends the *whole* event back to the queue, so the four handlers that already succeeded run again on every retry — which silently requires every handler to be idempotent, whether or not anyone wrote that down.

The trade-off worth knowing: the handler set is read when the row is written. A handler introduced by a later deployment does not retroactively gain rows for events already enqueued. In practice the queue drains in seconds, so the window is a deployment, and a new handler processing historical events is usually not wanted anyway — that is replay, and replay should be deliberate.

### Aliases, not type names

Rows outlive refactors, so both the event and the handler are named by a stable alias rather than a CLR type name:

```csharp
[DomainEventAlias("orders.created.v1")]
public sealed record OrderCreated(...) : IDomainEvent;

[DomainEventHandlerAlias("orders.notify-customer")]
public sealed class NotifyCustomer : IPostDomainEventHandler<OrderCreated> { ... }
```

Without an alias the fallback is the type's full name, which survives an assembly rename and every version bump but still breaks when you move the type to another namespace. Give anything that reaches the outbox an alias and version it; renaming the type and hoping is how queued rows become unresolvable.

### Retry, backoff and dead-letter

A failed row is retried after an exponential backoff (`NextAttemptAtTicks`), and once its attempts run out it is **dead-lettered** — an explicit terminal state carrying the last error.

That last part matters: a row whose retries are exhausted must not simply stop matching the claim predicate and vanish. `DeadLetteredAtTicks` is queryable, alertable, and tells you the difference between "done" and "given up on":

```csharp
var stuck = await db.Set<OutboxMessage>()
    .Where(m => m.DeadLetteredAtTicks != null)
    .ToListAsync();
```

An event type or handler that no longer resolves is dead-lettered immediately rather than retried — no number of attempts brings back a deleted handler.

### Claiming, and why it is the one query written per provider

Everything the outbox does is LINQ except the claim, which is written by hand for each provider:

| Provider | Claim |
| --- | --- |
| PostgreSQL | `FOR UPDATE SKIP LOCKED` + `RETURNING *` |
| SQL Server | `ROWLOCK, UPDLOCK, READPAST` + `OUTPUT INSERTED.*` |
| SQLite | subquery + `RETURNING *` |
| In-memory | in-process claim (test provider) |
| anything else | fails loudly |

`ExecuteUpdate` emits a plain `UPDATE`: there is no way to ask for skip-locked semantics, so concurrent processors either block on each other's row locks or claim overlapping candidate sets and lose the update. It also cannot return what it updated, which costs a second round trip to read back the rows just claimed. Both are avoided by writing that one statement per dialect.

An unrecognised provider throws rather than falling back to a non-atomic claim. A silent fallback would appear to work and would hand the same row to several processors under load, surfacing as duplicated side effects far from the cause.

### Naming conventions

Every identifier the claim statement writes — the table, the schema and each column — is read from the EF model, so a context using a naming convention works unchanged:

```csharp
optionsBuilder.UseNpgsql(cs).UseSnakeCaseNamingConvention();
```

The one thing the library cannot write for you is the partial-index filter, because raw SQL has to name columns rather than properties. `PendingIndexFilter` is therefore null by default; set it if you want one, spelled the way your own model maps:

```csharp
outbox.PendingIndexFilter = "\"processed_at_ticks\" IS NULL AND \"dead_lettered_at_ticks\" IS NULL";
```

### Aliases and the fallback

Without an attribute the alias is a compact, stable form of the type name: namespace, type name, and generic arguments by the same rule. It deliberately carries no assembly, version or culture — `Type.FullName` embeds all three for a closed generic, which both blows past any sane column width (400+ characters for a two-argument type) and changes on a version bump, orphaning rows already written.

It still changes if a type moves namespace, which is what the attribute is for.

### Timestamps

Timestamps are stored as UTC ticks rather than `DateTimeOffset`, because the claim query does nothing but order and compare on them and not every provider can translate that on a `DateTimeOffset` column — SQLite refuses both. Integers work everywhere and index more cheaply.

### Serialization flexibility
Unlike other libraries that force a specific JSON library, **Wiaoj.Ddd** leverages `Wiaoj.Serialization`. You can store your outbox payloads using:
*   `System.Text.Json` (Default recommendation)
*   `MessagePack` (For smaller payload size)
*   `MongoDB.Bson`
*   `YamlDotNet`

### DbContext Registration Modes

The Outbox and Domain Event infrastructure (interceptors, dispatcher, background
processor) is registered as stateless singletons and works identically across
**all** EF Core DbContext registration modes — `AddDbContext` (scoped),
`AddDbContextPool` (pooled), and `AddDbContextFactory`.

There is one constraint to be aware of:

*   **Repositories and `IUnitOfWork` require scoped registration (`AddDbContext`).**
    `EfcoreRepository<TContext, ...>` resolves its `DbContext` from DI, so it only
    works when the context is registered as a scoped service. Under
    `AddDbContextFactory`/pooled, the context is created by the factory and is not
    available in the container, so repositories cannot be constructed.
*   **Pre-commit handlers should write through `IUnitOfWork` for cross-mode safety.**
    A pre-commit handler that injects `TContext` (or a repository bound to it)
    directly will receive the wrong/no context under factory/pooled mode, so its
    changes would not join the active transaction. Injecting `IUnitOfWork` resolves
    the *live* context (the one being saved) via the ambient holder, so staged
    changes — including plain `Add` without an inner `SaveChanges` — commit
    atomically with the aggregate in every mode.

> **Rule of thumb:** if you use repositories or the Unit of Work, register your
> `DbContext` with `AddDbContext`. If you only need the Outbox/event dispatching,
> any registration mode works.

### ⚠️ Known Limitations

*   **Provider coverage.** PostgreSQL, SQL Server and SQLite have hand-written claim statements; the in-memory provider has a test-grade one. Any other provider throws on the first claim rather than guessing. PostgreSQL and SQL Server are covered by tests that pin the shape of their SQL — skip-locked semantics, single statement, parameters bound rather than interpolated — but are not executed against a live server here.
*   **Fan-out is fixed at enqueue time.** See *One row per (event, handler)* above.
*   **Synchronous `SaveChanges()` is not intercepted.** Both interceptors override only the async path, so a synchronous save enqueues nothing and stamps nothing.

## 📄 License

Licensed under the MIT License.