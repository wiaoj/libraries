using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;

namespace Wiaoj.Security.EntityFrameworkCore;

/// <summary>
/// Maps the <see cref="EncryptedSecret{TContext}"/> value object to a single compact string column 
/// in the database using the URL-safe <c>"v{version}.{blob}"</c> token format.
/// </summary>
/// <typeparam name="TContext">The secret domain context.</typeparam>
public sealed class EncryptedSecretValueConverter<TContext>() : ValueConverter<EncryptedSecret<TContext>, string>(
    static secret => secret.ToCompactString(),
    static dbValue => EncryptedSecret<TContext>.Parse(dbValue))
    where TContext : ISecretContext;

/// <summary>
/// Defines EF Core change tracking and snapshotting behavior for <see cref="EncryptedSecret{TContext}"/>.
/// </summary>
/// <typeparam name="TContext">The secret domain context.</typeparam>
public sealed class EncryptedSecretValueComparer<TContext>() : ValueComparer<EncryptedSecret<TContext>>(
    static (left, right) => left.Equals(right),
    static secret => secret.GetHashCode(),
    static secret => secret)
    where TContext : ISecretContext;

/// <summary>
/// Provides fluent extension methods for configuring <see cref="EncryptedSecret{TContext}"/> mappings in EF Core.
/// </summary>
public static class EncryptedSecretMappingExtensions {

    private const DynamicallyAccessedMemberTypes EntityMemberTypes =
        DynamicallyAccessedMemberTypes.PublicConstructors |
        DynamicallyAccessedMemberTypes.NonPublicConstructors |
        DynamicallyAccessedMemberTypes.PublicFields |
        DynamicallyAccessedMemberTypes.NonPublicFields |
        DynamicallyAccessedMemberTypes.PublicProperties |
        DynamicallyAccessedMemberTypes.NonPublicProperties |
        DynamicallyAccessedMemberTypes.Interfaces;

    // ─── PropertyBuilder (Standard Entity Properties) ─────────────────────────

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
    /// Configures an optional (nullable) <see cref="EncryptedSecret{TContext}"/> entity property.
    /// </summary>
    public static PropertyBuilder<EncryptedSecret<TContext>?> HasEncryptedSecretConversion<TContext>(
        this PropertyBuilder<EncryptedSecret<TContext>?> propertyBuilder)
        where TContext : ISecretContext {
        return propertyBuilder.HasConversion(
            new EncryptedSecretValueConverter<TContext>(),
            new EncryptedSecretValueComparer<TContext>());
    }

    // ─── ComplexTypePropertyBuilder (EF Core 8+ Complex Types) ────────────────

    /// <summary>
    /// Configures a required <see cref="EncryptedSecret{TContext}"/> property inside an EF Core 8+ complex type.
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
    /// Configures an optional <see cref="EncryptedSecret{TContext}"/> property inside an EF Core 8+ complex type.
    /// </summary>
    public static ComplexTypePropertyBuilder<EncryptedSecret<TContext>?> HasEncryptedSecretConversion<TContext>(
        this ComplexTypePropertyBuilder<EncryptedSecret<TContext>?> propertyBuilder)
        where TContext : ISecretContext {
        return propertyBuilder.HasConversion(
            new EncryptedSecretValueConverter<TContext>(),
            new EncryptedSecretValueComparer<TContext>());
    }

    // ─── OwnedNavigationBuilder (Owned Entities) ──────────────────────────────

    /// <summary>
    /// Configures a required <see cref="EncryptedSecret{TContext}"/> property inside an owned entity navigation.
    /// </summary>
    public static OwnedNavigationBuilder<TOwner, TDependant> HasEncryptedSecret<
        [DynamicallyAccessedMembers(EntityMemberTypes)] TOwner,
        [DynamicallyAccessedMembers(EntityMemberTypes)] TDependant,
        TContext>(
        this OwnedNavigationBuilder<TOwner, TDependant> builder,
        Expression<Func<TDependant, EncryptedSecret<TContext>>> propertyExpression)
        where TOwner : class
        where TDependant : class
        where TContext : ISecretContext {
        builder.Property(propertyExpression).HasEncryptedSecretConversion();
        return builder;
    }

    /// <summary>
    /// Configures an optional <see cref="EncryptedSecret{TContext}"/> property inside an owned entity navigation.
    /// </summary>
    public static OwnedNavigationBuilder<TOwner, TDependant> HasEncryptedSecret<
        [DynamicallyAccessedMembers(EntityMemberTypes)] TOwner,
        [DynamicallyAccessedMembers(EntityMemberTypes)] TDependant,
        TContext>(
        this OwnedNavigationBuilder<TOwner, TDependant> builder,
        Expression<Func<TDependant, EncryptedSecret<TContext>?>> propertyExpression)
        where TOwner : class
        where TDependant : class
        where TContext : ISecretContext {
        builder.Property(propertyExpression).HasEncryptedSecretConversion();
        return builder;
    }

    // ─── EntityTypeBuilder Shorthands ─────────────────────────────────────────

    /// <summary>
    /// Shorthand to configure a required <see cref="EncryptedSecret{TContext}"/> directly on an entity type builder.
    /// </summary>
    public static EntityTypeBuilder<TEntity> HasEncryptedSecret<
        [DynamicallyAccessedMembers(EntityMemberTypes)] TEntity,
        TContext>(
        this EntityTypeBuilder<TEntity> builder,
        Expression<Func<TEntity, EncryptedSecret<TContext>>> propertyExpression)
        where TEntity : class
        where TContext : ISecretContext {
        builder.Property(propertyExpression).HasEncryptedSecretConversion();
        return builder;
    }

    /// <summary>
    /// Shorthand to configure an optional <see cref="EncryptedSecret{TContext}"/> directly on an entity type builder.
    /// </summary>
    public static EntityTypeBuilder<TEntity> HasEncryptedSecret<
        [DynamicallyAccessedMembers(EntityMemberTypes)] TEntity,
        TContext>(
        this EntityTypeBuilder<TEntity> builder,
        Expression<Func<TEntity, EncryptedSecret<TContext>?>> propertyExpression)
        where TEntity : class
        where TContext : ISecretContext {
        builder.Property(propertyExpression).HasEncryptedSecretConversion();
        return builder;
    }

    // ─── ModelBuilder Conventions (Global / Bulk Configuration) ───────────────

    /// <summary>
    /// Globally applies <see cref="EncryptedSecretValueConverter{TContext}"/> to all required properties 
    /// of type <see cref="EncryptedSecret{TContext}"/> across the entire model. Call inside <c>ConfigureConventions</c>.
    /// </summary>
    public static PropertiesConfigurationBuilder<EncryptedSecret<TContext>> HaveEncryptedSecretConversion<TContext>(
        this PropertiesConfigurationBuilder<EncryptedSecret<TContext>> builder)
        where TContext : ISecretContext {
        return builder.HaveConversion<EncryptedSecretValueConverter<TContext>, EncryptedSecretValueComparer<TContext>>();
    }

    /// <summary>
    /// Globally applies <see cref="EncryptedSecretValueConverter{TContext}"/> to all optional properties 
    /// of type <see cref="EncryptedSecret{TContext}"/> across the entire model. Call inside <c>ConfigureConventions</c>.
    /// </summary>
    public static PropertiesConfigurationBuilder<EncryptedSecret<TContext>?> HaveEncryptedSecretConversion<TContext>(
        this PropertiesConfigurationBuilder<EncryptedSecret<TContext>?> builder)
        where TContext : ISecretContext {
        return builder.HaveConversion<EncryptedSecretValueConverter<TContext>, EncryptedSecretValueComparer<TContext>>();
    }
}