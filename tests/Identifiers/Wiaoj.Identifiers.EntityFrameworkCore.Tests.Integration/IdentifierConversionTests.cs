using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Wiaoj.Primitives.Snowflake;

namespace Wiaoj.Identifiers.EntityFrameworkCore.Tests.Integration;

[Identifier("cus")]
public readonly partial record struct CustomerId;

[Identifier("ord")]
public readonly partial record struct OrderId;

[Identifier("rev")]
public readonly partial record struct ReviewerId;

public sealed class Customer {
    public CustomerId Id { get; set; }

    public string Name { get; set; } = "";
}

public sealed class Order {
    public OrderId Id { get; set; }

    public CustomerId CustomerId { get; set; }

    public ReviewerId? ReviewerId { get; set; }
}

/// <summary>Shares its customer's key: the key is also a foreign key, so it must never be generated.</summary>
public sealed class CustomerProfile {
    public CustomerId Id { get; set; }

    public string Bio { get; set; } = "";
}

/// <summary>
/// Implements the identifier interface for another type. Scanning must skip it: it is not an identifier of itself.
/// </summary>
public readonly struct NotAnIdentifier : IIdentifier<CustomerId> {
    public static string Prefix => "nope";

    public SnowflakeId Value => default;

    public static CustomerId From(SnowflakeId value) => CustomerId.From(value);
}

/// <summary>A key whose value generation the model sets explicitly.</summary>
public sealed class ImportedOrder {
    public OrderId Id { get; set; }
}

internal sealed class ShopContext(SqliteConnection connection, Action<ModelConfigurationBuilder> conventions) : DbContext {
    public DbSet<Customer> Customers => this.Set<Customer>();

    public DbSet<Order> Orders => this.Set<Order>();

    public DbSet<ImportedOrder> ImportedOrders => this.Set<ImportedOrder>();

    public DbSet<CustomerProfile> Profiles => this.Set<CustomerProfile>();

    protected override void OnConfiguring(DbContextOptionsBuilder options) {
        // A model per convention set: the default model cache would reuse the first model for every context.
        options.UseSqlite(connection).ReplaceService<Microsoft.EntityFrameworkCore.Infrastructure.IModelCacheKeyFactory, NoModelCache>();
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder builder) => conventions(builder);

    protected override void OnModelCreating(ModelBuilder model) {
        model.Entity<Order>().HasOne<Customer>().WithMany().HasForeignKey(o => o.CustomerId);
        model.Entity<ImportedOrder>().Property(o => o.Id).ValueGeneratedNever();
        model.Entity<CustomerProfile>().HasOne<Customer>().WithOne().HasForeignKey<CustomerProfile>(p => p.Id);
    }

    private sealed class NoModelCache : Microsoft.EntityFrameworkCore.Infrastructure.IModelCacheKeyFactory {
        public object Create(DbContext context, bool designTime) => new object();
    }
}

/// <summary>
/// Identifiers are stored as their Snowflake value in bigint columns, queried through the conversion, and generated for
/// keys added without one (#162).
/// </summary>
[Trait("Category", "Integration")]
[Trait("Feature", "Identifiers")]
[Trait("Component", "EntityFrameworkCore")]
public sealed class IdentifierConversionTests : IAsyncLifetime {
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    private static void PerType(ModelConfigurationBuilder builder) {
        builder.AddIdentifier<CustomerId>();
        builder.AddIdentifier<OrderId>();
        builder.AddIdentifier<ReviewerId>();
    }

    public async ValueTask InitializeAsync() {
        await this._connection.OpenAsync(Ct);
        await using ShopContext context = this.Context();
        await context.Database.EnsureCreatedAsync(Ct);
    }

    public async ValueTask DisposeAsync() => await this._connection.DisposeAsync();

    private ShopContext Context(Action<ModelConfigurationBuilder>? conventions = null) => new(this._connection, conventions ?? PerType);

    private async Task<object?> ScalarAsync(string sql) {
        await using SqliteCommand command = this._connection.CreateCommand();
        command.CommandText = sql;
        return await command.ExecuteScalarAsync(Ct);
    }

    [Fact]
    public async Task Should_Store_The_Snowflake_Value_In_An_Integer_Column() {
        CustomerId id = CustomerId.From(new SnowflakeId(1234567890123456789));
        await using(ShopContext context = this.Context()) {
            context.Customers.Add(new Customer { Id = id, Name = "Ada" });
            await context.SaveChangesAsync(Ct);
        }

        Assert.Equal(1234567890123456789L, await this.ScalarAsync("SELECT Id FROM Customers"));
        Assert.Equal("integer", await this.ScalarAsync("SELECT typeof(Id) FROM Customers"));

        await using ShopContext read = this.Context();
        IProperty property = read.Model.FindEntityType(typeof(Customer))!.FindProperty(nameof(Customer.Id))!;
        Assert.Equal(typeof(long), property.GetTypeMapping().Converter!.ProviderClrType);
    }

    [Fact]
    public async Task Should_Not_Need_An_Identifier_Codec() {
        // The database holds values, not codec text: no codec is installed in this test process.
        Assert.Throws<InvalidOperationException>(() => IdCodec.Current);

        await using ShopContext context = this.Context();
        context.Customers.Add(new Customer { Id = CustomerId.New(), Name = "Grace" });
        await context.SaveChangesAsync(Ct);

        Assert.Single(await context.Customers.ToListAsync(Ct));
    }

    [Fact]
    public async Task Should_Round_Trip_Keys_Foreign_Keys_And_Nullable_Identifiers() {
        CustomerId customer = CustomerId.New();
        ReviewerId reviewer = ReviewerId.New();
        OrderId reviewed = OrderId.New();
        OrderId unreviewed = OrderId.New();

        await using(ShopContext context = this.Context()) {
            context.Customers.Add(new Customer { Id = customer, Name = "Linus" });
            context.Orders.AddRange(
                new Order { Id = reviewed, CustomerId = customer, ReviewerId = reviewer },
                new Order { Id = unreviewed, CustomerId = customer });
            await context.SaveChangesAsync(Ct);
        }

        await using ShopContext read = this.Context();
        Dictionary<OrderId, Order> orders = await read.Orders.ToDictionaryAsync(o => o.Id, Ct);

        Assert.Equal(customer, orders[reviewed].CustomerId);
        Assert.Equal(reviewer, orders[reviewed].ReviewerId);
        Assert.Null(orders[unreviewed].ReviewerId);
        Assert.Equal(1L, await this.ScalarAsync("SELECT COUNT(*) FROM Orders WHERE ReviewerId IS NULL"));
    }

    [Fact]
    public async Task Should_Translate_Equality_Contains_Null_Checks_And_Ordering() {
        CustomerId customer = CustomerId.New();
        OrderId[] ids = [OrderId.From(new(300)), OrderId.From(new(100)), OrderId.From(new(200))];
        ReviewerId reviewer = ReviewerId.From(new(7));

        await using(ShopContext context = this.Context()) {
            context.Customers.Add(new Customer { Id = customer, Name = "Barbara" });
            context.Orders.AddRange(ids.Select((id, i) => new Order { Id = id, CustomerId = customer, ReviewerId = i == 0 ? reviewer : null }));
            await context.SaveChangesAsync(Ct);
        }

        await using ShopContext read = this.Context();
        OrderId wanted = ids[2];
        OrderId[] some = [ids[0], ids[1]];

        Assert.Equal(wanted, (await read.Orders.SingleAsync(o => o.Id == wanted, Ct)).Id);
        Assert.Equal(2, await read.Orders.CountAsync(o => some.Contains(o.Id), Ct));
        Assert.Equal(ids[0], (await read.Orders.SingleAsync(o => o.ReviewerId == reviewer, Ct)).Id);
        Assert.Equal(2, await read.Orders.CountAsync(o => o.ReviewerId == null, Ct));
        Assert.Equal(3, await read.Orders.CountAsync(o => o.CustomerId == customer, Ct));

        // Ordered by the stored value, so identifiers sort by creation.
        List<OrderId> ordered = await read.Orders.OrderBy(o => o.Id).Select(o => o.Id).ToListAsync(Ct);
        Assert.Equal([100L, 200L, 300L], ordered.Select(id => id.Value.Value));
    }

    [Fact]
    public async Task Should_Generate_A_Key_For_An_Entity_Added_Without_One() {
        await using(ShopContext context = this.Context()) {
            Customer customer = new() { Name = "Margaret" };
            context.Customers.Add(customer);

            Assert.False(customer.Id.IsEmpty);
            await context.SaveChangesAsync(Ct);
        }

        Assert.NotEqual(0L, await this.ScalarAsync("SELECT Id FROM Customers"));
    }

    [Fact]
    public async Task Should_Keep_A_Key_That_Was_Set() {
        CustomerId id = CustomerId.From(new(42));

        await using(ShopContext context = this.Context()) {
            context.Customers.Add(new Customer { Id = id, Name = "Frances" });
            await context.SaveChangesAsync(Ct);
        }

        Assert.Equal(42L, await this.ScalarAsync("SELECT Id FROM Customers"));
    }

    [Fact]
    public async Task Should_Generate_Distinct_Keys_For_Many_Entities() {
        await using ShopContext context = this.Context();
        Customer[] customers = [.. Enumerable.Range(0, 500).Select(i => new Customer { Name = $"c{i}" })];
        context.Customers.AddRange(customers);
        await context.SaveChangesAsync(Ct);

        Assert.Equal(500, customers.Select(c => c.Id).Distinct().Count());
    }

    [Fact]
    public void Should_Not_Generate_A_Foreign_Key_Or_A_Key_Configured_Otherwise() {
        using ShopContext context = this.Context();

        IProperty foreignKey = context.Model.FindEntityType(typeof(Order))!.FindProperty(nameof(Order.CustomerId))!;
        IProperty explicitKey = context.Model.FindEntityType(typeof(ImportedOrder))!.FindProperty(nameof(ImportedOrder.Id))!;
        IProperty generatedKey = context.Model.FindEntityType(typeof(Order))!.FindProperty(nameof(Order.Id))!;

        Assert.Equal(ValueGenerated.Never, foreignKey.ValueGenerated);
        Assert.Null(foreignKey.GetValueGeneratorFactory());
        Assert.Equal(ValueGenerated.Never, explicitKey.ValueGenerated);
        Assert.Equal(ValueGenerated.OnAdd, generatedKey.ValueGenerated);
        Assert.NotNull(generatedKey.GetValueGeneratorFactory());
    }

    [Fact]
    public async Task Should_Not_Generate_A_Key_That_Is_Also_A_Foreign_Key() {
        await using ShopContext context = this.Context();
        IProperty sharedKey = context.Model.FindEntityType(typeof(CustomerProfile))!.FindProperty(nameof(CustomerProfile.Id))!;

        Assert.NotEqual(ValueGenerated.OnAdd, sharedKey.ValueGenerated);
        Assert.Null(sharedKey.GetValueGeneratorFactory());

        CustomerId customer = CustomerId.New();
        context.Customers.Add(new Customer { Id = customer, Name = "Katherine" });
        context.Profiles.Add(new CustomerProfile { Id = customer, Bio = "Orbital mechanics" });
        await context.SaveChangesAsync(Ct);

        Assert.Equal(customer.Value.Value, await this.ScalarAsync("SELECT Id FROM Profiles"));
    }

    [Fact]
    public void Should_Register_Every_Identifier_Type_Found_In_An_Assembly() {
        using ShopContext context = this.Context(builder => builder.AddIdentifiersFromAssemblies(typeof(CustomerId).Assembly));

        foreach((Type entity, string property) in new[] {
                     (typeof(Customer), nameof(Customer.Id)),
                     (typeof(Order), nameof(Order.CustomerId)),
                     (typeof(Order), nameof(Order.ReviewerId))
                 }) {
            ValueConverter? converter = context.Model.FindEntityType(entity)!.FindProperty(property)!.GetTypeMapping().Converter;
            Assert.NotNull(converter);
            Assert.Equal(typeof(long), converter.ProviderClrType);
        }

        Assert.NotNull(context.Model.FindEntityType(typeof(Customer))!.FindProperty(nameof(Customer.Id))!.GetValueGeneratorFactory());
    }

    [Fact]
    public void Should_Skip_A_Type_That_Implements_The_Interface_For_Another_Type() {
        // NotAnIdentifier implements IIdentifier<CustomerId>; registering it would fail AddIdentifier's constraint.
        using ShopContext context = this.Context(builder => builder.AddIdentifiersFromAssemblies(typeof(NotAnIdentifier).Assembly));

        Assert.NotNull(context.Model.FindEntityType(typeof(Customer)));
    }

    [Fact]
    public void Should_Tolerate_Registering_A_Type_Twice() {
        using ShopContext context = this.Context(builder => {
            PerType(builder);
            builder.AddIdentifier<CustomerId>();
            builder.AddIdentifiersFromAssemblies(typeof(CustomerId).Assembly, typeof(CustomerId).Assembly);
        });

        Assert.NotNull(context.Model.FindEntityType(typeof(Customer))!.FindProperty(nameof(Customer.Id))!.GetValueGeneratorFactory());
    }

    [Fact]
    public void Should_Convert_Values_In_Both_Directions() {
        IdentifierValueConverter<CustomerId> converter = new();

        Assert.Equal(99L, converter.ConvertToProvider(CustomerId.From(new(99))));
        Assert.Equal(CustomerId.From(new(-5)), converter.ConvertFromProvider(-5L));
    }
}
