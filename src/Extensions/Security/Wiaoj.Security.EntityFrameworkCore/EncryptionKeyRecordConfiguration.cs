using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Wiaoj.Security.EntityFrameworkCore;
/// <summary>
/// EF Core table/column/index mapping for <see cref="EncryptionKeyRecord"/>.
/// Apply in your <c>DbContext.OnModelCreating</c>:
/// <code>
/// modelBuilder.ApplyConfiguration(new EncryptionKeyRecordConfiguration());
/// </code>
/// </summary>
/// <param name="options">The configuration options for the encryption key entity.</param>
public sealed class EncryptionKeyRecordConfiguration(EncryptionKeyOptions options) : IEntityTypeConfiguration<EncryptionKeyRecord> {
    /// <summary>
    /// Initializes a new instance of <see cref="EncryptionKeyRecordConfiguration"/> with default options.
    /// </summary>
    public EncryptionKeyRecordConfiguration() : this(new EncryptionKeyOptions()) { }

    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<EncryptionKeyRecord> builder) {
        // Primary Key
        builder.HasKey(x => x.Id);

        // ContextName Configuration
        var contextNameProp = builder.Property(x => x.ContextName)
            .HasMaxLength(options.ContextNameMaxLength);

        if(options.ContextNameIsRequired)
            contextNameProp.IsRequired();

        // Version Configuration
        builder.Property(x => x.Version).IsRequired();

        builder.Property(x => x.WrappedKeyMaterial)
            .IsRequired()
            .HasMaxLength(options.WrappedKeyMaxLength);

        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.RetiredAt);

        builder.Ignore(x => x.IsRetired);

        // Unique Index: Context + Version
        builder.HasIndex(x => new { x.ContextName, x.Version }, options.ContextVersionUniqueIndexName) 
            .IsUnique();

        // Lookup Index: ContextName
        builder.HasIndex(x => x.ContextName, options.ContextLookupIndexName);
    }
}

/// <summary>
/// Defines configuration options for the <see cref="EncryptionKeyRecord"/> entity mapping.
/// </summary>
public sealed record EncryptionKeyOptions {
    /// <summary>Gets or sets the maximum length for the ContextName property. Default is 256.</summary>
    public int ContextNameMaxLength { get; set; } = 256;

    /// <summary>Gets or sets the maximum length for the WrappedKeyMaterial property. Default is 1024.</summary>
    public int WrappedKeyMaxLength { get; set; } = 1024;

    /// <summary>Gets or sets whether the ContextName property is required. Default is true.</summary>
    public bool ContextNameIsRequired { get; set; } = true; 

    /// <summary>Gets or sets the database name for the unique index on ContextName and Version.</summary>
    public string ContextVersionUniqueIndexName { get; set; } = "IX_EncryptionKeys_Context_Version";

    /// <summary>Gets or sets the database name for the lookup index on ContextName.</summary>
    public string ContextLookupIndexName { get; set; } = "IX_EncryptionKeys_ContextName";
}