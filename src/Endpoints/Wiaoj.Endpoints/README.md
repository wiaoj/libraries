# Wiaoj.Endpoints

Minimal API endpoint module system for ASP.NET Core. Organize endpoints into self-contained classes, scan and map them with a single call.

Works standalone in any `WebApplication` project, or inside `Wiaoj.Modulith.AspNetCore` via `IWebModule.Configure`.

---

## Installation

```bash
dotnet add package Wiaoj.Endpoints
```

---

## Concepts

### `IEndpoint`

A class that owns a related group of endpoints. Its only responsibility is calling `Map*` methods on the provided `IEndpointRouteBuilder`.

```csharp
[RoutePrefix("/orders")]
public sealed class OrderEndpoints : IEndpoint {
    public void Map(IEndpointRouteBuilder app) {
        app.MapGet("/",      GetAll);
        app.MapGet("/{id}", GetById);
        app.MapPost("/",    Create);
        app.MapDelete("/{id}", Delete);
    }

    // Handler methods (static or instance)
    static async Task<IResult> GetAll(IOrderService orders)
        => Results.Ok(await orders.GetAllAsync());

    static async Task<IResult> GetById(Guid id, IOrderService orders)
        => await orders.GetByIdAsync(id) is { } order
            ? Results.Ok(order)
            : Results.NotFound();

    static async Task<IResult> Create(CreateOrderRequest req, IOrderService orders)
        => Results.Created($"/orders/{await orders.CreateAsync(req)}", null);

    static async Task<IResult> Delete(Guid id, IOrderService orders) {
        await orders.DeleteAsync(id);
        return Results.NoContent();
    }
}
```

Rules:

- The class must have a public parameterless constructor. Instances are created via `Activator.CreateInstance`.
- Handler dependencies are injected by the framework at request time, not at construction time. Keep the constructor empty.
- `Map` must contain only `MapGet` / `MapPost` / `MapPut` / `MapDelete` / `MapPatch` calls. No business logic here.

---

### `[RoutePrefix]`

Declares the route prefix for all endpoints in the module. When present, `MapEndpoints` creates a `RouteGroupBuilder` scoped to the prefix and passes it to `Map`. Routes inside `Map` are then relative to this prefix.

```csharp
// Without [RoutePrefix] — must specify full paths manually
public sealed class OrderEndpoints : IEndpoint {
    public void Map(IEndpointRouteBuilder app) {
        app.MapGet("/orders",      GetAll);
        app.MapGet("/orders/{id}", GetById);
    }
}

// With [RoutePrefix] — routes are relative to the prefix
[RoutePrefix("/orders")]
public sealed class OrderEndpoints : IEndpoint {
    public void Map(IEndpointRouteBuilder app) {
        app.MapGet("/",      GetAll);  // → GET /orders
        app.MapGet("/{id}", GetById); // → GET /orders/{id}
    }
}
```

---

## Mapping endpoints

### Standalone — `WebApplication`

Scan an entire assembly and map all discovered `IEndpoint` implementations:

```csharp
var app = builder.Build();

app.MapEndpoints<Program>();   // scans the assembly containing Program
app.Run();
```

Or pass an `Assembly` directly:

```csharp
app.MapEndpoints(typeof(OrderEndpoints).Assembly);
```

### Single module

Map a specific module without scanning:

```csharp
app.MapEndpoints(new OrderEndpoints());
```

This is useful when the module is resolved from DI or constructed manually.

---

## Using with `Wiaoj.Modulith.AspNetCore`

Inside an `IWebModule.Configure`, call `MapEndpoints` on the assembly that contains the module's endpoints. No bridge package is required.

```csharp
public sealed class OrdersModule : IWebModule {
    public string Name => "Orders";

    public void Register(IServiceCollection services, IConfiguration configuration) {
        services.AddScoped<IOrderService, OrderService>();
    }

    public void Configure(IApplicationBuilder app) {
        if (app is WebApplication web)
            web.MapEndpoints(typeof(OrdersModule).Assembly);
    }
}
```

Each module scans its own assembly, so endpoint discovery is naturally scoped to the bounded context.

---

## Using with API versioning

`Wiaoj.Endpoints` does not depend on `Asp.Versioning`. Versioning is handled by the caller — pass a versioned `RouteGroupBuilder` to `MapEndpoints` instead of `WebApplication`.

```csharp
// Program.cs
builder.Services.AddApiVersioning();

var app = builder.Build();

var v1 = app.NewVersionedApi().MapGroup("/api/v1");
v1.MapEndpoints<Program>();

var v2 = app.NewVersionedApi().MapGroup("/api/v2");
v2.MapEndpoints(typeof(V2Endpoints).Assembly);

app.Run();
```

Because `MapEndpoints` accepts `IEndpointRouteBuilder`, it works on any route group — versioned or not.

---

## Filters and policies on a module

`RouteGroupBuilder` returned from `MapGroup` (which `[RoutePrefix]` uses internally) supports all standard middleware:

```csharp
[RoutePrefix("/orders")]
public sealed class OrderEndpoints : IEndpoint {
    public void Map(IEndpointRouteBuilder app) {
        // app is a RouteGroupBuilder here — cast to add group-level config
        if (app is RouteGroupBuilder group) {
            group.RequireAuthorization("OrdersPolicy");
            group.AddEndpointFilter<ValidationFilter>();
            group.WithTags("Orders");
        }

        app.MapGet("/", GetAll);
        app.MapPost("/", Create);
    }
}
```

---

## License

MIT