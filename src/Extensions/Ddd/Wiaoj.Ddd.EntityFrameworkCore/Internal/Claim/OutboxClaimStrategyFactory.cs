using Wiaoj.Preconditions;
using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;

namespace Wiaoj.Ddd.EntityFrameworkCore.Internal.Claim;

/// <summary>
/// Picks the claim strategy matching the context's provider, once per provider.
/// </summary>
internal sealed class OutboxClaimStrategyFactory {
    private const string PostgresProvider = "Npgsql.EntityFrameworkCore.PostgreSQL";
    private const string SqlServerProvider = "Microsoft.EntityFrameworkCore.SqlServer";
    private const string SqliteProvider = "Microsoft.EntityFrameworkCore.Sqlite";
    private const string InMemoryProvider = "Microsoft.EntityFrameworkCore.InMemory";

    private readonly ConcurrentDictionary<string, IOutboxClaimStrategy> _strategies = new(StringComparer.Ordinal);

    /// <summary>Resolves the strategy for <paramref name="dbContext"/>'s provider.</summary>
    public IOutboxClaimStrategy Create(DbContext dbContext) {
        Preca.ThrowIfNull(dbContext);

        string provider = dbContext.Database.ProviderName ?? "unknown";

        return this._strategies.GetOrAdd(provider, static name => name switch {
            PostgresProvider => new PostgresOutboxClaimStrategy(),
            SqlServerProvider => new SqlServerOutboxClaimStrategy(),
            SqliteProvider => new SqliteOutboxClaimStrategy(),
            InMemoryProvider => new InMemoryOutboxClaimStrategy(),
            _ => new UnsupportedOutboxClaimStrategy(name)
        });
    }
}
