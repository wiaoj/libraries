using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Wiaoj.Webhooks.Publishing;

namespace Wiaoj.Webhooks.EntityFrameworkCore.Tests.Integration.Infrastructure;

/// <summary>
/// One implementation of the webhook stores that the shared contract suites run against.
/// </summary>
public abstract class StoreBackend : IAsyncDisposable {
    /// <summary>Gets why the backend cannot run on this machine, or <see langword="null"/> when it can.</summary>
    public virtual string? SkipReason => null;

    /// <summary>Gets whether the stores keep their state when the service provider is rebuilt.</summary>
    public abstract bool IsDurable { get; }

    public virtual void ConfigureServices(IServiceCollection services) {
    }

    public abstract void ConfigureWebhooks(IWebhookBuilder webhooks);

    public abstract void ConfigurePublishing(IWebhookPublishingBuilder publishing);

    /// <summary>Creates the schema, once, before the first provider is built.</summary>
    public virtual Task CreateSchemaAsync(IServiceProvider services) {
        return Task.CompletedTask;
    }

    public virtual ValueTask DisposeAsync() {
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }
}

/// <summary>The default in-memory stores of <c>Wiaoj.Webhooks</c> and <c>Wiaoj.Webhooks.Publishing</c>.</summary>
public sealed class InMemoryStoreBackend : StoreBackend {
    public override bool IsDurable => false;

    public override void ConfigureWebhooks(IWebhookBuilder webhooks) {
        webhooks.UseInMemoryStore();
    }

    public override void ConfigurePublishing(IWebhookPublishingBuilder publishing) {
        publishing.UseInMemoryStore().UsePersistentBatching();
    }
}

/// <summary>Shared EF Core wiring: the stores registered on <see cref="TestWebhookDbContext"/>.</summary>
public abstract class EfCoreStoreBackend : StoreBackend {
    public override bool IsDurable => true;

    public override void ConfigureServices(IServiceCollection services) {
        services.AddDbContext<TestWebhookDbContext>(this.ConfigureDbContext);
    }

    protected abstract void ConfigureDbContext(DbContextOptionsBuilder options);

    public override void ConfigureWebhooks(IWebhookBuilder webhooks) {
        webhooks.UseEntityFrameworkCoreStore<TestWebhookDbContext>();
    }

    public override void ConfigurePublishing(IWebhookPublishingBuilder publishing) {
        publishing.UseEntityFrameworkCoreStore<TestWebhookDbContext>();
    }

    public override async Task CreateSchemaAsync(IServiceProvider services) {
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        TestWebhookDbContext context = scope.ServiceProvider.GetRequiredService<TestWebhookDbContext>();
        await context.Database.EnsureCreatedAsync();
    }
}

/// <summary>
/// SQLite in memory. A named shared-cache database lives as long as one connection to it is open, so the backend keeps
/// one open for its lifetime while every store operation opens its own: the stores can then run concurrently, which a
/// single shared connection would not allow.
/// </summary>
public sealed class SqliteStoreBackend : EfCoreStoreBackend {
    private readonly string _connectionString = $"Data Source=webhooks_{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
    private readonly SqliteConnection _keepAlive;

    public SqliteStoreBackend() {
        this._keepAlive = new SqliteConnection(this._connectionString);
        this._keepAlive.Open();
    }

    protected override void ConfigureDbContext(DbContextOptionsBuilder options) {
        options.UseSqlite(this._connectionString);
    }

    public override async ValueTask DisposeAsync() {
        await this._keepAlive.DisposeAsync();
        await base.DisposeAsync();
    }
}

/// <summary>
/// PostgreSQL, when <c>WIAOJ_TEST_POSTGRES</c> holds a connection string to a server the tests may create databases
/// on. Every backend gets its own database and drops it afterwards.
/// </summary>
public sealed class PostgreSqlStoreBackend : EfCoreStoreBackend {
    public const string EnvironmentVariable = "WIAOJ_TEST_POSTGRES";

    private static readonly string? ServerConnectionString = Environment.GetEnvironmentVariable(EnvironmentVariable);

    private readonly string? _connectionString;

    public PostgreSqlStoreBackend() {
        if(!string.IsNullOrWhiteSpace(ServerConnectionString)) {
            NpgsqlConnectionStringBuilder builder = new(ServerConnectionString) {
                Database = $"webhooks_{Guid.NewGuid():N}"
            };
            this._connectionString = builder.ConnectionString;
        }
    }

    public override string? SkipReason => this._connectionString is null
        ? $"Set {EnvironmentVariable} to a PostgreSQL connection string to run the PostgreSQL store tests."
        : null;

    protected override void ConfigureDbContext(DbContextOptionsBuilder options) {
        options.UseNpgsql(this._connectionString);
    }

    public override async ValueTask DisposeAsync() {
        if(this._connectionString is not null) {
            DbContextOptions<TestWebhookDbContext> options = new DbContextOptionsBuilder<TestWebhookDbContext>()
                .UseNpgsql(this._connectionString)
                .Options;

            await using TestWebhookDbContext context = new(options);
            NpgsqlConnection.ClearAllPools();
            await context.Database.EnsureDeletedAsync();
        }

        await base.DisposeAsync();
    }
}
