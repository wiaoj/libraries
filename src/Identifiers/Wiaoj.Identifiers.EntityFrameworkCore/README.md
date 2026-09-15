# Wiaoj.Identifiers.EntityFrameworkCore

Stores [Wiaoj.Identifiers](../Wiaoj.Identifiers/README.md) as `bigint` columns in Entity Framework Core, and generates keys for entities added without one.

## Installation

```bash
dotnet add package Wiaoj.Identifiers.EntityFrameworkCore
```

## Usage

```csharp
public sealed class ShopContext(DbContextOptions<ShopContext> options) : DbContext(options) {
    public DbSet<Customer> Customers => Set<Customer>();

    protected override void ConfigureConventions(ModelConfigurationBuilder builder) {
        builder.AddIdentifier<CustomerId>();
        builder.AddIdentifier<OrderId>();
    }
}

public sealed class Customer {
    public CustomerId Id { get; set; }        // bigint primary key, generated on add
    public OrderId? LastOrderId { get; set; } // nullable bigint
}
```

`AddIdentifiersFromAssemblies(typeof(CustomerId).Assembly)` registers every identifier type in an assembly instead. It uses reflection, so under trimming or Native AOT call `AddIdentifier<TId>()` for each type.

## What is stored

The column holds `Value`, the Snowflake, as a 64-bit integer. The database never stores the text a codec writes. This means:

- **No codec needed:** migrations, queries and reports work without a codec or key, and neither is installed to read or write rows.
- **Key changes don't touch data:** changing the AES key, or switching between plain and AES, changes nothing in the database.
- **Order and indexing:** columns sort by creation (the Snowflake order) and index like any `bigint`.

## Keys

A primary key made of a single identifier property gets a value generator: an entity added with an empty key (`IsEmpty`) receives `TId.New()`, and a key you set yourself is kept.

A key is left alone when:

- it is also a foreign key, as in a one-to-one relationship that shares the principal's key;
- the model configures its generation explicitly, for example with `ValueGeneratedNever()`.

## Queries

Compare identifiers directly and the converter translates them:

```csharp
await db.Orders.Where(o => o.CustomerId == customerId).ToListAsync();
await db.Orders.Where(o => ids.Contains(o.Id)).ToListAsync();
await db.Orders.Where(o => o.ReviewerId == null).OrderBy(o => o.Id).ToListAsync();
```

Don't use `o.Id.Value` inside a query. The provider sees the converted column, not the struct's members, so the expression can't be translated. Compare with an identifier instead.
