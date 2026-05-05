using Microsoft.EntityFrameworkCore;

namespace Wiaoj.Security.EntityFrameworkCore;

/// <summary>
/// Minimal interface your <c>DbContext</c> must implement to plug into the key management system.
/// </summary>
/// <example>
/// <code>
/// public class AppDbContext : DbContext, IEncryptionKeyDbContext
/// {
///     public DbSet&lt;EncryptionKeyRecord&gt; EncryptionKeys { get; set; } = null!;
///
///     protected override void OnModelCreating(ModelBuilder modelBuilder)
///     {
///         modelBuilder.ApplyConfiguration(new EncryptionKeyRecordConfiguration());
///         // ... rest of your config
///     }
/// }
/// </code>
/// </example> 
public interface IEncryptionKeyDbContext {

    /// <summary>
    /// Gets the collection of cryptographic key records stored in the database.
    /// </summary>
    DbSet<EncryptionKeyRecord> EncryptionKeys { get; }

    /// <summary>
    /// Asynchronously saves all changes made in this context to the underlying database.
    /// </summary>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
    /// <returns>
    /// A task that represents the asynchronous save operation. 
    /// The task result contains the number of state entries written to the database.
    /// </returns>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}