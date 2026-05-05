using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion; 

namespace Wiaoj.Security.EntityFrameworkCore;
/// <summary>
/// Maps the <see cref="EncryptedSecret{TContext}"/> object to a single string column 
/// in the database using the "Version:Base64Blob" format.
/// </summary>
/// <typeparam name="TContext">The secret context type associated with this secret.</typeparam>
public sealed class EncryptedSecretValueConverter<TContext> : ValueConverter<EncryptedSecret<TContext>, string>
    where TContext : ISecretContext {

    /// <summary>
    /// Initializes a new instance of the <see cref="EncryptedSecretValueConverter{TContext}"/> class.
    /// </summary>
    public EncryptedSecretValueConverter()
        : base(
            // C# -> DB (Writing): Concatenate version and blob with a ":" separator
            secret => $"{secret.KeyVersion.Value}:{secret.Blob.ToStorageString()}",

            // DB -> C# (Reading): Split by ":" and reconstruct the object
            dbValue => Parse(dbValue)) {
    }

    /// <summary>
    /// Parses the string value stored in the database back into an <see cref="EncryptedSecret{TContext}"/>.
    /// </summary>
    /// <param name="dbValue">The raw string value from the database (format: 'version:base64').</param>
    /// <returns>A restored instance of <see cref="EncryptedSecret{TContext}"/>.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the database value is not in the expected format.</exception>
    private static EncryptedSecret<TContext> Parse(string dbValue) { 
        if(string.IsNullOrWhiteSpace(dbValue)) {
            throw new InvalidOperationException(
                $"The database value for EncryptedSecret<{typeof(TContext).Name}> is empty or whitespace. " +
                "This usually indicates data corruption or an uninitialized column.");
        }

        ReadOnlySpan<char> span = dbValue.AsSpan();
        int colonIndex = span.IndexOf(':');

        if(colonIndex == -1) {
            throw new InvalidOperationException(
                $"Invalid EncryptedSecret format for context {typeof(TContext).Name}. " +
                "Expected format is 'version:base64'.");
        }

        int version = int.Parse(span[..colonIndex]);
        string base64 = span[(colonIndex + 1)..].ToString();

        return EncryptedSecret<TContext>.FromPersisted(base64, version);
    }
}

/// <summary>
/// Defines how Entity Framework Core should compare <see cref="EncryptedSecret{TContext}"/> instances 
/// for value equality, hash code generation, and snapshotting during change tracking.
/// </summary>
/// <typeparam name="TContext">The secret context type associated with this secret.</typeparam>
public sealed class EncryptedSecretValueComparer<TContext> : ValueComparer<EncryptedSecret<TContext>>
    where TContext : ISecretContext {

    /// <summary>
    /// Initializes a new instance of the <see cref="EncryptedSecretValueComparer{TContext}"/> class 
    /// with custom equality, hash code, and snapshot expressions.
    /// </summary>
    /// <remarks>
    /// The snapshot expression assumes that <see cref="EncryptedSecret{TContext}"/> is immutable. 
    /// Therefore, it safely returns the same instance instead of creating a deep copy.
    /// </remarks>
    public EncryptedSecretValueComparer() : base(
        // 1. Equality: Are the contents of the two objects identical?
        (left, right) => left.Equals(right),

        // 2. HashCode: How should the hash value be calculated? (Used for dictionaries/HashSets)
        secret => secret.GetHashCode(),

        // 3. Snapshot: How should EF Core take a copy of this object for change tracking?
        // Since EncryptedSecret is assumed to be IMMUTABLE, returning the reference directly is safe.
        // If it were mutable, a new instance (deep copy) would need to be created here.
        secret => secret) {
    }
}

/// <summary>
/// 
/// </summary>
public static class EncryptedSecretMappingExtensions {
    /// <summary>
    /// Configures the property to use the <see cref="EncryptedSecretValueConverter{TContext}"/> 
    /// and <see cref="EncryptedSecretValueComparer{TContext}"/> for seamless database mapping.
    /// </summary>
    /// <remarks>
    /// This extension method bundles both the value conversion (storing as 'version:base64') 
    /// and the value comparison logic (ensuring EF Core correctly tracks changes for the immutable record struct).
    /// </remarks>
    /// <typeparam name="TContext">The secret context type associated with the <see cref="EncryptedSecret{TContext}"/>.</typeparam>
    /// <param name="propertyBuilder">The builder for the property being configured.</param>
    /// <returns>The same builder instance so that multiple calls can be chained.</returns>
    public static PropertyBuilder<EncryptedSecret<TContext>> HasEncryptedSecretConversion<TContext>(
        this PropertyBuilder<EncryptedSecret<TContext>> propertyBuilder)
        where TContext : ISecretContext {
        propertyBuilder.IsRequired();
        return propertyBuilder.HasConversion(
            new EncryptedSecretValueConverter<TContext>(),
            new EncryptedSecretValueComparer<TContext>()
        );
    }

    /// <summary>
    /// Configures the property to use the <see cref="EncryptedSecretValueConverter{TContext}"/> 
    /// and <see cref="EncryptedSecretValueComparer{TContext}"/> for seamless database mapping.
    /// </summary>
    /// <remarks>
    /// This extension method bundles both the value conversion (storing as 'version:base64') 
    /// and the value comparison logic (ensuring EF Core correctly tracks changes for the immutable record struct).
    /// </remarks>
    /// <typeparam name="TContext">The secret context type associated with the <see cref="EncryptedSecret{TContext}"/>.</typeparam>
    /// <param name="propertyBuilder">The builder for the property being configured.</param>
    /// <returns>The same builder instance so that multiple calls can be chained.</returns>
    public static PropertyBuilder<EncryptedSecret<TContext>?> HasNullableEncryptedSecretConversion<TContext>(
        this PropertyBuilder<EncryptedSecret<TContext>?> propertyBuilder)
        where TContext : ISecretContext {

        return propertyBuilder.HasConversion(
            new EncryptedSecretValueConverter<TContext>(),
            new EncryptedSecretValueComparer<TContext>()
        );
    }

    /// <summary>
    /// Configures all properties of type EncryptedSecret to use the custom converter and comparer.
    /// Used within ConfigureConventions.
    /// </summary>
    public static PropertiesConfigurationBuilder<EncryptedSecret<TContext>> HaveEncryptedSecretConversion<TContext>(
        this PropertiesConfigurationBuilder<EncryptedSecret<TContext>> builder)
        where TContext : ISecretContext {
        return builder.HaveConversion<EncryptedSecretValueConverter<TContext>, EncryptedSecretValueComparer<TContext>>();
    }

    /// <summary>
    /// Configures all properties of type EncryptedSecret to use the custom converter and comparer.
    /// Used within ConfigureConventions.
    /// </summary>
    public static PropertiesConfigurationBuilder<EncryptedSecret<TContext>?> HaveEncryptedSecretConversion<TContext>(
        this PropertiesConfigurationBuilder<EncryptedSecret<TContext>?> builder)
        where TContext : ISecretContext {
        return builder.HaveConversion<EncryptedSecretValueConverter<TContext>, EncryptedSecretValueComparer<TContext>>();
    }
}