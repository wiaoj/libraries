using Wiaoj.Webhooks.EntityFrameworkCore;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Microsoft.EntityFrameworkCore;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Schema-shape options for the webhook tables. They are fixed when the model is built.
/// </summary>
public sealed class WebhookModelOptions {
    /// <summary>Gets or sets the schema of all webhook tables. Default is the provider's default schema.</summary>
    public string? Schema { get; set; }

    /// <summary>Gets or sets the jobs table name. Default is <c>WebhookJobs</c>.</summary>
    public string JobsTableName { get; set; } = "WebhookJobs";

    /// <summary>Gets or sets the delivery attempts table name. Default is <c>WebhookDeliveryAttempts</c>.</summary>
    public string AttemptsTableName { get; set; } = "WebhookDeliveryAttempts";

    /// <summary>Gets or sets the publishing subscriptions table name. Default is <c>WebhookSubscriptions</c>.</summary>
    public string SubscriptionsTableName { get; set; } = "WebhookSubscriptions";

    /// <summary>Gets or sets the publish batches table name. Default is <c>WebhookPublishBatches</c>.</summary>
    public string BatchesTableName { get; set; } = "WebhookPublishBatches";

    /// <summary>Gets or sets the maximum length of identifier columns (job, endpoint, batch, subscription, lease holder). Default is 200.</summary>
    public int IdentifierMaxLength { get; set; } = 200;

    /// <summary>Gets or sets the maximum length of event type, event pattern, partition key and namespace columns. Default is 256.</summary>
    public int NameMaxLength { get; set; } = 256;
}

/// <summary>
/// Registers the webhook store entities on a model.
/// </summary>
public static class WebhookModelBuilderExtensions {
    /// <summary>
    /// Maps the tables used by the EF Core webhook stores: jobs and their delivery attempts, publishing subscriptions and
    /// publish batches.
    /// </summary>
    /// <remarks>
    /// Call this from your context's <c>OnModelCreating</c>. The stores reach the tables through
    /// <see cref="DbContext.Set{TEntity}()"/>, so the context needs no <c>DbSet</c> properties for them.
    /// </remarks>
    /// <param name="modelBuilder">The model builder of the application's <see cref="DbContext"/>.</param>
    /// <param name="configure">An optional delegate adjusting table names, schema and column lengths.</param>
    /// <returns>The same <paramref name="modelBuilder"/>, for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="modelBuilder"/> is <see langword="null"/>.</exception>
    public static ModelBuilder ApplyWebhooks(this ModelBuilder modelBuilder, Action<WebhookModelOptions>? configure = null) {
        Preca.ThrowIfNull(modelBuilder);

        WebhookModelOptions options = new();
        configure?.Invoke(options);

        modelBuilder.Entity<WebhookJobEntity>(entity => {
            entity.ToTable(options.JobsTableName, options.Schema);
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Id).HasMaxLength(options.IdentifierMaxLength);
            entity.Property(x => x.BatchId).HasMaxLength(options.IdentifierMaxLength);
            entity.Property(x => x.EndpointId).HasMaxLength(options.IdentifierMaxLength).IsRequired();
            entity.Property(x => x.PartitionKey).HasMaxLength(options.NameMaxLength).IsRequired();
            entity.Property(x => x.EventType).HasMaxLength(options.NameMaxLength).IsRequired();
            entity.Property(x => x.SerializedPayload).IsRequired();
            entity.Property(x => x.LockedBy).HasMaxLength(options.IdentifierMaxLength);

            entity.HasMany(x => x.Attempts)
                .WithOne()
                .HasForeignKey(x => x.JobId)
                .OnDelete(DeleteBehavior.Cascade);

            // Recovery reads by status and one of the three timestamps; history and dead letters read by endpoint.
            entity.HasIndex(x => new { x.Status, x.LockExpiresAtTicks }).HasDatabaseName($"IX_{options.JobsTableName}_Status_LockExpires");
            entity.HasIndex(x => new { x.Status, x.CreatedAtTicks }).HasDatabaseName($"IX_{options.JobsTableName}_Status_Created");
            entity.HasIndex(x => new { x.Status, x.NextAttemptAtTicks }).HasDatabaseName($"IX_{options.JobsTableName}_Status_NextAttempt");
            entity.HasIndex(x => new { x.EndpointId, x.Status }).HasDatabaseName($"IX_{options.JobsTableName}_Endpoint_Status");
        });

        modelBuilder.Entity<WebhookDeliveryAttemptEntity>(entity => {
            entity.ToTable(options.AttemptsTableName, options.Schema);
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).ValueGeneratedOnAdd();

            entity.Property(x => x.JobId).HasMaxLength(options.IdentifierMaxLength).IsRequired();
            entity.Property(x => x.EndpointId).HasMaxLength(options.IdentifierMaxLength).IsRequired();
            entity.Property(x => x.ResultKind).HasMaxLength(32).IsRequired();
            entity.Property(x => x.DeduplicationKey).HasMaxLength(options.NameMaxLength);

            entity.HasIndex(x => x.JobId).HasDatabaseName($"IX_{options.AttemptsTableName}_JobId");
        });

        modelBuilder.Entity<WebhookSubscriptionEntity>(entity => {
            entity.ToTable(options.SubscriptionsTableName, options.Schema);
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Id).HasMaxLength(options.IdentifierMaxLength);
            entity.Property(x => x.Namespace).HasMaxLength(options.NameMaxLength).IsRequired();
            entity.Property(x => x.EndpointId).HasMaxLength(options.IdentifierMaxLength).IsRequired();
            entity.Property(x => x.EventTypePattern).HasMaxLength(options.NameMaxLength).IsRequired();

            entity.HasIndex(x => new { x.Namespace, x.IsEnabled }).HasDatabaseName($"IX_{options.SubscriptionsTableName}_Namespace_Enabled");
        });

        modelBuilder.Entity<WebhookPublishBatchEntity>(entity => {
            entity.ToTable(options.BatchesTableName, options.Schema);
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Id).HasMaxLength(options.IdentifierMaxLength);
            entity.Property(x => x.Namespace).HasMaxLength(options.NameMaxLength).IsRequired();
            entity.Property(x => x.EventName).HasMaxLength(options.NameMaxLength).IsRequired();
            entity.Property(x => x.SerializedPayload).IsRequired();
            entity.Property(x => x.TargetEndpoints).IsRequired();
            entity.Property(x => x.LockedBy).HasMaxLength(options.IdentifierMaxLength);

            entity.HasIndex(x => new { x.Status, x.LockExpiresAtTicks }).HasDatabaseName($"IX_{options.BatchesTableName}_Status_LockExpires");
        });

        return modelBuilder;
    }
}
