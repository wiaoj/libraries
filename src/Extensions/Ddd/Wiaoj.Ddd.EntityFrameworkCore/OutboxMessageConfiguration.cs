using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wiaoj.Ddd.EntityFrameworkCore.Outbox;

namespace Wiaoj.Ddd.EntityFrameworkCore;
public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage> {
    public void Configure(EntityTypeBuilder<OutboxMessage> builder) {
        builder.ToTable("OutboxMessages");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Type).HasMaxLength(500).IsRequired();
        builder.Property(x => x.Content).IsRequired(); // JSON ise MaxLength vermeyebiliriz
        builder.Property(x => x.PartitionKey).HasMaxLength(100);

        // Optimistic Concurrency Token
        builder.Property(x => x.Version).IsConcurrencyToken();

        // Indexes for Performance
        builder.HasIndex(x => new { x.ProcessedAt, x.OccurredAt });

        // 2. Partitioning kullanılıyorsa: PartitionKey + ProcessedAt
        builder.HasIndex(x => new { x.PartitionKey, x.ProcessedAt })
               .HasFilter("\"ProcessedAt\" IS NULL");
    }
}