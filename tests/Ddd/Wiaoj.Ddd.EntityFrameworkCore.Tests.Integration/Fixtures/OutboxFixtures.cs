using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wiaoj.Ddd.DomainEvents;
using Wiaoj.Ddd.EntityFrameworkCore.Internal;
using Wiaoj.Ddd.EntityFrameworkCore.Outbox;
using Wiaoj.Ddd.ValueObjects;
using Wiaoj.Serialization;
using Wiaoj.Serialization.SystemTextJson;

namespace Wiaoj.Ddd.EntityFrameworkCore.Tests.Integration.Fixtures;

public readonly record struct InvoiceId(long Value) : IId;

[DomainEventAlias("invoices.raised.v1")]
public sealed record InvoiceRaised(Guid Id, long InvoiceId, DateTimeOffset OccurredAt) : IDomainEvent;

public sealed class Invoice : Aggregate<InvoiceId> {
    private Invoice() { }

    public Invoice(InvoiceId id, string reference) : base(id) {
        this.Reference = reference;
    }

    public string Reference { get; private set; } = string.Empty;

    public void Raise(DateTimeOffset at) => RaiseDomainEvent(new InvoiceRaised(Guid.CreateVersion7(), this.Id.Value, at));
}

/// <summary>Records what ran, so a test can assert a failing handler did not replay its siblings.</summary>
public sealed class HandlerLog {
    private readonly Lock _gate = new();
    private readonly List<string> _calls = [];

    public IReadOnlyList<string> Calls {
        get {
            lock(this._gate) {
                return [.. this._calls];
            }
        }
    }

    public int CountOf(string handler) => this.Calls.Count(c => c == handler);

    public void Record(string handler) {
        lock(this._gate) {
            this._calls.Add(handler);
        }
    }

    /// <summary>When set, the named handler throws on every call.</summary>
    public string? AlwaysFailing { get; set; }
}

[DomainEventHandlerAlias("invoices.ledger")]
public sealed class LedgerHandler(HandlerLog log) : IPostDomainEventHandler<InvoiceRaised> {
    public ValueTask Handle(InvoiceRaised @event, CancellationToken cancellationToken = default) {
        log.Record("ledger");
        return Fail.IfConfigured(log, "ledger");
    }
}

[DomainEventHandlerAlias("invoices.notify")]
public sealed class NotifyHandler(HandlerLog log) : IPostDomainEventHandler<InvoiceRaised> {
    public ValueTask Handle(InvoiceRaised @event, CancellationToken cancellationToken = default) {
        log.Record("notify");
        return Fail.IfConfigured(log, "notify");
    }
}

internal static class Fail {
    public static ValueTask IfConfigured(HandlerLog log, string handler) {
        return log.AlwaysFailing == handler
            ? ValueTask.FromException(new InvalidOperationException($"'{handler}' is configured to fail."))
            : ValueTask.CompletedTask;
    }
}

public sealed class OutboxTestContext(DbContextOptions<OutboxTestContext> options) : DbContext(options) {
    public DbSet<Invoice> Invoices => Set<Invoice>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Invoice>(entity => {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasConversion(v => v.Value, v => new InvoiceId(v));
            entity.Property(x => x.Reference).IsRequired();
            entity.Ignore(x => x.DomainEvents);
            entity.Ignore(x => x.Version);
        });

        // No DbSet<OutboxMessage> anywhere: the entity reaches the model through this call alone.
        modelBuilder.ApplyDddOutbox();
    }
}

/// <summary>Builds a provider on either SQLite or the in-memory provider, wired the same way.</summary>
public sealed class OutboxHarness : IAsyncDisposable {
    private readonly SqliteConnection? _connection;

    public ServiceProvider Services { get; }
    public HandlerLog Log { get; } = new();

    private OutboxHarness(ServiceProvider services, HandlerLog log, SqliteConnection? connection) {
        this.Services = services;
        this.Log = log;
        this._connection = connection;
    }

    public static OutboxHarness Sqlite(TimeProvider timeProvider) {
        SqliteConnection connection = new("DataSource=:memory:");
        connection.Open();

        return Build(timeProvider, connection, options => options.UseSqlite(connection), ensureCreated: true);
    }

    public static OutboxHarness InMemory(TimeProvider timeProvider) {
        string databaseName = Guid.NewGuid().ToString();
        return Build(timeProvider, null, options => options.UseInMemoryDatabase(databaseName), ensureCreated: false);
    }

    private static OutboxHarness Build(
        TimeProvider timeProvider,
        SqliteConnection? connection,
        Action<DbContextOptionsBuilder> configureProvider,
        bool ensureCreated) {

        HandlerLog log = new();

        ServiceCollection services = new();
        services.AddSingleton(timeProvider);
        services.AddSingleton(log);
        services.AddLogging();
        services.AddSingleton<ISerializer<DddEfCoreOutboxSerializerKey>>(
            _ => new SystemTextJsonSerializer<DddEfCoreOutboxSerializerKey>(new()));
        services.AddSingleton<IOutboxAliasRegistry, OutboxAliasRegistry>();
        services.AddSingleton<OutboxHandlerCatalog>();
        services.AddScoped<IPostDomainEventHandler<InvoiceRaised>, LedgerHandler>();
        services.AddScoped<IPostDomainEventHandler<InvoiceRaised>, NotifyHandler>();

        services.AddSingleton<Microsoft.Extensions.Options.IOptions<OutboxOptions>>(
            Microsoft.Extensions.Options.Options.Create(new OutboxOptions { InitialDelay = TimeSpan.Zero }));

        services.AddScoped<DddAmbientUnitOfWork>();
        services.AddSingleton<IDomainEventDispatcher, NoOpPreCommitDispatcher>();
        services.AddSingleton<OutboxSignal<OutboxTestContext>>();
        services.AddSingleton<DomainEventDispatcherInterceptor<OutboxTestContext>>();

        services.AddDbContext<OutboxTestContext>((sp, options) => {
            configureProvider(options);
            options.AddInterceptors(sp.GetRequiredService<DomainEventDispatcherInterceptor<OutboxTestContext>>());
        });

        ServiceProvider provider = services.BuildServiceProvider();

        if(ensureCreated) {
            using IServiceScope scope = provider.CreateScope();
            scope.ServiceProvider.GetRequiredService<OutboxTestContext>().Database.EnsureCreated();
        }

        return new OutboxHarness(provider, log, connection);
    }

    public async ValueTask DisposeAsync() {
        await this.Services.DisposeAsync();

        if(this._connection is not null) {
            await this._connection.DisposeAsync();
        }
    }
}

/// <summary>
/// Pre-commit dispatch is not what these tests are about; this keeps the interceptor satisfied without
/// pulling in the full DDD registration.
/// </summary>
public sealed class NoOpPreCommitDispatcher : IDomainEventDispatcher {
    public ValueTask DispatchPreCommitAsync<TDomainEvent>(TDomainEvent @event, CancellationToken cancellationToken = default)
        where TDomainEvent : IDomainEvent => ValueTask.CompletedTask;

    public ValueTask DispatchPostCommitAsync<TDomainEvent>(TDomainEvent @event, CancellationToken cancellationToken = default)
        where TDomainEvent : IDomainEvent => ValueTask.CompletedTask;
}
