using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wiaoj.Ddd.EntityFrameworkCore.Outbox;
using Wiaoj.Preconditions;

#pragma warning disable IDE0130
namespace Microsoft.EntityFrameworkCore;
#pragma warning restore IDE0130

/// <summary>
/// Schema-shape options for the outbox table. These are fixed when the model is built, which is why they
/// live here and not in <see cref="OutboxOptions"/> — that one carries runtime behaviour, which can change
/// under a running process, and this one cannot.
/// </summary>
public sealed class OutboxModelOptions {
    /// <summary>Gets or sets the table name. Default is <c>OutboxMessages</c>.</summary>
    public string TableName { get; set; } = "OutboxMessages";

    /// <summary>Gets or sets the schema. Default is the provider's default schema.</summary>
    public string? Schema { get; set; }

    /// <summary>
    /// Gets or sets whether the claim indexes are declared as filtered (partial) indexes.
    /// </summary>
    /// <remarks>
    /// A filter is expressed as raw SQL and is not portable, so it is opt-out. Turning it off costs index
    /// size, not correctness — the indexes still cover the claim query.
    /// </remarks>
    public bool UseFilteredIndexes { get; set; } = true;

    /// <summary>Gets or sets the maximum length of the alias columns. Default is 256.</summary>
    public int AliasMaxLength { get; set; } = 256;
}

/// <summary>
/// Registers the outbox entity on a model.
/// </summary>
public static class OutboxModelBuilderExtensions {
    /// <summary>
    /// Maps <see cref="OutboxMessage"/> into the model, with indexes covering the claim query.
    /// </summary>
    /// <remarks>
    /// Call this from <c>OnModelCreating</c>. It replaces the previous arrangement, where the entity reached
    /// the model only because the context happened to expose a <c>DbSet&lt;OutboxMessage&gt;</c> — a
    /// requirement nothing enforced, so forgetting it failed at run time rather than at build time.
    /// </remarks>
    public static ModelBuilder ApplyDddOutbox(this ModelBuilder modelBuilder, Action<OutboxModelOptions>? configure = null) {
        Preca.ThrowIfNull(modelBuilder);

        OutboxModelOptions options = new();
        configure?.Invoke(options);

        modelBuilder.Entity<OutboxMessage>(entity => {
            entity.ToTable(options.TableName, options.Schema);
            entity.HasKey(x => x.Id);

            entity.Property(x => x.EventAlias).HasMaxLength(options.AliasMaxLength).IsRequired();
            entity.Property(x => x.HandlerAlias).HasMaxLength(options.AliasMaxLength).IsRequired();
            entity.Property(x => x.Payload).IsRequired();
            entity.Property(x => x.PartitionKey).HasMaxLength(100);
            entity.Property(x => x.ProcessedBy).HasMaxLength(200);
            entity.Property(x => x.LockId).HasMaxLength(200);

            entity.Ignore(x => x.OccurredAt);
            entity.Ignore(x => x.ProcessedAt);
            entity.Ignore(x => x.DeadLetteredAt);
            entity.Ignore(x => x.IsTerminal);

            // The claim query orders by NextAttemptAtTicks over rows that are neither processed nor
            // dead-lettered; this index is that query.
            IndexBuilder claimIndex = entity
                .HasIndex(x => new { x.ProcessedAtTicks, x.DeadLetteredAtTicks, x.NextAttemptAtTicks })
                .HasDatabaseName($"IX_{options.TableName}_Claim");

            IndexBuilder partitionIndex = entity
                .HasIndex(x => new { x.PartitionKey, x.ProcessedAtTicks, x.DeadLetteredAtTicks, x.NextAttemptAtTicks })
                .HasDatabaseName($"IX_{options.TableName}_Claim_Partition");

            if(options.UseFilteredIndexes) {
                // Pending rows only. Quoting differs by provider, so this is the one place a dialect leaks —
                // hence UseFilteredIndexes, which turns it off for providers that disagree.
                const string pendingFilter = @"""ProcessedAtTicks"" IS NULL AND ""DeadLetteredAtTicks"" IS NULL";
                claimIndex.HasFilter(pendingFilter);
                partitionIndex.HasFilter(pendingFilter);
            }
        });

        return modelBuilder;
    }
}
