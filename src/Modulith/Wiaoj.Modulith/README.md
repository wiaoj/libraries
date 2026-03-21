# Wiaoj.Modulith

Modular monolith infrastructure for .NET. Each bounded context defines its own `IModule`; service registration, lifecycle hooks, and conditional loading are managed through a single consistent API.

This package targets the generic host only — no ASP.NET Core dependency. For middleware and endpoint configuration in a `WebApplication`, add `Wiaoj.Modulith.AspNetCore`.

---

## Installation

```bash
dotnet add package Wiaoj.Modulith
```

---

## Concepts

### `IModule`

The smallest unit. Its sole responsibility is registering its own services into the DI container.

```csharp
public sealed class OrdersModule : IModule {
    public string Name => "Orders";

    public void Register(IServiceCollection services, IConfiguration configuration) {
        services.AddScoped<IOrderService, OrderService>();
        services.AddDbContext<OrdersDbContext>(opt =>
            opt.UseSqlServer(configuration.GetConnectionString("Orders")));
    }
}
```

Rules:

- The class must be `sealed`. Modules are not designed for inheritance.
- A public parameterless constructor is required. Module instances are created via `Activator.CreateInstance`.
- `Register` must contain only service registrations. Business logic must not run here.

---

### Registration

`AddModulith` requires `IConfiguration` and `IHostEnvironment` as explicit parameters. These are passed directly from the host builder — no intermediate `BuildServiceProvider()` call is made.

```csharp
// WebApplication
builder.Services.AddModulith(
    builder.Configuration,
    builder.Environment,
    modules => modules.AddModulesFromAssemblyContaining<Program>());

// Generic host
hostBuilder.ConfigureServices((ctx, services) =>
    services.AddModulith(
        ctx.Configuration,
        ctx.HostingEnvironment,
        modules => modules.AddModulesFromAssemblyContaining<Program>()));
```

`AddModulesFromAssemblyContaining<T>()` scans the assembly that contains `T` and registers all non-abstract `IModule` implementations. To add a specific module manually:

```csharp
modules
    .AddModulesFromAssemblyContaining<Program>()
    .AddModule<SomeExternalModule>();
```

If the same type is added more than once (e.g. via both scanning and `AddModule<T>()`), it is deduplicated and registered only once.

---

### Boot order

Modules are always started in dependency order. If `ShippingModule` depends on `OrdersModule`, then `OrdersModule.Register` is always called first. The sort is performed using Kahn's algorithm. If a cycle is detected, an `InvalidOperationException` is thrown at startup with the names of the offending modules listed explicitly.

---

## Declaring dependencies — `[DependsOn]`

Declares that this module must be booted after the specified modules.

```csharp
[DependsOn(typeof(CoreModule))]
public sealed class OrdersModule : IModule { ... }

[DependsOn(typeof(OrdersModule), typeof(InventoryModule))]
public sealed class ShippingModule : IModule { ... }
```

Resulting boot order: `CoreModule` → `OrdersModule`, `InventoryModule` → `ShippingModule`.

Every type passed to `[DependsOn]` must implement `IModule`; the attribute constructor throws `ArgumentException` otherwise. If a declared dependency is not registered, a descriptive `InvalidOperationException` is thrown at startup.

---

## Conditional loading — `[FeatureFlag]`

Loads the module only when a specific configuration key equals `true`.

```csharp
[FeatureFlag("Features:Billing")]
public sealed class BillingModule : IModule { ... }
```

```json
{
  "Features": {
    "Billing": true
  }
}
```

If the key is absent from configuration, the module is **skipped by default**. To invert this behavior, set `LoadWhenMissing = true`:

```csharp
// Load unless the key is explicitly set to "false"
[FeatureFlag("Features:Legacy", LoadWhenMissing = true)]
public sealed class LegacyModule : IModule { ... }
```

Value comparison is case-insensitive: `"True"`, `"true"`, and `"TRUE"` are equivalent.

---

## Environment restriction — `[RequiresEnvironment]`

Activates the module only in the specified environments.

```csharp
[RequiresEnvironment("Development")]
public sealed class SeedDataModule : IModule { ... }

[RequiresEnvironment("Production", "Staging")]
public sealed class TelemetryModule : IModule { ... }
```

Comparison against `IHostEnvironment.EnvironmentName` is case-insensitive. When multiple names are provided, a match on any one of them is sufficient.

`[FeatureFlag]` and `[RequiresEnvironment]` may be combined on the same module. Both conditions must be satisfied for the module to load.

```csharp
[RequiresEnvironment("Production")]
[FeatureFlag("Features:NewCheckout")]
public sealed class NewCheckoutModule : IModule { ... }
```

---

## Lifecycle hooks — `IModuleLifecycle`

`IModuleLifecycle` is optional. Modules that implement it participate in host startup and shutdown.

```csharp
public sealed class OrdersModule : IModule, IModuleLifecycle {
    public string Name => "Orders";

    public void Register(IServiceCollection services, IConfiguration configuration) {
        services.AddScoped<IOrderService, OrderService>();
    }

    public async Task OnStarting(CancellationToken ct) {
        // Runs before the host begins accepting requests.
        // Suitable for migrations, cache warm-up, connection checks.
        await _dbContext.Database.MigrateAsync(ct);
    }

    public Task OnStarted(CancellationToken ct) {
        // Runs after the host is fully up and accepting requests.
        // Failures here are non-fatal — logged and ignored.
        _logger.LogInformation("Orders module ready.");
        return Task.CompletedTask;
    }

    public async Task OnStopping(CancellationToken ct) {
        // Runs during shutdown, in reverse boot order.
        // Suitable for draining queues and closing connections gracefully.
        await _outbox.FlushAsync(ct);
    }
}
```

| Hook | Order | On failure |
|---|---|---|
| `OnStarting` | Boot order | Fatal — startup is aborted and the exception propagates |
| `OnStarted` | Boot order | Non-fatal — logged, application continues |
| `OnStopping` | Reverse boot order | Non-fatal — logged, remaining modules still stop |

`OnStopping` never rethrows — every module is guaranteed a chance to shut down regardless of what other modules do.

---

## Options — `ModulithOptions`

Passed as the optional fourth parameter to `AddModulith`. All values are validated at startup; invalid values throw `OptionsValidationException` before the host starts.

```csharp
builder.Services.AddModulith(
    builder.Configuration,
    builder.Environment,
    modules => modules.AddModulesFromAssemblyContaining<Program>(),
    options => {
        options.ModuleLifetime                   = ServiceLifetime.Singleton; // default
        options.StartupHookTimeout               = TimeSpan.FromSeconds(60);  // default: 30s
        options.ShutdownHookTimeout              = TimeSpan.FromSeconds(15);  // default: 10s
        options.LogSkippedModules                = true;                      // default: true
        options.SkipModulesWithMissingFeatureFlag = true;                     // default: true
    });
```

**`ModuleLifetime`:** The DI lifetime used when registering module instances. Defaults to `Singleton` because modules hold no per-request state. Use `Scoped` in integration tests to swap modules per test run.

**`StartupHookTimeout`:** Maximum time allowed for a single module's `OnStarting` hook. If exceeded, a `TimeoutException` is thrown and startup aborts. Set to `Timeout.InfiniteTimeSpan` to disable.

**`ShutdownHookTimeout`:** Maximum time for `OnStopping`. If exceeded, the failure is logged and shutdown continues.

**`LogSkippedModules`:** When `true`, an `Information`-level log entry is emitted for each module skipped due to `[FeatureFlag]` or `[RequiresEnvironment]`.

**`SkipModulesWithMissingFeatureFlag`:** When `true`, a module whose `[FeatureFlag]` key is absent from configuration is not loaded. Per-module `LoadWhenMissing = true` overrides this.

---

## Startup sequence

```
AddModulith()
├── ModulithOptions validation
├── Module discovery (assembly scan + manual registrations)
├── Deduplication
├── [RequiresEnvironment] filter
├── [FeatureFlag] filter
├── Topological sort via [DependsOn]
└── For each module in order:
    ├── Activator.CreateInstance(moduleType)
    ├── module.Register(services, configuration)
    └── TryAdd(moduleType → instance, lifetime)

Host.Build()

Host.StartAsync()
└── ModulithHostedService
    ├── For each IModuleLifecycle in boot order → OnStarting(ct)
    └── For each IModuleLifecycle in boot order → OnStarted(ct)

Host.StopAsync()
└── ModulithHostedService
    └── For each IModuleLifecycle in reverse boot order → OnStopping(ct)
```

---

## FAQ

**Can a module expose its services to other modules?**
Yes. Registrations made in `Register` go into the shared DI container and are visible to all modules. For cross-module contracts, define a separate `Contracts` project to avoid circular project references.

**Can I inject dependencies into a module's constructor?**
No. Module instances are created with `Activator.CreateInstance` before the DI container is built. Inject dependencies into the services the module registers, not into the module class itself.

**What happens if a dependency cycle exists?**
An `InvalidOperationException` is thrown at startup listing the names of all modules involved in the cycle.

**Can I use this in a worker service or console application?**
Yes. `Wiaoj.Modulith` is sufficient. `Wiaoj.Modulith.AspNetCore` is only needed for `WebApplication` middleware and endpoint mapping.

---

## License

MIT