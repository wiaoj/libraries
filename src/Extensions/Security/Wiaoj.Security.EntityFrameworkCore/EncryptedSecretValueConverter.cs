using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Wiaoj.Security.EntityFrameworkCore;

/// <summary>
/// Maps the <see cref="EncryptedSecret{TContext}"/> object to a single string column 
/// in the database using the "Version:Base64Blob" format.
/// </summary>
public sealed class EncryptedSecretValueConverter<TContext> : ValueConverter<EncryptedSecret<TContext>, string>
    where TContext : ISecretContext {
    private const char StorageSeparator = ':';

    public EncryptedSecretValueConverter()
        : base(
            secret => $"{secret.KeyVersion.Value}{StorageSeparator}{secret.Blob.ToStorageString()}",
            dbValue => Parse(dbValue)) {
    }

    private static EncryptedSecret<TContext> Parse(string dbValue) {
        if(string.IsNullOrWhiteSpace(dbValue))
            throw new InvalidOperationException(
                $"The database value for EncryptedSecret<{typeof(TContext).Name}> is empty or whitespace.");

        ReadOnlySpan<char> span = dbValue.AsSpan();
        int colonIndex = span.IndexOf(StorageSeparator);

        if(colonIndex == -1)
            throw new InvalidOperationException(
                $"Invalid EncryptedSecret format for context {typeof(TContext).Name}. Expected 'version:base64'.");

        int version = int.Parse(span[..colonIndex]);
        string base64 = span[(colonIndex + 1)..].ToString();
        return EncryptedSecret<TContext>.FromPersisted(base64, version);
    }
}

/// <summary>
/// Defines EF Core change tracking behavior for <see cref="EncryptedSecret{TContext}"/>.
/// </summary>
public sealed class EncryptedSecretValueComparer<TContext> : ValueComparer<EncryptedSecret<TContext>>
    where TContext : ISecretContext {
    public EncryptedSecretValueComparer() : base(
        (left, right) => left.Equals(right),
        secret => secret.GetHashCode(),
        secret => secret) {
    }
}

public static class EncryptedSecretMappingExtensions {

    // ─── PropertyBuilder (Entity Properties) ────────────────────────────────

    /// <summary>
    /// Configures a required <see cref="EncryptedSecret{TContext}"/> entity property.
    /// </summary>
    public static PropertyBuilder<EncryptedSecret<TContext>> HasEncryptedSecretConversion<TContext>(
        this PropertyBuilder<EncryptedSecret<TContext>> propertyBuilder)
        where TContext : ISecretContext {
        propertyBuilder.IsRequired();
        return propertyBuilder.HasConversion(
            new EncryptedSecretValueConverter<TContext>(),
            new EncryptedSecretValueComparer<TContext>());
    }

    /// <summary>
    /// Configures an optional <see cref="EncryptedSecret{TContext}"/> entity property.
    /// </summary>
    public static PropertyBuilder<EncryptedSecret<TContext>?> HasEncryptedSecretConversion<TContext>(
        this PropertyBuilder<EncryptedSecret<TContext>?> propertyBuilder)
        where TContext : ISecretContext {
        return propertyBuilder.HasConversion(
            new EncryptedSecretValueConverter<TContext>(),
            new EncryptedSecretValueComparer<TContext>());
    }

    // ─── ComplexTypePropertyBuilder (EF Core 8+ Complex Types) ──────────────

    /// <summary>
    /// Configures a required <see cref="EncryptedSecret{TContext}"/> property
    /// inside an EF Core 8+ complex type.
    /// <example>
    /// <code>
    /// builder.ComplexProperty(x => x.SigningConfig, cp => {
    ///     cp.Property(x => x.Secret).HasEncryptedSecretConversion();
    /// });
    /// </code>
    /// </example>
    /// </summary>
    public static ComplexTypePropertyBuilder<EncryptedSecret<TContext>> HasEncryptedSecretConversion<TContext>(
        this ComplexTypePropertyBuilder<EncryptedSecret<TContext>> propertyBuilder)
        where TContext : ISecretContext {
        propertyBuilder.IsRequired();
        return propertyBuilder.HasConversion(
            new EncryptedSecretValueConverter<TContext>(),
            new EncryptedSecretValueComparer<TContext>());
    }

    /// <summary>
    /// Configures an optional <see cref="EncryptedSecret{TContext}"/> property
    /// inside an EF Core 8+ complex type.
    /// </summary>
    public static ComplexTypePropertyBuilder<EncryptedSecret<TContext>?> HasEncryptedSecretConversion<TContext>(
        this ComplexTypePropertyBuilder<EncryptedSecret<TContext>?> propertyBuilder)
        where TContext : ISecretContext {
        return propertyBuilder.HasConversion(
            new EncryptedSecretValueConverter<TContext>(),
            new EncryptedSecretValueComparer<TContext>());
    }

    // ─── OwnedNavigationBuilder (Owned Entities) ────────────────────────────

    /// <summary>
    /// Configures a required <see cref="EncryptedSecret{TContext}"/> property
    /// inside an owned entity navigation.
    /// <example>
    /// <code>
    /// builder.OwnsOne(x => x.SigningConfig, owned => {
    ///     owned.HasEncryptedSecret(x => x.Secret);
    ///     owned.HasEncryptedSecret(x => x.PreviousSecret);
    /// });
    /// </code>
    /// </example>
    /// </summary>
    public static OwnedNavigationBuilder<TOwner, TDependant> HasEncryptedSecret<TOwner, TDependant, TContext>(
        this OwnedNavigationBuilder<TOwner, TDependant> builder,
        Expression<Func<TDependant, EncryptedSecret<TContext>>> propertyExpression)
        where TOwner : class
        where TDependant : class
        where TContext : ISecretContext {
        builder.Property(propertyExpression).HasEncryptedSecretConversion();
        return builder;
    }

    /// <summary>
    /// Configures an optional <see cref="EncryptedSecret{TContext}"/> property
    /// inside an owned entity navigation.
    /// </summary>
    public static OwnedNavigationBuilder<TOwner, TDependant> HasEncryptedSecret<TOwner, TDependant, TContext>(
        this OwnedNavigationBuilder<TOwner, TDependant> builder,
        Expression<Func<TDependant, EncryptedSecret<TContext>?>> propertyExpression)
        where TOwner : class
        where TDependant : class
        where TContext : ISecretContext {
        builder.Property(propertyExpression).HasEncryptedSecretConversion();
        return builder;
    }

    // ─── EntityTypeBuilder Shorthands ───────────────────────────────────────

    /// <summary>
    /// Shorthand to configure a required <see cref="EncryptedSecret{TContext}"/> 
    /// directly on an entity type builder without nesting into Property().
    /// <example>
    /// <code>
    /// builder.HasEncryptedSecret(x => x.SigningSecret);
    /// builder.HasEncryptedSecret(x => x.PreviousSigningSecret); // optional
    /// </code>
    /// </example>
    /// </summary>
    public static EntityTypeBuilder<TEntity> HasEncryptedSecret<TEntity, TContext>(
        this EntityTypeBuilder<TEntity> builder,
        Expression<Func<TEntity, EncryptedSecret<TContext>>> propertyExpression)
        where TEntity : class
        where TContext : ISecretContext {
        builder.Property(propertyExpression).HasEncryptedSecretConversion();
        return builder;
    }

    /// <summary>
    /// Shorthand to configure an optional <see cref="EncryptedSecret{TContext}"/>
    /// directly on an entity type builder.
    /// </summary>
    public static EntityTypeBuilder<TEntity> HasEncryptedSecret<TEntity, TContext>(
        this EntityTypeBuilder<TEntity> builder,
        Expression<Func<TEntity, EncryptedSecret<TContext>?>> propertyExpression)
        where TEntity : class
        where TContext : ISecretContext {
        builder.Property(propertyExpression).HasEncryptedSecretConversion();
        return builder;
    }

    // ─── ModelBuilder Conventions (Global) ──────────────────────────────────

    /// <summary>
    /// Globally applies <see cref="EncryptedSecretValueConverter{TContext}"/> to all
    /// required properties of type <see cref="EncryptedSecret{TContext}"/> in the model.
    /// Call inside <c>ConfigureConventions</c>.
    /// </summary>
    public static PropertiesConfigurationBuilder<EncryptedSecret<TContext>> HaveEncryptedSecretConversion<TContext>(
        this PropertiesConfigurationBuilder<EncryptedSecret<TContext>> builder)
        where TContext : ISecretContext {
        return builder.HaveConversion<EncryptedSecretValueConverter<TContext>, EncryptedSecretValueComparer<TContext>>();
    }

    /// <summary>
    /// Globally applies <see cref="EncryptedSecretValueConverter{TContext}"/> to all
    /// optional properties of type <see cref="EncryptedSecret{TContext}"/> in the model.
    /// Call inside <c>ConfigureConventions</c>.
    /// </summary>
    public static PropertiesConfigurationBuilder<EncryptedSecret<TContext>?> HaveEncryptedSecretConversion<TContext>(
        this PropertiesConfigurationBuilder<EncryptedSecret<TContext>?> builder)
        where TContext : ISecretContext {
        return builder.HaveConversion<EncryptedSecretValueConverter<TContext>, EncryptedSecretValueComparer<TContext>>();
    }
}