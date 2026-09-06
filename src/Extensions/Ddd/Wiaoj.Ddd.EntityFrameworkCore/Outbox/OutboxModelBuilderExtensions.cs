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
    /// Gets or sets the raw SQL predicate restricting the claim indexes to pending rows, or
    /// <see langword="null"/> for unfiltered indexes. Default is <see langword="null"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A partial index filter is raw SQL and must name <b>columns</b>, not properties — so a library cannot
    /// write it for you: a context using a naming convention maps <c>ProcessedAtTicks</c> to
    /// <c>processed_at_ticks</c>, and a built-in filter naming the property would make schema creation itself
    /// fail. Quoting differs by provider on top of that.
    /// </para>
    /// <para>
    /// Leaving it null costs index size, never correctness — the indexes still cover the claim query. Set it
    /// when you know your own column names and dialect:
    /// </para>
    /// <code>
    /// outbox.PendingIndexFilter = "\"processed_at_ticks\" IS NULL AND \"dead_lettered_at_ticks\" IS NULL";
    /// </code>
    /// </remarks>
    public string? PendingIndexFilter { get; set; }

    /// <summary>Gets or sets the maximum length of the alias columns. Default is 512.</summary>
    public int AliasMaxLength { get; set; } = 512;
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

            if(options.PendingIndexFilter is { Length: > 0 } pendingFilter) {
                claimIndex.HasFilter(pendingFilter);
                partitionIndex.HasFilter(pendingFilter);
            }
        });

        return modelBuilder;
    }
}
