using Microsoft.EntityFrameworkCore.ChangeTracking;
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
            dbValue => Parse(dbValue),
            new ConverterMappingHints(size: 255)) {
    }

    /// <summary>
    /// Parses the string value stored in the database back into an <see cref="EncryptedSecret{TContext}"/>.
    /// </summary>
    /// <param name="dbValue">The raw string value from the database (format: 'version:base64').</param>
    /// <returns>A restored instance of <see cref="EncryptedSecret{TContext}"/>.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the database value is not in the expected format.</exception>
    private static EncryptedSecret<TContext> Parse(string dbValue) {
        if(string.IsNullOrWhiteSpace(dbValue)) {
            return default;
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
        (left, right) =>
             left.KeyVersion.Value == right.KeyVersion.Value &&
             left.Blob.ToStorageString() == right.Blob.ToStorageString(), // You can also use SequenceEqual if Blob exposes a byte array.

        // 2. HashCode: How should the hash value be calculated? (Used for dictionaries/HashSets)
        secret => HashCode.Combine(secret.KeyVersion.Value, secret.Blob.ToStorageString()),

        // 3. Snapshot: How should EF Core take a copy of this object for change tracking?
        // Since EncryptedSecret is assumed to be IMMUTABLE, returning the reference directly is safe.
        // If it were mutable, a new instance (deep copy) would need to be created here.
        secret => secret) {
    }
}