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
public class MyDbContext : DbContext, IOutboxDbContext
{
    public DbSet<User> Users { get; set; }

    public MyDbContext(DbContextOptions<MyDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Creates the '_CorvusOutboxMessages' table
        modelBuilder.ApplyCorvusOutbox(new EfCoreOutboxConfiguration(null!)); 
        base.OnModelCreating(modelBuilder);
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        // This is crucial! It injects Audit and Dispatcher interceptors.
        // In a real app, pass the IServiceProvider from DI.
        // optionsBuilder.UseDddInterceptors(serviceProvider); 
    }
}
```

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
4.  **Background Processor:** The `OutboxProcessor` background service polls the table, deserializes the events, and executes `IPostDomainEventHandler`s.

### Serialization flexibility
Unlike other libraries that force a specific JSON library, **Wiaoj.Ddd** leverages `Wiaoj.Serialization`. You can store your outbox payloads using:
*   `System.Text.Json` (Default recommendation)
*   `MessagePack` (For smaller payload size)
*   `MongoDB.Bson`
*   `YamlDotNet`

### ⚠️ Known Limitations
*   **Outbox Concurrency:** The built-in `OutboxProcessor` in this package uses a simple polling mechanism intended for **single-instance deployments**. If you deploy multiple replicas (e.g., Kubernetes), you may encounter race conditions where the same message is processed twice.
    *   *Solution:* For high-scale, distributed environments, please upgrade to **[Wiaoj.Corvus.Outbox](https://github.com/wiaoj/corvus)**, which implements `SKIP LOCKED` and distributed locking strategies.

## 📄 License

Licensed under the MIT License.