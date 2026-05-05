namespace Wiaoj.Security;

/// <summary>
/// Re-encrypts application data that was encrypted with an older key version.
/// </summary>
/// <remarks>
/// <para>
/// Implement this interface for each <typeparamref name="TContext"/> that stores
/// encrypted data in the database. Register via
/// <c>AddDataRotator&lt;TContext, TRotator&gt;()</c>.
/// </para>
/// <para>
/// <b>Example skeleton:</b>
/// <code>
/// public sealed class UserSecretDataRotator : IDataRotator&lt;UserSecretContext&gt;
/// {
///     private readonly AppDbContext _db;
///     private readonly ISecretProtector&lt;UserSecretContext&gt; _protector;
///
///     public UserSecretDataRotator(AppDbContext db, ISecretProtector&lt;UserSecretContext&gt; protector)
///     {
///         _db        = db;
///         _protector = protector;
///     }
///
///     public async Task&lt;int&gt; RotateBatchAsync(int batchSize, CancellationToken cancellationToken)
///     {
///         var stale = await _db.Users
///             .Where(u => u.SecretKeyVersion &lt; _protector.CurrentKeyVersion)
///             .Take(batchSize)
///             .ToListAsync(cancellationToken);
///
///         foreach (var user in stale)
///         {
///             var encrypted = EncryptedSecret&lt;UserSecretContext&gt;.FromPersisted(
///                 user.EncryptedSecret, user.SecretKeyVersion);
///             var rotated = _protector.Rotate(encrypted);
///             user.EncryptedSecret   = rotated.Blob.ToStorageString();
///             user.SecretKeyVersion  = rotated.KeyVersion;
///         }
///
///         await _db.SaveChangesAsync(cancellationToken);
///         return stale.Count;
///     }
///
///     public async Task&lt;bool&gt; IsCompleteAsync(CancellationToken cancellationToken)
///         =&gt; !await _db.Users.AnyAsync(
///             u =&gt; u.SecretKeyVersion &lt; _protector.CurrentKeyVersion, cancellationToken);
/// }
/// </code>
/// </para>
/// </remarks>
/// <typeparam name="TContext">The secret domain.</typeparam>
public interface IDataRotator<TContext> where TContext : ISecretContext {
    /// <summary>
    /// Re-encrypts up to <paramref name="batchSize"/> records using the current key.
    /// </summary>
    /// <returns>Number of records actually re-encrypted in this batch.</returns>
    Task<int> RotateBatchAsync(int batchSize, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns <see langword="true"/> when all data is encrypted with the current key
    /// (no more records to rotate).
    /// </summary>
    Task<bool> IsCompleteAsync(CancellationToken cancellationToken = default);
}